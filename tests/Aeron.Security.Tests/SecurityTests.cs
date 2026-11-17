using Aeron.Contracts;
using Xunit;

namespace Aeron.Security.Tests;

public sealed class RedactionFilterTests
{
    [Fact]
    public void Redact_AuthorizationAndBearerHeaders_RemovesRawTokens()
    {
        var filter = new RedactionFilter();
        var fakeToken = "synthBearer" + "TokenValue9876543210";
        var input = $"Request failed with Authorization: Bearer {fakeToken} during call.";

        var output = filter.Redact(input);

        Assert.DoesNotContain(fakeToken, output, StringComparison.Ordinal);
        Assert.Contains(RedactionFilter.RedactedPlaceholder, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_KnownProviderApiKeyPrefixes_RemovesEveryKey()
    {
        var filter = new RedactionFilter();
        string[] syntheticKeys =
        [
            "sk-" + "syntheticUnitTestKeyAlpha123456",
            "sk-or-v1-" + "syntheticOpenRouterTest12345678",
            "ghp_" + "syntheticGitHubPatValue1234567890",
            "github_pat_" + "syntheticFineGrainedToken12345678",
            "xoxb-" + "1234567890-syntheticSlackTest",
            "AIza" + "SyntheticGoogleKeyTest0123456789",
            "AKIA" + "SYNTHETICTEST123",
            "glpat-" + "syntheticGitLabToken12345"
        ];

        foreach (var key in syntheticKeys)
        {
            var output = filter.Redact($"Encountered credential value {key} in diagnostic stream.");
            Assert.DoesNotContain(key, output, StringComparison.Ordinal);
            Assert.Contains(RedactionFilter.RedactedPlaceholder, output, StringComparison.Ordinal);
            Assert.True(filter.ContainsPotentialSecret(key));
        }
    }

    [Fact]
    public void Redact_JwtUrlCredentialsJsonSecretsConnectionStringsAndPem_ScrubsCompletely()
    {
        var filter = new RedactionFilter();

        var fakeJwt = "eyJhbGciOiJIUzI1NiJ9." + "eyJzdWIiOiJ0ZXN0LXVzZXIifQ." + "c3ludGhldGljU2lnbmF0dXJlMTIzNDU";
        var fakeUrlPass = "UrlPass" + "99887766!";
        var fakeJsonSecret = "JsonApi" + "Secret445566";
        var fakeConnPass = "ConnStr" + "Pass112233";
        var fakePemBody = "SYNTHETIC_PEM_BODY_DO_NOT_USE_991122";
        var fakePem = "-----BEGIN RSA " + "PRIVATE KEY-----\n" +
                      fakePemBody + "\n" +
                      "-----END RSA " + "PRIVATE KEY-----";

        var composite = $$"""
            jwt={{fakeJwt}}
            endpoint=https://svc_user:{{fakeUrlPass}}@example.invalid/v1
            payload={"apiKey": "{{fakeJsonSecret}}", "safeField": "visible-ok"}
            conn=Server=localhost;User Id=app;Password={{fakeConnPass}};Pooling=false;
            key={{fakePem}}
            """;

        var redacted = filter.Redact(composite);

        Assert.DoesNotContain(fakeJwt, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fakeUrlPass, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fakeJsonSecret, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fakeConnPass, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fakePemBody, redacted, StringComparison.Ordinal);
        Assert.Contains("visible-ok", redacted, StringComparison.Ordinal);
        Assert.Contains(RedactionFilter.RedactedPlaceholder, redacted, StringComparison.Ordinal);

        // Redaction must remain strictly idempotent on a second pass.
        var secondPass = filter.Redact(redacted);
        Assert.Equal(redacted, secondPass);
    }

    [Fact]
    public void Redact_RuntimeRegisteredSecrets_ReplacesLongestFirstAndEscapesSpecialChars()
    {
        var filter = new RedactionFilter();
        var shortSecret = "CustomVault$Secret";
        var longerOverlappingSecret = "CustomVault$Secret+Extended[2026]";

        filter.RegisterSensitiveValue(shortSecret);
        filter.RegisterSensitiveValue(longerOverlappingSecret);

        var output = filter.Redact($"Values: ({longerOverlappingSecret}) and ({shortSecret}).");

        Assert.DoesNotContain(shortSecret, output, StringComparison.Ordinal);
        Assert.DoesNotContain("Extended[2026]", output, StringComparison.Ordinal);
        Assert.Equal("Values: ([REDACTED]) and ([REDACTED]).", output);
    }

    [Fact]
    public void Redact_OrdinaryFoundationDiagnostics_PreservesReadableText()
    {
        var filter = new RedactionFilter();
        const string message = "AERON foundation transitioned to Running state (SQLite WAL enabled, Windows 11 25H2).";

        var output = filter.Redact(message);

        Assert.Equal(message, output);
        Assert.False(filter.ContainsPotentialSecret(message));
    }
}

public sealed class ConfigPolicyValidatorTests
{
    private static ConfigPolicyValidator CreateValidator() => new(new RedactionFilter());

    [Fact]
    public void ValidateConfig_DefaultConfiguration_SucceedsAndLocksInvariants()
    {
        var validator = CreateValidator();
        var config = AeronAppConfig.CreateDefault();

        var result = validator.ValidateConfig(config);

        Assert.True(result.IsSuccess);
        Assert.True(config.Window.CloseToTray);
        Assert.Equal(26200, config.Platform.MinimumWindowsBuild);
        Assert.Equal("Windows 11 25H2", config.Platform.MinimumWindowsRelease);
        Assert.True(ConfigPolicyValidator.AreSecurityInvariantsLocked(config));
    }

    [Fact]
    public void ValidateConfig_SecurityInvariantViolations_FailClosedWithSpecificErrorCodes()
    {
        var validator = CreateValidator();
        var baseline = AeronAppConfig.CreateDefault();

        var remoteDisabled = validator.ValidateConfig(baseline with
        {
            Security = baseline.Security with { RemoteOnlyInference = false }
        });
        Assert.True(remoteDisabled.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.RemoteOnlyRequired, remoteDisabled.ErrorCode);

        var paidEnabled = validator.ValidateConfig(baseline with
        {
            Security = baseline.Security with { AllowPaidFallback = true }
        });
        Assert.True(paidEnabled.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.PaidFallbackForbidden, paidEnabled.ErrorCode);

        var localEnabled = validator.ValidateConfig(baseline with
        {
            Security = baseline.Security with { AllowLocalInference = true }
        });
        Assert.True(localEnabled.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.LocalInferenceForbidden, localEnabled.ErrorCode);

        var scriptsEnabled = validator.ValidateConfig(baseline with
        {
            Security = baseline.Security with { AllowSkillScripts = true }
        });
        Assert.True(scriptsEnabled.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.SkillScriptsForbidden, scriptsEnabled.ErrorCode);

        var redactionDisabled = validator.ValidateConfig(baseline with
        {
            Security = baseline.Security with { RedactLogs = false }
        });
        Assert.True(redactionDisabled.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.LogRedactionRequired, redactionDisabled.ErrorCode);
    }

    [Theory]
    [InlineData(19045)]
    [InlineData(22621)]
    [InlineData(22631)]
    [InlineData(26100)]
    [InlineData(26199)]
    public void ValidateConfig_MinimumWindowsBuildBelow25H2_FailsClosed(int obsoleteBuild)
    {
        var validator = CreateValidator();
        var invalid = AeronAppConfig.CreateDefault() with
        {
            Platform = new PlatformBaselinePolicy
            {
                MinimumWindowsBuild = obsoleteBuild,
                MinimumWindowsRelease = "Windows 11"
            }
        };

        var result = validator.ValidateConfig(invalid);

        Assert.True(result.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.WindowsBaselineInvalid, result.ErrorCode);
    }

    [Fact]
    public void ValidateRawJson_InlineSecretPropertyOrValue_FailsClosed()
    {
        var validator = CreateValidator();
        var syntheticKey = "sk-" + "syntheticInlineConfigSecret998877";

        const string secretPropertyJson = """
            {
              "schemaVersion": 1,
              "apiKey": "any-value-here"
            }
            """;

        var propResult = validator.ValidateRawJson(secretPropertyJson);
        Assert.True(propResult.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.InlineSecretForbidden, propResult.ErrorCode);

        var secretValueJson = $$"""
            {
              "schemaVersion": 1,
              "platform": {
                "minimumWindowsBuild": 26200,
                "minimumWindowsRelease": "{{syntheticKey}}"
              }
            }
            """;

        var valResult = validator.ValidateRawJson(secretValueJson);
        Assert.True(valResult.IsFailure);
        Assert.Equal(ConfigPolicyValidator.ErrorCodes.InlineSecretForbidden, valResult.ErrorCode);
        Assert.DoesNotContain(syntheticKey, valResult.ErrorMessage, StringComparison.Ordinal);
    }
}
