using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Aeron.Architecture.Tests;

/// <summary>
/// Enforces AERON Milestone 1 architectural invariants, .NET 10 LTS targeting,
/// warnings-as-errors hygiene (including avoidance of experimental WPF0001 ThemeMode APIs),
/// module boundary isolation, scope discipline, and historical governance preservation.
/// </summary>
public sealed class ArchitectureAndGovernanceTests
{
    private static readonly string RepoRoot = LocateRepositoryRoot();

    private static readonly string[] ForbiddenPackageSubstrings =
    [
        "LangChain",
        "SemanticKernel",
        "AutoGen",
        "CrewAI",
        "LlamaSharp",
        "Ollama",
        "OnnxRuntime",
        "TorchSharp",
        "TensorFlow",
        "OpenAI",
        "Anthropic",
        "System.Windows.Forms"
    ];

    private static readonly string[] ForbiddenModelWeightExtensions =
    [
        ".gguf",
        ".ggml",
        ".safetensors",
        ".onnx",
        ".pt",
        ".pth",
        ".tflite"
    ];

    private static readonly string[] ExpectedMilestone1SourceProjects =
    [
        "Aeron.Contracts",
        "Aeron.Core",
        "Aeron.Desktop",
        "Aeron.Infrastructure",
        "Aeron.Security"
    ];

