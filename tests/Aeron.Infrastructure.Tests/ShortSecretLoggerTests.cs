using System.Text.Json;
using Aeron.Contracts;
using Aeron.Security;
using Xunit;

namespace Aeron.Infrastructure.Tests;

public sealed class ShortSecretLoggerTests
{
    [Fact]
    public void Logger_SuppressesRegisteredShortValuesAcrossStructuredFieldsAndExceptions()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        const string secret = "Q7!";
        filter.RegisterSensitiveValue(secret);

        using (var logger = new RedactingLogger(sandbox.Paths, filter))
        {
            logger.Log(AeronLogLevel.Error, $"category {secret}", $"message {secret}",
                new Dictionary<string, string?> { [$"key {secret}"] = $"value {secret}" },
                new InvalidOperationException($"outer {secret}", new InvalidOperationException($"inner {secret}")));
            logger.Flush();
        }

        var content = File.ReadAllText(sandbox.Paths.ActiveLogFilePath);
        Assert.DoesNotContain(secret, content, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(content);
        Assert.Equal(string.Empty, json.RootElement.GetProperty("category").GetString());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("message").GetString());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("exception").GetProperty("message").GetString());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("exception").GetProperty("innerException").GetProperty("message").GetString());
    }

    [Fact]
    public void Logger_ScrubsSensitiveFieldMarkerWhenMarkerIsRegisteredSecret()
    {
        using var sandbox = new TempSandbox();
        var filter = new RedactionFilter();
        filter.RegisterSensitiveValue(RedactionFilter.RedactedPlaceholder);

        using (var logger = new RedactingLogger(sandbox.Paths, filter))
        {
            logger.Log(AeronLogLevel.Information, "category", "message",
                new Dictionary<string, string?> { ["password"] = "synthetic-password" });
            logger.Flush();
        }

        var content = File.ReadAllText(sandbox.Paths.ActiveLogFilePath);
        Assert.DoesNotContain(RedactionFilter.RedactedPlaceholder, content, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-password", content, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(content);
        Assert.Equal(string.Empty, json.RootElement.GetProperty("properties").GetProperty("password").GetString());
    }
}
