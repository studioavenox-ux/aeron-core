using System.Text.Json;
using Aeron.Contracts;
using Aeron.Security;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aeron.Infrastructure.Tests;

internal sealed class TempSandbox : IDisposable
{
    public TempSandbox()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"aeron-m1-tests-{Guid.NewGuid():N}");
        Paths = AppPaths.FromRoot(RootPath);
    }

    public string RootPath { get; }
    public AppPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of temp test directory.
        }
    }
}

public sealed class AppPathsAndConfigTests
{
    [Fact]
    public void EnsureCreatedAndSecured_CreatesIsolatedLayoutAndEnforcesCurrentUserAclOnWindows()
    {
        using var sandbox = new TempSandbox();

        sandbox.Paths.EnsureCreatedAndSecured();

        Assert.True(Directory.Exists(sandbox.Paths.RootDirectory));
        Assert.True(Directory.Exists(sandbox.Paths.ConfigDirectory));
        Assert.True(Directory.Exists(sandbox.Paths.DataDirectory));
        Assert.True(Directory.Exists(sandbox.Paths.BackupsDirectory));
        Assert.True(Directory.Exists(sandbox.Paths.LogsDirectory));

        if (OperatingSystem.IsWindows())
        {
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.RootDirectory));
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.ConfigDirectory));
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.DataDirectory));
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.BackupsDirectory));
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.LogsDirectory));
        }
    }

    [Fact]
    public void FromRoot_PathInsideDocumentsFolder_ThrowsFailClosed()
    {
        var fakeDocuments = Path.Combine(Path.GetTempPath(), $"aeron-fake-docs-{Guid.NewGuid():N}", "Documents");
        var forbiddenChild = Path.Combine(fakeDocuments, "Aeron");

        Assert.Throws<InvalidOperationException>(() =>
            AppPaths.FromRoot(forbiddenChild, forbiddenDocumentsRootOverride: fakeDocuments));
    }

    [Fact]
    public void SecureConfigStore_BootstrapsDefaultSavesAtomicallyAndSecuresFileAcl()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        var store = new SecureConfigStore(sandbox.Paths, new ConfigPolicyValidator(filter), filter);

        var initial = store.LoadOrCreateDefault();
        Assert.True(initial.IsSuccess);
        Assert.True(initial.Value.Window.CloseToTray);
        Assert.True(File.Exists(sandbox.Paths.ConfigFilePath));

        if (OperatingSystem.IsWindows())
        {
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.ConfigFilePath));
        }

        var updated = initial.Value with
        {
            Window = initial.Value.Window with { CloseToTray = false }
        };

        var saveResult = store.Save(updated);
        Assert.True(saveResult.IsSuccess);

        // Verify zero leftover atomic temporary files.
        var tempFiles = Directory.GetFiles(sandbox.Paths.ConfigDirectory, "*.tmp");
        Assert.Empty(tempFiles);

        var reloaded = store.LoadOrCreateDefault();
        Assert.True(reloaded.IsSuccess);
        Assert.False(reloaded.Value.Window.CloseToTray);
        Assert.True(reloaded.Value.Security.RemoteOnlyInference);
        Assert.False(reloaded.Value.Security.AllowPaidFallback);
        Assert.False(reloaded.Value.Security.AllowLocalInference);
        Assert.False(reloaded.Value.Security.AllowSkillScripts);
    }

    [Fact]
    public void SecureConfigStore_RejectsPolicyTamperingInlineSecretsAndCorruptJsonWithoutOverwritingFile()
    {
        using var sandbox = new TempSandbox();
        sandbox.Paths.EnsureCreatedAndSecured();

        var filter = new RedactionFilter();
        var store = new SecureConfigStore(sandbox.Paths, new ConfigPolicyValidator(filter), filter);

        // 1. Corrupt JSON must fail closed and preserve original bytes untouched.
        const string corruptJson = "{ \"schemaVersion\": 1, broken-json ";
        File.WriteAllText(sandbox.Paths.ConfigFilePath, corruptJson);

        var corruptResult = store.LoadOrCreateDefault();
        Assert.True(corruptResult.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.InvalidJson, corruptResult.ErrorCode);
        Assert.Equal(corruptJson, File.ReadAllText(sandbox.Paths.ConfigFilePath));

        // 2. Paid fallback enabled must fail closed.
        const string paidFallbackJson = """
            {
              "schemaVersion": 1,
              "window": { "closeToTray": true, "startMinimizedToTray": false },
              "logging": { "minimumLevel": "Information", "maxFileBytes": 5242880, "maxRetainedFiles": 5 },
              "security": {
                "remoteOnlyInference": true,
                "allowPaidFallback": true,
                "allowLocalInference": false,
                "allowSkillScripts": false,
                "redactLogs": true
              },
              "platform": { "minimumWindowsBuild": 26200, "minimumWindowsRelease": "Windows 11 25H2" }
            }
            """;
        File.WriteAllText(sandbox.Paths.ConfigFilePath, paidFallbackJson);

        var paidResult = store.LoadOrCreateDefault();
        Assert.True(paidResult.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.PaidFallbackForbidden, paidResult.ErrorCode);

        // 3. Inline credential field must fail closed.
        var fakeToken = "sk-" + "syntheticStoredConfigSecret554433";
        var inlineSecretJson = $$"""
            {
              "schemaVersion": 1,
              "omniroute_api_key": "{{fakeToken}}"
            }
            """;
        File.WriteAllText(sandbox.Paths.ConfigFilePath, inlineSecretJson);

        var secretResult = store.LoadOrCreateDefault();
        Assert.True(secretResult.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.InlineSecretForbidden, secretResult.ErrorCode);
        Assert.DoesNotContain(fakeToken, secretResult.ErrorMessage, StringComparison.Ordinal);
    }
}

