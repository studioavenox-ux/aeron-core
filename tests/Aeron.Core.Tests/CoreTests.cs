using System.Collections.Concurrent;
using System.Diagnostics;
using Aeron.Contracts;
using Aeron.Infrastructure;
using Xunit;

namespace Aeron.Core.Tests;

public sealed class LifecycleAndEventBusTests
{
    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"aeron-core-tests-{Guid.NewGuid():N}");

    private static void DeleteTempRoot(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task LifecycleCoordinator_TransitionsCleanlyExecutesLifoHooksAndProvidesVerifiedSnapshot()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var observedTransitions = new List<(AppLifecycleState From, AppLifecycleState To)>();
            var hookOrder = new List<int>();

            await using (var host = AeronFoundationHost.Create(AppPaths.FromRoot(tempRoot)))
            {
                Assert.Equal(AppLifecycleState.Uninitialized, host.Lifecycle.CurrentState);

                using var sub = host.EventBus.Subscribe<LifecycleStateChangedEvent>(
                    evt => observedTransitions.Add((evt.PreviousState, evt.CurrentState)));

                _ = host.Lifecycle.RegisterShutdownHook(_ =>
                {
                    hookOrder.Add(1);
                    return Task.CompletedTask;
                });
                _ = host.Lifecycle.RegisterShutdownHook(_ =>
                {
                    hookOrder.Add(2);
                    return Task.CompletedTask;
                });
                _ = host.Lifecycle.RegisterShutdownHook(_ =>
                {
                    hookOrder.Add(3);
                    return Task.CompletedTask;
                });

                await host.Lifecycle.StartAsync();
                Assert.Equal(AppLifecycleState.Running, host.Lifecycle.CurrentState);
                Assert.NotNull(host.Lifecycle.StartedAtUtc);

                // Starting a second time while Running must be rejected.
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.Lifecycle.StartAsync());

                var snapshot = await host.HealthService.CaptureSnapshotAsync();
                Assert.Equal(AppLifecycleState.Running, snapshot.LifecycleState);
                Assert.True(snapshot.ConfigValid);
                Assert.True(snapshot.CloseToTrayEnabled);
                Assert.True(snapshot.SecurityInvariantsLocked);
                Assert.True(snapshot.RedactingLoggerActive);
                Assert.True(snapshot.Database.IsHealthy);
                Assert.Equal("wal", snapshot.Database.JournalMode);
                Assert.True(snapshot.Database.ForeignKeysEnabled);
                Assert.Equal(26200, snapshot.OsBaseline.MinimumRequiredBuild);

                await host.Lifecycle.StopAsync("Automated test teardown");
                Assert.Equal(AppLifecycleState.Stopped, host.Lifecycle.CurrentState);

                // Repeated StopAsync must be strictly idempotent.
                await host.Lifecycle.StopAsync("Redundant stop call");
                Assert.Equal(AppLifecycleState.Stopped, host.Lifecycle.CurrentState);

                // Restarting after Stopped must be rejected.
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.Lifecycle.StartAsync());
            }

            Assert.Equal(
                new[]
                {
                    (AppLifecycleState.Uninitialized, AppLifecycleState.Starting),
                    (AppLifecycleState.Starting, AppLifecycleState.Running),
                    (AppLifecycleState.Running, AppLifecycleState.Stopping),
                    (AppLifecycleState.Stopping, AppLifecycleState.Stopped)
                },
                observedTransitions);

            Assert.Equal(new[] { 3, 2, 1 }, hookOrder);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task LifecycleCoordinator_StartupPolicyFailure_RollsBackToFaultedAndCancelsToken()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var paths = AppPaths.FromRoot(tempRoot);
            paths.EnsureCreatedAndSecured();

            // Write a tampered config enabling forbidden local inference.
            await File.WriteAllTextAsync(paths.ConfigFilePath, """
                {
                  "schemaVersion": 1,
                  "window": { "closeToTray": true, "startMinimizedToTray": false },
                  "logging": { "minimumLevel": "Information", "maxFileBytes": 5242880, "maxRetainedFiles": 5 },
                  "security": {
                    "remoteOnlyInference": true,
                    "allowPaidFallback": false,
                    "allowLocalInference": true,
                    "allowSkillScripts": false,
                    "redactLogs": true
                  },
                  "platform": { "minimumWindowsBuild": 26200, "minimumWindowsRelease": "Windows 11 25H2" }
                }
                """);

            var rollbackHookCalled = false;

            await using var host = AeronFoundationHost.Create(paths);
            _ = host.Lifecycle.RegisterShutdownHook(_ =>
            {
                rollbackHookCalled = true;
                return Task.CompletedTask;
            });

            var ex = await Assert.ThrowsAsync<LifecycleStartupException>(() => host.Lifecycle.StartAsync());

            Assert.Equal("POLICY_LOCAL_INFERENCE_FORBIDDEN", ex.ErrorCode);
            Assert.Equal(AppLifecycleState.Faulted, host.Lifecycle.CurrentState);
            Assert.True(host.Lifecycle.ShutdownToken.IsCancellationRequested);
            Assert.True(rollbackHookCalled);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task TriggerEmergencyStop_CancelsAwaitingBackgroundWorkPromptly()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            await using var host = AeronFoundationHost.Create(AppPaths.FromRoot(tempRoot));
            await host.Lifecycle.StartAsync();

            var backgroundTask = Task.Delay(TimeSpan.FromSeconds(30), host.Lifecycle.ShutdownToken);
            var stopwatch = Stopwatch.StartNew();

            host.Lifecycle.TriggerEmergencyStop("Owner cancellation test");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backgroundTask);
            stopwatch.Stop();

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Expected prompt cancellation, observed {stopwatch.Elapsed.TotalMilliseconds} ms.");
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CoreEventBus_SupportsConcurrentDeliveryUnsubscribeAndSubscriberFaultIsolation()
    {
        var bus = new CoreEventBus();
        var received = new ConcurrentBag<string>();
        var unsubscribedCount = 0;

        using var faultySub = bus.Subscribe<WindowActivationRequestedEvent>(
            static _ => throw new InvalidOperationException("Simulated faulty observer"));

        using var healthySub = bus.Subscribe<WindowActivationRequestedEvent>(
            evt => received.Add(evt.Source));

        var transientSub = bus.Subscribe<WindowActivationRequestedEvent>(
            _ => Interlocked.Increment(ref unsubscribedCount));

        transientSub.Dispose();
        transientSub.Dispose(); // Must be idempotent.

        var publishers = Enumerable.Range(0, 20)
            .Select(i => Task.Run(() =>
                bus.Publish(new WindowActivationRequestedEvent($"source-{i}", DateTimeOffset.UtcNow))));

        await Task.WhenAll(publishers);

        Assert.Equal(20, received.Count);
        Assert.Equal(0, unsubscribedCount);
    }
}
