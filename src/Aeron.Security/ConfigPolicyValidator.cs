using System.Text.Json;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Enforces fail-closed security invariants and rejects inline secrets in AERON configuration.
/// </summary>
public sealed class ConfigPolicyValidator
{
    public const int MaxRawConfigBytes = 256 * 1024;

    public static class ErrorCodes
    {
        public const string EmptyOrOversized = "CONFIG_EMPTY_OR_OVERSIZED";
        public const string InvalidJson = "CONFIG_INVALID_JSON";
        public const string UnsupportedSchema = "CONFIG_UNSUPPORTED_SCHEMA";
        public const string UnknownProperty = "CONFIG_UNKNOWN_PROPERTY";
        public const string InlineSecretForbidden = "CONFIG_INLINE_SECRET_FORBIDDEN";
        public const string RemoteOnlyRequired = "POLICY_REMOTE_ONLY_REQUIRED";
        public const string PaidFallbackForbidden = "POLICY_PAID_FALLBACK_FORBIDDEN";
        public const string LocalInferenceForbidden = "POLICY_LOCAL_INFERENCE_FORBIDDEN";
        public const string SkillScriptsForbidden = "POLICY_SKILL_SCRIPTS_FORBIDDEN";
        public const string LogRedactionRequired = "POLICY_LOG_REDACTION_REQUIRED";
        public const string WindowsBaselineInvalid = "POLICY_WINDOWS_BASELINE_INVALID";
        public const string LoggingBoundsInvalid = "CONFIG_LOGGING_BOUNDS_INVALID";
    }

    private readonly IRedactionFilter _redactionFilter;

    public ConfigPolicyValidator(IRedactionFilter redactionFilter)
    {
        ArgumentNullException.ThrowIfNull(redactionFilter);
        _redactionFilter = redactionFilter;
    }

    public Result ValidateRawJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Result.Failure(ErrorCodes.EmptyOrOversized, "Configuration content must not be empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(rawJson) > MaxRawConfigBytes)
        {
            return Result.Failure(ErrorCodes.EmptyOrOversized, "Configuration file exceeds maximum permitted size.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure(ErrorCodes.InvalidJson, "Configuration root must be a JSON object.");
            }

            return InspectJsonElementForSecrets(document.RootElement, "$");
        }
        catch (JsonException)
        {
            return Result.Failure(ErrorCodes.InvalidJson, "Configuration file contains malformed JSON.");
        }
    }

