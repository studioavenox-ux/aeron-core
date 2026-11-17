# AERON Milestone 1 — Automated Repository Hygiene, Secret & Policy Check
# Copyright (c) 2026 Avenox Studio. All Rights Reserved.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    $failures = [System.Collections.Generic.List[string]]::new()
    $trackedFiles = @(git ls-files)

    if ($trackedFiles.Count -eq 0) {
        throw "No git-tracked files found in repository."
    }

    $forbiddenExtensions = @(
        '.db', '.db-wal', '.db-shm', '.sqlite', '.sqlite3', '.jsonl',
        '.pfx', '.p12', '.key', '.pem', '.secret', '.secrets',
        '.gguf', '.ggml', '.safetensors', '.onnx', '.pt', '.pth', '.tflite',
        '.exe', '.dll', '.pdb', '.nupkg'
    )

    $secretPatterns = @(
        '-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----',
        '\bsk-[A-Za-z0-9_\-]{12,}\b',
        '\bsk-or-v1-[A-Za-z0-9_\-]{12,}\b',
        '\bghp_[A-Za-z0-9]{16,}\b',
        '\bgithub_pat_[A-Za-z0-9_]{16,}\b',
        '\bAKIA[0-9A-Z]{16}\b',
        '\bAIza[0-9A-Za-z\-_]{20,}\b',
        '\bxox[baprs]-[A-Za-z0-9\-]{12,}\b'
    )

    foreach ($relPath in $trackedFiles) {
        $fileName = [System.IO.Path]::GetFileName($relPath)
        $ext = [System.IO.Path]::GetExtension($relPath).ToLowerInvariant()

        if ($fileName -eq '.env' -or ($fileName.StartsWith('.env.') -and $fileName -ne '.env.example')) {
            $failures.Add("Forbidden environment file tracked: $relPath")
            continue
        }

        if ($fileName -eq 'aeron.config.json') {
            $failures.Add("Runtime configuration file must not be tracked: $relPath")
            continue
        }

        if ($forbiddenExtensions -contains $ext) {
            $failures.Add("Forbidden binary/runtime/model-weight file tracked: $relPath")
            continue
        }

        # Skip scanning this script itself for its own regex literals.
        if ($relPath -eq 'scripts/verify-repo-hygiene.ps1') {
            continue
        }

        if (Test-Path -LiteralPath $relPath -PathType Leaf) {
            $content = Get-Content -LiteralPath $relPath -Raw -ErrorAction SilentlyContinue
            if ($null -ne $content) {
                foreach ($pattern in $secretPatterns) {
                    if ([System.Text.RegularExpressions.Regex]::IsMatch($content, $pattern)) {
                        $failures.Add("Potential secret pattern matched in tracked file: $relPath")
                        break
                    }
                }

                if ($relPath.StartsWith('src/') -and ($ext -eq '.cs' -or $ext -eq '.xaml')) {
                    if ($content.Contains('ThemeMode=') -or $content.Contains('.ThemeMode') -or $content.Contains('Fluent.xaml')) {
                        $failures.Add("Experimental WPF0001 theming API detected in: $relPath")
                    }
                    if ($content.Contains('#pragma warning disable')) {
                        $failures.Add("Disallowed #pragma warning disable detected in: $relPath")
                    }
                }
            }
        }
    }

    $requiredFiles = @(
        'README.md',
        '.gitignore',
        'LICENSE',
        'global.json',
        'Directory.Build.props',
        'Directory.Build.targets',
        'Directory.Packages.props',
        'Aeron.sln',
        'docs/governance/U-08-ORIGINAL.md',
        'docs/governance/U-08R-REPLACEMENT.md',
        'docs/governance/ADR-AUTH-001.md',
        'docs/governance/ADR-AUTH-002.md',
        'docs/governance/ADR-AUTH-003.md',
        'docs/governance/ADR-TECH-001-DOTNET10-WPF.md',
        'docs/milestones/M1-FOUNDATION.md',
        'docs/milestones/M1-APEX-VERIFICATION.md'
    )

    foreach ($req in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $req -PathType Leaf)) {
            $failures.Add("Missing required repository artifact: $req")
        }
    }

    if ($failures.Count -gt 0) {
        Write-Error ("AERON repository hygiene check FAILED:`n - " + ($failures -join "`n - "))
        exit 1
    }

    Write-Host "AERON repository hygiene check PASSED ($($trackedFiles.Count) tracked files inspected, 0 violations)."
}
finally {
    Pop-Location
}
