using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aeron.Contracts;

/// <summary>
/// Strongly typed, secret-free application configuration for AERON Milestone 1.
/// Stored strictly under the current user's isolated LocalApplicationData directory.
/// </summary>
public sealed record AeronAppConfig
{
    public const int CurrentSchemaVersion = 1;
    public const int Windows11Version25H2Build = 26200;
    public const string Windows11Version25H2Release = "Windows 11 25H2";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public WindowPreferences Window { get; init; } = new();

    public LoggingPreferences Logging { get; init; } = new();

    public SecurityInvariants Security { get; init; } = new();

    public PlatformBaselinePolicy Platform { get; init; } = new();

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }

    public static AeronAppConfig CreateDefault() => new();
}

public sealed record WindowPreferences
{
    /// <summary>
    /// Approved default (D-M1-03): closing the main window (X) minimizes AERON to the system tray.
    /// </summary>
    public bool CloseToTray { get; init; } = true;

    public bool StartMinimizedToTray { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LoggingPreferences
{
    public const long MinAllowedFileBytes = 64 * 1024;
    public const long DefaultMaxFileBytes = 5 * 1024 * 1024;
    public const long MaxAllowedFileBytes = 100 * 1024 * 1024;
    public const int MinRetainedFiles = 1;
    public const int DefaultMaxRetainedFiles = 5;
    public const int MaxAllowedRetainedFiles = 50;

    [JsonConverter(typeof(JsonStringEnumConverter<AeronLogLevel>))]
    public AeronLogLevel MinimumLevel { get; init; } = AeronLogLevel.Information;

    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    public int MaxRetainedFiles { get; init; } = DefaultMaxRetainedFiles;

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Immutable non-negotiable security invariants enforced fail-closed at startup and save time.
/// </summary>
public sealed record SecurityInvariants
{
    public bool RemoteOnlyInference { get; init; } = true;

    public bool AllowPaidFallback { get; init; }

    public bool AllowLocalInference { get; init; }

    public bool AllowSkillScripts { get; init; }

    public bool RedactLogs { get; init; } = true;

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Minimum supported operating system baseline (D-M1-02: Windows 11 25H2 Build 26200 x64 or newer).
/// Kept distinct from the APEX development machine specification.
/// </summary>
public sealed record PlatformBaselinePolicy
{
    public int MinimumWindowsBuild { get; init; } = AeronAppConfig.Windows11Version25H2Build;

    public string MinimumWindowsRelease { get; init; } = AeronAppConfig.Windows11Version25H2Release;

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