public sealed class RedactingLoggerAndOsBaselineTests
{
    [Fact]
    public void RedactingLogger_ZeroSecretLeaksAcrossMessagesPropertiesAndNestedExceptions_WithRotation()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        var registeredSecret = "RegisteredVault" + "Material#778899";
        filter.RegisterSensitiveValue(registeredSecret);

        var bearerSecret = "synthLogBearer" + "998877665544";
        var apiKeySecret = "sk-" + "syntheticLoggerApiKey1122334455";
        var rawPasswordValue = "shortPass!";
        var connPassword = "DbSecretPassword" + "44332211";
        var pemInner = "SYNTHETIC_PRIVATE_KEY_PAYLOAD_887766";
        var pemSecret = "-----BEGIN " + "PRIVATE KEY----- " + pemInner + " -----END " + "PRIVATE KEY-----";

        using (var logger = new RedactingLogger(
                   sandbox.Paths,
                   filter,
                   minimumLevel: AeronLogLevel.Debug,
                   maxFileBytes: 1024,
                   maxRetainedFiles: 3))
        {
            var inner = new InvalidOperationException(
                $"Connection failed for Server=db;Password={connPassword}; and {pemSecret}");
            var aggregate = new AggregateException(
                $"Aggregate failure carrying {registeredSecret}",
                inner,
                new UnauthorizedAccessException($"Authorization: Bearer {bearerSecret}"));

            for (var i = 0; i < 18; i++)
            {
                logger.LogError(
                    "SecurityAuditTest",
                    $"Attempt {i} failed using key {apiKeySecret} and secret {registeredSecret}",
                    aggregate,
                    new Dictionary<string, string?>
                    {
                        ["password"] = rawPasswordValue,
                        ["authorization"] = $"Bearer {bearerSecret}",
                        ["diagnosticNote"] = $"Normal value with embedded {apiKeySecret}"
                    });
            }

            logger.Flush();
        }

        var logFiles = Directory.GetFiles(sandbox.Paths.LogsDirectory, "*.jsonl");
        Assert.True(logFiles.Length >= 2, "Expected size-based log rotation to produce archive files.");
        Assert.True(logFiles.Length <= 4, "Expected active log plus at most 3 retained archive files.");

