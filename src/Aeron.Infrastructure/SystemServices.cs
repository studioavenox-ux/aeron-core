using Aeron.Contracts;

namespace Aeron.Infrastructure;

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Evaluates host OS eligibility against AERON's minimum supported Windows baseline
/// (Windows 11 25H2 Build 26200, x64 or newer), distinct from the APEX development machine profile.
/// </summary>
public sealed class OsBaselineEvaluator : IOsBaselineEvaluator
{
    public OsBaselineReport Evaluate(int minimumRequiredBuild, string minimumRequiredRelease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minimumRequiredRelease);

        var isWindows = OperatingSystem.IsWindows();
        var is64Bit = Environment.Is64BitOperatingSystem;
        var build = isWindows ? Environment.OSVersion.Version.Build : 0;

        return EvaluateHost(isWindows, is64Bit, build, minimumRequiredBuild, minimumRequiredRelease);
    }

    public static OsBaselineReport EvaluateHost(
        bool isWindows,
        bool is64BitOperatingSystem,
        int detectedBuild,
        int minimumRequiredBuild = AeronAppConfig.Windows11Version25H2Build,
        string minimumRequiredRelease = AeronAppConfig.Windows11Version25H2Release)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minimumRequiredRelease);

        if (minimumRequiredBuild < AeronAppConfig.Windows11Version25H2Build)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredBuild),
                minimumRequiredBuild,
                $"Minimum supported build cannot be lower than {AeronAppConfig.Windows11Version25H2Build} ({AeronAppConfig.Windows11Version25H2Release}).");
        }

        if (!isWindows)
        {
            return new OsBaselineReport(
                IsWindows: false,
                Is64BitOperatingSystem: is64BitOperatingSystem,
                DetectedBuild: detectedBuild,
                MinimumRequiredBuild: minimumRequiredBuild,
                MinimumRequiredRelease: minimumRequiredRelease,
                MeetsMinimumBaseline: false,
                Summary: $"Unsupported OS family. AERON requires {minimumRequiredRelease} (Build {minimumRequiredBuild}+, x64).");
        }

        if (!is64BitOperatingSystem)
        {
            return new OsBaselineReport(
                IsWindows: true,
                Is64BitOperatingSystem: false,
                DetectedBuild: detectedBuild,
                MinimumRequiredBuild: minimumRequiredBuild,
                MinimumRequiredRelease: minimumRequiredRelease,
                MeetsMinimumBaseline: false,
                Summary: $"Unsupported 32-bit Windows (Build {detectedBuild}). AERON requires 64-bit {minimumRequiredRelease} (Build {minimumRequiredBuild}+).");
        }

        if (detectedBuild < minimumRequiredBuild)
        {
            return new OsBaselineReport(
                IsWindows: true,
                Is64BitOperatingSystem: true,
                DetectedBuild: detectedBuild,
                MinimumRequiredBuild: minimumRequiredBuild,
                MinimumRequiredRelease: minimumRequiredRelease,
                MeetsMinimumBaseline: false,
                Summary: $"Windows Build {detectedBuild} is below minimum supported {minimumRequiredRelease} (Build {minimumRequiredBuild}+, x64).");
        }

        return new OsBaselineReport(
            IsWindows: true,
            Is64BitOperatingSystem: true,
            DetectedBuild: detectedBuild,
            MinimumRequiredBuild: minimumRequiredBuild,
            MinimumRequiredRelease: minimumRequiredRelease,
            MeetsMinimumBaseline: true,
            Summary: $"Compliant: 64-bit Windows Build {detectedBuild} meets {minimumRequiredRelease} (Build {minimumRequiredBuild}+) baseline.");
    }
}
