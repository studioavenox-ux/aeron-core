using Xunit;

namespace Aeron.Security.Tests;

public sealed class ShortSecretRedactionTests
{
    [Theory]
    [InlineData("Q")]
    [InlineData("Q7")]
    [InlineData("Q7!")]
    public void RegisteredShortValues_AreNotSilentlyIgnored(string secret)
    {
        var filter = new RedactionFilter();
        filter.RegisterSensitiveValue(secret);

        Assert.True(filter.ContainsPotentialSecret($"before {secret} after"));
        Assert.Equal(string.Empty, filter.Redact($"before {secret} after"));
        Assert.Equal("unrelated text", filter.Redact("unrelated text"));
    }

    [Theory]
    [InlineData("REDACT")]
    [InlineData("[REDACTED]")]
    public void RegisteredValueInsideReplacementMarker_SuppressesWholeField(string secret)
    {
        var filter = new RedactionFilter();
        filter.RegisterSensitiveValue(secret);

        Assert.True(filter.ContainsPotentialSecret(secret));
        Assert.Equal(string.Empty, filter.Redact($"before {secret} after"));
        // The marker produced by a recognized credential pattern must be checked too.
        Assert.Equal(string.Empty, filter.Redact("password=synthetic-value"));
    }

    [Fact]
    public void LongerRegisteredValues_ContinueToUseTheRedactionMarker()
    {
        var filter = new RedactionFilter();
        filter.RegisterSensitiveValue("synthetic-value-9876");

        var output = filter.Redact("before synthetic-value-9876 after");
        Assert.Equal("before [REDACTED] after", output);
        Assert.Equal(output, filter.Redact(output));
    }
}