        if (OperatingSystem.IsWindows())
        {
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.ActiveLogFilePath));
        }

        string[] forbiddenSubstrings =
        [
            registeredSecret,
            bearerSecret,
            apiKeySecret,
            rawPasswordValue,
            connPassword,
            pemInner
        ];

        foreach (var file in logFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var secret in forbiddenSubstrings)
            {
                Assert.DoesNotContain(secret, content, StringComparison.Ordinal);
            }

            Assert.Contains(RedactionFilter.RedactedPlaceholder, content, StringComparison.Ordinal);

            // Every line must be strictly valid structured JSON.
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                using var doc = JsonDocument.Parse(line);
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            }
        }
    }

    [Theory]
    [InlineData(true, true, 26200, true)]
    [InlineData(true, true, 26300, true)]
    [InlineData(true, true, 26199, false)]
    [InlineData(true, true, 26100, false)]
    [InlineData(true, true, 22631, false)]
    [InlineData(true, true, 22621, false)]
    [InlineData(true, false, 26200, false)]
    [InlineData(false, true, 26200, false)]
    public void OsBaselineEvaluator_EnforcesWindows11Version25H2Build26200X64(
        bool isWindows,
        bool is64Bit,
        int build,
        bool expectedCompliant)
    {
        var report = OsBaselineEvaluator.EvaluateHost(isWindows, is64Bit, build);

        Assert.Equal(expectedCompliant, report.MeetsMinimumBaseline);
        Assert.Equal(26200, report.MinimumRequiredBuild);
        Assert.Equal("Windows 11 25H2", report.MinimumRequiredRelease);
    }
}

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task MigrateAndProbe_ConfiguresWalForeignKeysChecksumAndIdempotentRestart()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        var factory = new SqliteConnectionFactory(sandbox.Paths);
        var runner = new SqliteMigrationRunner(factory);
        var db = new SqliteDatabaseService(factory, runner, filter);

        var firstApplied = await runner.MigrateAsync();
        Assert.Equal(1, firstApplied);

        // Re-running migration on restart must be strictly idempotent.
        var secondApplied = await runner.MigrateAsync();
        Assert.Equal(0, secondApplied);

        var health = await db.ProbeAsync();
        Assert.True(health.IsHealthy);
        Assert.Equal("wal", health.JournalMode);
        Assert.True(health.ForeignKeysEnabled);
        Assert.True(health.IntegrityOk);
        Assert.Equal(1, health.AppliedMigrationCount);
        Assert.Equal(1, health.LatestSchemaVersion);

        if (OperatingSystem.IsWindows())
        {
            Assert.True(WindowsAclHelper.IsProtectedCurrentUserOnlyAcl(sandbox.Paths.DatabaseFilePath));
        }

        // Line-ending normalization guarantees identical SHA-256 checksum across CRLF and LF checkouts.
        var crlfChecksum = SqliteMigration.ComputeChecksum("CREATE TABLE t (id INT);\r\n");
        var lfChecksum = SqliteMigration.ComputeChecksum("CREATE TABLE t (id INT);\n");
        Assert.Equal(lfChecksum, crlfChecksum);
    }

    [Fact]
    public async Task MigrateAsync_FaultyMigrationRollsBackAtomically_AndCreatesPreUpgradeBackup()
    {
        using var sandbox = new TempSandbox();
        var factory = new SqliteConnectionFactory(sandbox.Paths);
        var builtIn = SqliteMigrationRunner.LoadBuiltInMigrations();

        var initialRunner = new SqliteMigrationRunner(factory, migrationsOverride: builtIn);
        Assert.Equal(1, await initialRunner.MigrateAsync());

        var faultySet = new List<SqliteMigration>(builtIn)
        {
            new(2, "broken_upgrade", """
                CREATE TABLE partial_table_should_rollback (id INTEGER PRIMARY KEY);
                THIS IS INVALID SQL SYNTAX;
                """)
        };

        var faultyRunner = new SqliteMigrationRunner(factory, migrationsOverride: faultySet);
        await Assert.ThrowsAsync<SqliteException>(() => faultyRunner.MigrateAsync());

        // Pre-upgrade backup was captured before executing v2.
        var backups = Directory.GetFiles(sandbox.Paths.BackupsDirectory, "*.db");
        Assert.Single(backups);

        // Partial table and version 2 record must both have rolled back completely.
        await using var conn = await factory.OpenConnectionAsync();
        await using var tableCheck = conn.CreateCommand();
        tableCheck.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='partial_table_should_rollback';";
        var tableCount = Convert.ToInt32(await tableCheck.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, tableCount);

        await using var versionCheck = conn.CreateCommand();
        versionCheck.CommandText = "SELECT MAX(version) FROM schema_migrations;";
        var maxVersion = Convert.ToInt32(await versionCheck.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(1, maxVersion);
    }

    [Fact]
    public async Task MigrateAsync_AlteredAppliedMigrationChecksum_FailsClosed()
    {
        using var sandbox = new TempSandbox();
        var factory = new SqliteConnectionFactory(sandbox.Paths);

        var original = new[]
        {
            new SqliteMigration(1, "foundation", "CREATE TABLE sample_meta (id INTEGER PRIMARY KEY);")
        };
        var initialRunner = new SqliteMigrationRunner(factory, migrationsOverride: original);
        Assert.Equal(1, await initialRunner.MigrateAsync());

        var tampered = new[]
        {
            new SqliteMigration(1, "foundation", "CREATE TABLE sample_meta (id INTEGER PRIMARY KEY, extra TEXT);")
        };
        var tamperedRunner = new SqliteMigrationRunner(factory, migrationsOverride: tampered);

        await Assert.ThrowsAsync<MigrationChecksumMismatchException>(() => tamperedRunner.MigrateAsync());
    }

    [Fact]
    public async Task RecordTransitionAsync_RedactsPotentialSecretsBeforeWritingToSqlite()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        var factory = new SqliteConnectionFactory(sandbox.Paths);
        var runner = new SqliteMigrationRunner(factory);
        var db = new SqliteDatabaseService(factory, runner, filter);
        await db.InitializeAsync();

        var fakeSecret = "sk-" + "syntheticJournalLeakAttempt998877";
        await db.RecordTransitionAsync(
            "aeron-test-01",
            AppLifecycleState.Stopped,
            $"Shutdown after error with {fakeSecret}",
            DateTimeOffset.UtcNow);

        await using var conn = await factory.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT reason FROM lifecycle_journal LIMIT 1;";
        var storedReason = (string?)await cmd.ExecuteScalarAsync();

        Assert.NotNull(storedReason);
        Assert.DoesNotContain(fakeSecret, storedReason, StringComparison.Ordinal);
        Assert.Contains(RedactionFilter.RedactedPlaceholder, storedReason, StringComparison.Ordinal);
    }
}