    public Result ValidateConfig(AeronAppConfig? config)
    {
        if (config is null)
        {
            return Result.Failure(ErrorCodes.InvalidJson, "Configuration deserialized to null.");
        }

        if (config.Window is null || config.Logging is null || config.Security is null || config.Platform is null)
        {
            return Result.Failure(ErrorCodes.InvalidJson, "Configuration is missing a required section.");
        }

        if (HasExtensionProperties(config.ExtensionData) ||
            HasExtensionProperties(config.Window.ExtensionData) ||
            HasExtensionProperties(config.Logging.ExtensionData) ||
            HasExtensionProperties(config.Security.ExtensionData) ||
            HasExtensionProperties(config.Platform.ExtensionData))
        {
            var secretCheck = CheckExtensionPropertiesForSecrets(
                config.ExtensionData,
                config.Window.ExtensionData,
                config.Logging.ExtensionData,
                config.Security.ExtensionData,
                config.Platform.ExtensionData);

            if (secretCheck.IsFailure)
            {
                return secretCheck;
            }

            return Result.Failure(
                ErrorCodes.UnknownProperty,
                "Configuration contains unrecognized properties rejected by strict schema policy.");
        }

        if (config.SchemaVersion != AeronAppConfig.CurrentSchemaVersion)
        {
            return Result.Failure(
                ErrorCodes.UnsupportedSchema,
                $"Unsupported configuration schema version '{config.SchemaVersion}'. Expected '{AeronAppConfig.CurrentSchemaVersion}'.");
        }

        if (!config.Security.RemoteOnlyInference)
        {
            return Result.Failure(
                ErrorCodes.RemoteOnlyRequired,
                "Security invariant violation: RemoteOnlyInference must remain true.");
        }

        if (config.Security.AllowPaidFallback)
        {
            return Result.Failure(
                ErrorCodes.PaidFallbackForbidden,
                "Security invariant violation: AllowPaidFallback must remain false.");
        }

        if (config.Security.AllowLocalInference)
        {
            return Result.Failure(
                ErrorCodes.LocalInferenceForbidden,
                "Security invariant violation: AllowLocalInference must remain false.");
        }

        if (config.Security.AllowSkillScripts)
        {
            return Result.Failure(
                ErrorCodes.SkillScriptsForbidden,
                "Security invariant violation: AllowSkillScripts must remain false in v1.");
        }

        if (!config.Security.RedactLogs)
        {
            return Result.Failure(
                ErrorCodes.LogRedactionRequired,
                "Security invariant violation: RedactLogs must remain true.");
        }

        if (config.Platform.MinimumWindowsBuild < AeronAppConfig.Windows11Version25H2Build ||
            string.IsNullOrWhiteSpace(config.Platform.MinimumWindowsRelease))
        {
            return Result.Failure(
                ErrorCodes.WindowsBaselineInvalid,
                $"Platform baseline violation: MinimumWindowsBuild must be >= {AeronAppConfig.Windows11Version25H2Build} ({AeronAppConfig.Windows11Version25H2Release}).");
        }

        if (_redactionFilter.ContainsPotentialSecret(config.Platform.MinimumWindowsRelease))
        {
            return Result.Failure(
                ErrorCodes.InlineSecretForbidden,
                "Inline secrets or credentials are strictly prohibited in configuration files.");
        }

        if (!Enum.IsDefined(config.Logging.MinimumLevel) ||
            config.Logging.MaxFileBytes < LoggingPreferences.MinAllowedFileBytes ||
            config.Logging.MaxFileBytes > LoggingPreferences.MaxAllowedFileBytes ||
            config.Logging.MaxRetainedFiles < LoggingPreferences.MinRetainedFiles ||
            config.Logging.MaxRetainedFiles > LoggingPreferences.MaxAllowedRetainedFiles)
        {
            return Result.Failure(
                ErrorCodes.LoggingBoundsInvalid,
                "Logging preferences fall outside allowed safety bounds.");
        }

        return Result.Success();
    }

    public static bool AreSecurityInvariantsLocked(AeronAppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Security is
               {
                   RemoteOnlyInference: true,
                   AllowPaidFallback: false,
                   AllowLocalInference: false,
                   AllowSkillScripts: false,
                   RedactLogs: true
               } &&
               config.Platform.MinimumWindowsBuild >= AeronAppConfig.Windows11Version25H2Build;
    }

    private Result InspectJsonElementForSecrets(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (RedactionFilter.IsSensitiveKeyName(property.Name))
                    {
                        return Result.Failure(
                            ErrorCodes.InlineSecretForbidden,
                            $"Configuration contains forbidden credential property at '{path}.{RedactionFilter.RedactedPlaceholder}'.");
                    }

                    var nested = InspectJsonElementForSecrets(property.Value, $"{path}.{property.Name}");
                    if (nested.IsFailure)
                    {
                        return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var nested = InspectJsonElementForSecrets(item, $"{path}[{index}]");
                    if (nested.IsFailure)
                    {
                        return nested;
                    }
                    index++;
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (_redactionFilter.ContainsPotentialSecret(stringValue))
                {
                    return Result.Failure(
                        ErrorCodes.InlineSecretForbidden,
                        $"Configuration contains a prohibited inline secret value at '{path}'.");
                }
                break;
        }

        return Result.Success();
    }

    private Result CheckExtensionPropertiesForSecrets(params IDictionary<string, JsonElement>?[] bags)
    {
        foreach (var bag in bags)
        {
            if (bag is null)
            {
                continue;
            }

            foreach (var pair in bag)
            {
                if (RedactionFilter.IsSensitiveKeyName(pair.Key))
                {
                    return Result.Failure(
                        ErrorCodes.InlineSecretForbidden,
                        "Inline secrets or credential fields are strictly prohibited in configuration files.");
                }

                var nested = InspectJsonElementForSecrets(pair.Value, pair.Key);
                if (nested.IsFailure)
                {
                    return nested;
                }
            }
        }

        return Result.Success();
    }

    private static bool HasExtensionProperties(IDictionary<string, JsonElement>? extensionData) =>
        extensionData is { Count: > 0 };
}
