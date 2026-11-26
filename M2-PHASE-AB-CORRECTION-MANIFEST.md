# M2 Phase A/B correction patch manifest

- Patch: `AERON-M2-PHASE-AB-CORRECTION.patch`
- Exact base: `38724834f75dd65ae59a7bf922d48e5b0ed78890`
- Size: **54,704 bytes**
- SHA-256: **57c9667e01f966d51eb43003471470337afd038e37e95f06ae03e544e47ead43**
- Generated using a temporary Git index; the working index was not staged or changed. Applied to a fresh detached worktree at that exact base using `git apply --check --whitespace=error`, then `git apply --whitespace=error`; `git diff --check` passed. The separate temporary worktree was removed. No code was compiled or tested by this check.
- The prior Phase A patch and all specification patches remain unchanged. The previous Phase A Windows instructions were marked historical in the working tree but are not included in this correction patch.

## Exact patch inventory

```text
M2-PHASE-AB-CORRECTION-REVIEW.md
docs/governance/U-08R-REPLACEMENT.md
docs/milestones/M2-BUILD-PROGRESS.md
src/Aeron.Contracts/AuditLedgerContracts.cs
src/Aeron.Contracts/WindowsIdentityContracts.cs
src/Aeron.Security/AuditLedger.cs
src/Aeron.Security/AuthorityState.cs
src/Aeron.Security/OperationBinding.cs
src/Aeron.Security/OwnerApprovalService.cs
src/Aeron.Security/WindowsOwnerIdentityProvider.cs
tests/Aeron.Security.Tests/AuditLedgerTests.cs
tests/Aeron.Security.Tests/AuthorityStateTests.cs
tests/Aeron.Security.Tests/OperationBindingTests.cs
tests/Aeron.Security.Tests/OwnerApprovalServiceTests.cs
tests/Aeron.Security.Tests/WindowsOwnerIdentityProviderTests.cs
```

The patch does not contain the prior patch artifact, specification patches, unrelated implementation or OmniRoute. The manifest itself is external to the patch, avoiding a self-referential hash.

## Isolated Windows validation

In PowerShell, copy the patch outside the existing repository, set `$Patch` to that copy, and run the commands below. A fresh clone in a unique temporary directory prevents touching the Owner's dirty checkout. The logs can contain environment information and should be reviewed before sharing.

```powershell
$ErrorActionPreference = 'Stop'
$Patch = 'C:\path\to\AERON-M2-PHASE-AB-CORRECTION.patch' # change this path
$Base = '38724834f75dd65ae59a7bf922d48e5b0ed78890'
if (-not (Test-Path -LiteralPath $Patch -PathType Leaf)) { throw 'Patch not found.' }
$Patch = (Resolve-Path -LiteralPath $Patch).Path
if ((Get-Item -LiteralPath $Patch).Length -ne 54704 -or (Get-FileHash -LiteralPath $Patch -Algorithm SHA256).Hash -ne '57c9667e01f966d51eb43003471470337afd038e37e95f06ae03e544e47ead43') { throw 'Patch mismatch.' }
$Run = Join-Path $env:TEMP ('aeron-m2-correction-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Run | Out-Null
$Repo = Join-Path $Run 'checkout'; $Logs = Join-Path $Run 'logs'
New-Item -ItemType Directory -Path $Logs | Out-Null
Write-Host "Validation directory: $Run"
git clone --no-checkout https://github.com/studioavenox-ux/aeron-core.git $Repo
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location $Repo
git checkout --detach $Base
if ($LASTEXITCODE -ne 0 -or (git rev-parse HEAD).Trim() -ne $Base) { throw 'Wrong base or checkout failure.' }
git apply --check --whitespace=error $Patch
if ($LASTEXITCODE -ne 0) { throw 'Patch check failed.' }
git apply --whitespace=error $Patch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git status --short | Tee-Object -FilePath (Join-Path $Logs 'inventory.txt')
function Run-Dotnet([string]$Log, [string[]]$ArgsForDotnet) {
    & dotnet @ArgsForDotnet 2>&1 | Tee-Object -FilePath (Join-Path $Logs $Log)
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE); see $Log" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    'BLOCKER: .NET 10 SDK unavailable on Windows (dotnet not found).' | Tee-Object -FilePath (Join-Path $Logs 'blocker.txt')
    throw 'BLOCKER: .NET 10 SDK unavailable on Windows.'
}
Run-Dotnet '01-info.txt' @('--info')
$Sdk = (& dotnet --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $Sdk -notmatch '^10\.') {
    "BLOCKER: .NET 10 SDK unavailable on Windows or incompatible with global.json: $Sdk" | Tee-Object -FilePath (Join-Path $Logs 'blocker.txt')
    throw 'BLOCKER: .NET 10 SDK unavailable on Windows or incompatible with global.json.'
}
$Sdk | Set-Content (Join-Path $Logs 'sdk-version.txt')
Run-Dotnet '02-restore.txt' @('restore', 'Aeron.sln')
Run-Dotnet '03-tests.txt' @('test', 'Aeron.sln', '-c', 'Release', '--no-restore', '--verbosity', 'normal')
Run-Dotnet '04-desktop-build.txt' @('build', 'src/Aeron.Desktop/Aeron.Desktop.csproj', '-c', 'Release', '-p:EnableWindowsTargeting=true', '--no-restore')
Write-Host "Review test counts and retain logs in $Logs"
```

Report actual exit codes and test counts. Previous PR CI is not evidence for this patch; a successful application is not a successful build or test.