    [Fact]
    public void BuildConfiguration_EnforcesCSharp14Net10LtsWarningsAsErrorsAndNoBlanketSuppressions()
    {
        var globalJsonPath = Path.Combine(RepoRoot, "global.json");
        Assert.True(File.Exists(globalJsonPath));

        using (var doc = JsonDocument.Parse(File.ReadAllText(globalJsonPath)))
        {
            var sdk = doc.RootElement.GetProperty("sdk");
            var version = sdk.GetProperty("version").GetString();
            Assert.NotNull(version);
            Assert.StartsWith("10.0.", version, StringComparison.Ordinal);
            Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
        }

        var propsDoc = XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));
        Assert.Equal("14.0", GetSingleProperty(propsDoc, "LangVersion"));
        Assert.Equal("enable", GetSingleProperty(propsDoc, "Nullable"));
        Assert.Equal("true", GetSingleProperty(propsDoc, "TreatWarningsAsErrors"));
        Assert.Empty(propsDoc.Descendants("NoWarn"));

        var allProjects = Directory.GetFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !IsBuildOutputPath(path))
            .ToArray();

        Assert.Equal(10, allProjects.Length);

        foreach (var csproj in allProjects)
        {
            var xml = XDocument.Load(csproj);
            var tfm = GetSingleProperty(xml, "TargetFramework");
            Assert.True(
                tfm is "net10.0" or "net10.0-windows",
                $"Project '{Path.GetFileName(csproj)}' uses disallowed TFM '{tfm}'.");
            Assert.Empty(xml.Descendants("TargetFrameworks"));
            Assert.Empty(xml.Descendants("NoWarn"));
        }
    }

    [Fact]
    public void SourceAndXaml_AvoidExperimentalWpfThemeModeApisAndPragmaWarningBypasses()
    {
        var srcDir = Path.Combine(RepoRoot, "src");
        var codeFiles = Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p) &&
                               (p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.NotEmpty(codeFiles);

        foreach (var file in codeFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("ThemeMode=", content, StringComparison.Ordinal);
            Assert.DoesNotContain(".ThemeMode", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Fluent.xaml", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#pragma warning disable", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ModuleBoundaries_EnforceUnidirectionalGraphHeadlessIsolationAndNoPrematureModules()
    {
        var actualSrcProjects = Directory.GetDirectories(Path.Combine(RepoRoot, "src"))
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrEmpty(name))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        // Scope guard: no premature agent, gateway, voice, memory, execution, or virtual HQ projects in M1.
        Assert.Equal(ExpectedMilestone1SourceProjects, actualSrcProjects);

        AssertProjectReferences("src/Aeron.Contracts/Aeron.Contracts.csproj", []);
        Assert.Empty(GetPackageReferences("src/Aeron.Contracts/Aeron.Contracts.csproj"));

        AssertProjectReferences("src/Aeron.Security/Aeron.Security.csproj", ["Aeron.Contracts"]);
        Xunit.Assert.Empty(GetPackageReferences("src/Aeron.Security/Aeron.Security.csproj"));

        AssertProjectReferences(
            "src/Aeron.Infrastructure/Aeron.Infrastructure.csproj",
            ["Aeron.Contracts", "Aeron.Security"]);

        AssertProjectReferences(
            "src/Aeron.Core/Aeron.Core.csproj",
            ["Aeron.Contracts", "Aeron.Infrastructure", "Aeron.Security"]);

        // M2 moved the Owner security surface into the shell, so the WPF host references
        // the kernel assembly directly. The graph stays acyclic:
        // Desktop -> Core -> {Security, Infrastructure} and Desktop -> Security.
        AssertProjectReferences(
            "src/Aeron.Desktop/Aeron.Desktop.csproj",
            ["Aeron.Contracts", "Aeron.Core", "Aeron.Security"]);

        // Headless modules must have zero WPF coupling.
        string[] headlessProjects =
        [
            "src/Aeron.Contracts/Aeron.Contracts.csproj",
            "src/Aeron.Security/Aeron.Security.csproj",
            "src/Aeron.Infrastructure/Aeron.Infrastructure.csproj",
            "src/Aeron.Core/Aeron.Core.csproj"
        ];

        foreach (var relPath in headlessProjects)
        {
            var xml = XDocument.Load(Path.Combine(RepoRoot, relPath));
            Assert.Equal("net10.0", GetSingleProperty(xml, "TargetFramework"));
            Assert.Empty(xml.Descendants("UseWPF"));
        }

        // Aeron.Desktop must have zero direct SQLite reference and zero localhost/TCP/HTTP server or client code in M1.
        Assert.DoesNotContain(
            "Microsoft.Data.Sqlite",
            GetPackageReferences("src/Aeron.Desktop/Aeron.Desktop.csproj"),
            StringComparer.OrdinalIgnoreCase);

        var desktopCsFiles = Directory.GetFiles(Path.Combine(RepoRoot, "src", "Aeron.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p));

        foreach (var file in desktopCsFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Microsoft.Data.Sqlite", text, StringComparison.Ordinal);
            Assert.DoesNotContain("TcpListener", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpListener", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net.Sockets", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DependenciesAndAssets_ContainZeroForbiddenAgentLocalLlmOrModelWeightArtifacts()
    {
        var packageFiles = Directory.GetFiles(RepoRoot, "*.*", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p) &&
                               (p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)));

        foreach (var file in packageFiles)
        {
            var xml = XDocument.Load(file);
            var packages = xml.Descendants()
                .Where(static e => e.Name.LocalName is "PackageReference" or "PackageVersion")
                .Select(static e => e.Attribute("Include")?.Value ?? string.Empty);

            foreach (var pkg in packages)
            {
                foreach (var forbidden in ForbiddenPackageSubstrings)
                {
                    Assert.DoesNotContain(forbidden, pkg, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        var allRepoFiles = Directory.GetFiles(RepoRoot, "*.*", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p) && !IsGitInternalPath(p));

        foreach (var file in allRepoFiles)
        {
            var ext = Path.GetExtension(file);
            Assert.DoesNotContain(ext, ForbiddenModelWeightExtensions, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SecurityKernel_KeepsAcyclicDependenciesAndNoForbiddenRuntimeDependencies()
    {
        // The M2 kernel added no project references and no packages: Contracts has none,
        // Security references Contracts only, Infrastructure references Contracts+Security.
        AssertProjectReferences("src/Aeron.Security/Aeron.Security.csproj", ["Aeron.Contracts"]);
        Xunit.Assert.Empty(GetPackageReferences("src/Aeron.Security/Aeron.Security.csproj"));

        // No platform-specific Windows API package was introduced: DPAPI and the account
        // source use built-in runtime interop types only.
        var packageFiles = Directory.GetFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p))
            .ToArray();

        foreach (var file in packageFiles)
        {
            foreach (var package in GetPackageReferences(Path.GetRelativePath(RepoRoot, file).Replace('\\', '/')))
            {
                Assert.DoesNotContain("Windows.Security", package, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("CredentialManagement", package, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("IdentityModel", package, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Cryptography", package, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Headless security code must not gain WPF, WinForms, or network server coupling.
        var securityFiles = Directory.GetFiles(Path.Combine(RepoRoot, "src", "Aeron.Security"), "*.cs", SearchOption.AllDirectories)
            .Where(static p => !IsBuildOutputPath(p));

        foreach (var file in securityFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("System.Windows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpListener", text, StringComparison.Ordinal);
            Assert.DoesNotContain("TcpListener", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", text, StringComparison.Ordinal);

            // AERON.Security stays platform-neutral: Windows interop (DPAPI, account
            // observation, ACLs) belongs to Aeron.Infrastructure, which composes it.
            Assert.DoesNotContain("DllImport", text, StringComparison.Ordinal);
            Assert.DoesNotContain("LibraryImport", text, StringComparison.Ordinal);
            Assert.DoesNotContain("RegisterWindowsAccount", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Milestone2Documentation_RecordsTheOwnerDecisionRegisterAndRuntimeStatus()
    {
        string[] requiredM2Docs =
        [
            "docs/specifications/M2/M2-DECISION-REGISTER.md",
            "docs/milestones/M2-BUILD-PROGRESS.md",
            "docs/specifications/M2/SECURITY-ARCHITECTURE.md",
            "docs/specifications/M2/M2-ACCEPTANCE-CRITERIA.md"
        ];

        foreach (var doc in requiredM2Docs)
        {
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, doc.Replace('/', Path.DirectorySeparatorChar))),
                $"Required M2 governance artifact missing: {doc}");
        }

        var register = File.ReadAllText(Path.Combine(RepoRoot, "docs", "specifications", "M2", "M2-DECISION-REGISTER.md"));
        foreach (var entry in new[] { "D2-01", "D2-02", "D2-03", "D2-04", "D2-05", "D2-06", "D2-07", "D2-08", "D2-09", "D2-10" })
        {
            Assert.Contains(entry, register, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GovernanceAndDocumentation_PreserveBaselineSeparateU8RecordsAndWindows11Version25H2()
    {
        // Preserved original README line
        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));
        Assert.Contains("# aeron-core", readme, StringComparison.Ordinal);
        Assert.Contains(
            "AERON — An intelligent, multi-agent personal AI assistant for Windows, built with privacy, autonomy and adaptive intelligence.",
            readme,
            StringComparison.Ordinal);

        // Proprietary Avenox Studio designation
        var license = File.ReadAllText(Path.Combine(RepoRoot, "LICENSE"));
        Assert.Contains("Avenox Studio", license, StringComparison.Ordinal);
        Assert.Contains("All Rights Reserved", license, StringComparison.Ordinal);

        // Mandatory governance, blueprint, and separated verification documents
        string[] requiredDocs =
        [
            "docs/architecture/BLUEPRINT.md",
            "docs/governance/CONSTRAINTS.md",
            "docs/governance/DECISION_LEDGER.md",
            "docs/governance/U-01-TO-U-07.md",
            "docs/governance/U-08-ORIGINAL.md",
            "docs/governance/U-08R-REPLACEMENT.md",
            "docs/governance/ADR-AUTH-001.md",
            "docs/governance/ADR-AUTH-002.md",
            "docs/governance/ADR-AUTH-003.md",
            "docs/governance/ADR-TECH-001-DOTNET10-WPF.md",
            "docs/milestones/M1-FOUNDATION.md",
            "docs/milestones/M1-APEX-VERIFICATION.md",
            ".github/workflows/ci-windows.yml"
        ];

        foreach (var docRel in requiredDocs)
        {
            var fullPath = Path.Combine(RepoRoot, docRel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Required governance/milestone artifact missing: {docRel}");
        }

        // U-8 original and U-8R replacement must remain strictly separate, distinct records.
        var u8Original = File.ReadAllText(Path.Combine(RepoRoot, "docs", "governance", "U-08-ORIGINAL.md"));
        var u8Replacement = File.ReadAllText(Path.Combine(RepoRoot, "docs", "governance", "U-08R-REPLACEMENT.md"));
        Assert.NotEqual(u8Original, u8Replacement);
        Assert.Contains("UNRESOLVED", u8Original, StringComparison.Ordinal);
        Assert.Contains("UNRESOLVED", u8Replacement, StringComparison.Ordinal);

        // Technical ADR must specify .NET 10 LTS, WPF, and Windows 11 25H2 (26200) baseline.
        var techAdr = File.ReadAllText(Path.Combine(RepoRoot, "docs", "governance", "ADR-TECH-001-DOTNET10-WPF.md"));
        Assert.Contains(".NET 10 LTS", techAdr, StringComparison.Ordinal);
        Assert.Contains("Windows 11 Version 25H2", techAdr, StringComparison.Ordinal);
        Assert.Contains("26200", techAdr, StringComparison.Ordinal);
    }

    private static void AssertProjectReferences(string relativeCsprojPath, string[] expectedProjectNames)
    {
        var xml = XDocument.Load(Path.Combine(RepoRoot, relativeCsprojPath.Replace('/', Path.DirectorySeparatorChar)));
        var actual = xml.Descendants("ProjectReference")
            .Select(static e => Path.GetFileNameWithoutExtension(e.Attribute("Include")?.Value ?? string.Empty))
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToArray();

        var expectedSorted = expectedProjectNames.OrderBy(static n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedSorted, actual);
    }

    private static string[] GetPackageReferences(string relativeCsprojPath)
    {
        var xml = XDocument.Load(Path.Combine(RepoRoot, relativeCsprojPath.Replace('/', Path.DirectorySeparatorChar)));
        return xml.Descendants("PackageReference")
            .Select(static e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
    }

    private static string? GetSingleProperty(XDocument doc, string elementName) =>
        doc.Descendants(elementName).Select(static e => e.Value).FirstOrDefault();

    private static bool IsBuildOutputPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{sep}artifacts{sep}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{sep}TestResults{sep}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGitInternalPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}.git{sep}", StringComparison.OrdinalIgnoreCase);
    }

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Aeron.sln")) &&
                File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate AERON repository root from test base directory.");
    }
}
