using Aeron.Contracts;

namespace Aeron.Infrastructure;

/// <summary>
/// Resolves and secures AERON's isolated local storage layout under %LOCALAPPDATA%\Aeron.
/// Strictly rejects cloud-synced Documents folders and repository paths.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string ApplicationFolderName = "Aeron";
    public const string ConfigFileName = "aeron.config.json";
    public const string DatabaseFileName = "aeron_core.db";
    public const string ActiveLogFileName = "aeron.jsonl";

    private AppPaths(string normalizedRoot)
    {
        RootDirectory = normalizedRoot;
        ConfigDirectory = Path.Combine(normalizedRoot, "config");
        DataDirectory = Path.Combine(normalizedRoot, "data");
        BackupsDirectory = Path.Combine(DataDirectory, "backups");
        LogsDirectory = Path.Combine(normalizedRoot, "logs");

        ConfigFilePath = Path.Combine(ConfigDirectory, ConfigFileName);
        DatabaseFilePath = Path.Combine(DataDirectory, DatabaseFileName);
        ActiveLogFilePath = Path.Combine(LogsDirectory, ActiveLogFileName);
    }

    public string RootDirectory { get; }
    public string ConfigDirectory { get; }
    public string DataDirectory { get; }
    public string BackupsDirectory { get; }
    public string LogsDirectory { get; }
    public string ConfigFilePath { get; }
    public string DatabaseFilePath { get; }
    public string ActiveLogFilePath { get; }

    public static AppPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException(
                "Unable to resolve LocalApplicationData directory for isolated AERON storage.");
        }

        var root = Path.Combine(localAppData, ApplicationFolderName);
        return FromRoot(root);
    }

    public static AppPaths FromRoot(string rootDirectory, string? forbiddenDocumentsRootOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!Path.IsPathRooted(rootDirectory))
        {
            throw new ArgumentException("AERON storage root must be an absolute rooted path.", nameof(rootDirectory));
        }

        var normalizedRoot = TrimTrailingSeparator(Path.GetFullPath(rootDirectory));

        var documentsFolder = forbiddenDocumentsRootOverride ?? Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolderOption.DoNotVerify);

        if (!string.IsNullOrWhiteSpace(documentsFolder) && IsSameOrChildPath(normalizedRoot, documentsFolder))
        {
            throw new InvalidOperationException(
                "AERON storage root cannot reside inside the user Documents directory due to cloud-sync exposure policy.");
        }

        return new AppPaths(normalizedRoot);
    }

    public void EnsureCreatedAndSecured()
    {
        string[] directories =
        [
            RootDirectory,
            ConfigDirectory,
            DataDirectory,
            BackupsDirectory,
            LogsDirectory
        ];

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
            if (OperatingSystem.IsWindows())
            {
                WindowsAclHelper.SecureDirectory(directory);
            }
        }
    }

    private static bool IsSameOrChildPath(string candidatePath, string parentPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var normalizedCandidate = TrimTrailingSeparator(Path.GetFullPath(candidatePath));
        var normalizedParent = TrimTrailingSeparator(Path.GetFullPath(parentPath));

        if (string.Equals(normalizedCandidate, normalizedParent, comparison))
        {
            return true;
        }

        var parentWithSeparator = normalizedParent + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(parentWithSeparator, comparison);
    }

    private static string TrimTrailingSeparator(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
