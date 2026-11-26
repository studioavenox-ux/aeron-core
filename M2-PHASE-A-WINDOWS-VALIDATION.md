# M2 Phase A — isolated Windows validation

> Historical instructions for the previous patch, preserved as-is below. PR #4 CI has since passed; its results do not validate this earlier patch or later corrections. See `M2-PHASE-AB-CORRECTION-REVIEW.md` for the current status.

**PHASE A — IMPLEMENTED, BUILD/TEST VALIDATION PENDING** (status when this document was originally written)

Owner confirmed v1.1.1 as the implementation baseline and granted M2 BUILD; the U-8R implementation gate is satisfied for this build. Historical original U-8 recovery remains unresolved; original file byte hashes are unavailable. The recovered, Owner-confirmed U-8R selections are unchanged. No original signature or independent source verification is asserted. Earlier pack statements saying BUILD is blocked predate this authorization.

## Patch verification in this environment

- Exact base commit: `24d0804b445b429909dd16068da7e00cccc603a0` (commit object fetched and verified).
- `AERON-M2-PHASE-A-WINDOWS-VALIDATION.patch`: **59,371 bytes**, SHA-256 **589febe4ab13290885a793895997dab0700ed42c41a35aa948fedcffd5b379f4**.
- Generated against that base from a temporary index populated with the current source/test tree, including untracked files, without staging the real index. Only `src/` and `tests/` paths were included; the inventory below was checked for unrelated paths.
- Applied successfully to a separate detached worktree at the exact base using `git apply --check --whitespace=error` followed by `git apply --whitespace=error`. `git diff --check` also passed there. The temporary checkout was removed; existing working-tree changes and artifacts were preserved.
- No .NET SDK is available in this Linux environment. Patch application is not compilation or execution; none of the tests below is claimed to have run.

## Windows procedure

Use a fresh PowerShell session, Git, network access to the repository and NuGet, and the .NET 10 SDK compatible with `global.json` (10.0.100, latest feature roll-forward). Download/copy the patch to a location **outside** the existing repository. Set `$Patch` to that existing file. This procedure creates a uniquely named directory in the user's temporary directory and never uses the Owner's existing dirty checkout. Logs may contain environment or failure information; inspect them before sharing.

```powershell
$ErrorActionPreference = 'Stop'
$Patch = 'C:\path\to\AERON-M2-PHASE-A-WINDOWS-VALIDATION.patch' # change to the copied patch
$Base = '24d0804b445b429909dd16068da7e00cccc603a0'
$ExpectedHash = '589febe4ab13290885a793895997dab0700ed42c41a35aa948fedcffd5b379f4'
if (-not (Test-Path -LiteralPath $Patch -PathType Leaf)) { throw 'Patch file not found.' }
$Patch = (Resolve-Path -LiteralPath $Patch).Path
if ((Get-Item -LiteralPath $Patch).Length -ne 59371 -or (Get-FileHash -LiteralPath $Patch -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'Patch size or SHA-256 mismatch.' }
$Run = Join-Path $env:TEMP ('aeron-m2-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Run | Out-Null
$Repo = Join-Path $Run 'checkout'
$Logs = Join-Path $Run 'logs'
New-Item -ItemType Directory -Path $Logs | Out-Null
Write-Host "Validation directory: $Run"

git clone --no-checkout https://github.com/studioavenox-ux/aeron-core.git $Repo
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location $Repo
git checkout --detach $Base
if ($LASTEXITCODE -ne 0) { throw 'Base checkout failed.' }
if ((git rev-parse HEAD).Trim() -ne $Base) { throw 'Wrong base commit.' }
git apply --check --whitespace=error $Patch
if ($LASTEXITCODE -ne 0) { throw 'Patch check failed.' }
git apply --whitespace=error $Patch
if ($LASTEXITCODE -ne 0) { throw 'Patch apply failed.' }
git status --short | Tee-Object -FilePath (Join-Path $Logs 'patch-inventory.txt')

# Tee each native command to its own log, and stop on the native exit code.
function Invoke-DotnetLogged([string]$Log, [string[]]$DotnetArgs) {
    & dotnet @DotnetArgs 2>&1 | Tee-Object -FilePath (Join-Path $Logs $Log)
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "dotnet $($DotnetArgs -join ' ') failed with exit code $code; see $Log" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    'BLOCKER: .NET 10 SDK unavailable on Windows (dotnet not found).' | Tee-Object -FilePath (Join-Path $Logs 'blocker.txt')
    throw 'BLOCKER: .NET 10 SDK unavailable on Windows.'
}
Invoke-DotnetLogged '01-dotnet-info.txt' @('--info')
# Run inside the checkout so global.json selects the SDK.
$SdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $SdkVersion -notmatch '^10\.') {
    "BLOCKER: .NET 10 SDK unavailable on Windows or incompatible with global.json. dotnet --version: $SdkVersion" | Tee-Object -FilePath (Join-Path $Logs 'blocker.txt')
    throw 'BLOCKER: .NET 10 SDK unavailable on Windows or incompatible with global.json.'
}
$SdkVersion | Set-Content -Path (Join-Path $Logs 'sdk-version.txt')
Invoke-DotnetLogged '02-restore.txt' @('restore', 'Aeron.sln')
Invoke-DotnetLogged '03-tests.txt' @('test', 'Aeron.sln', '-c', 'Release', '--no-restore', '--verbosity', 'normal')
Invoke-DotnetLogged '04-desktop-build.txt' @('build', 'src/Aeron.Desktop/Aeron.Desktop.csproj', '-c', 'Release', '-p:EnableWindowsTargeting=true', '--no-restore')
Write-Host "Commands completed; inspect test counts and failures in $Logs"
```

The required commands in order are `dotnet --info`, `dotnet restore Aeron.sln`, `dotnet test Aeron.sln -c Release --no-restore --verbosity normal`, and `dotnet build src/Aeron.Desktop/Aeron.Desktop.csproj -c Release -p:EnableWindowsTargeting=true --no-restore`. If restore, test, or build fails, retain the logs, exact exit code and error; do not claim success. Diagnose and rerun in a fresh isolated checkout if needed. Report actual executed test counts and explain existing test failures separately. If .NET 10 SDK is unavailable on Windows, report that exact blocker. Do not delete the validation directory until logs have been retained.

## Implementation inventory and review

All following paths are the complete patch inventory. The implementation is reviewed statically, not tested here.

- `src/Aeron.Contracts/SecurityPolicy.cs`: actions, sensitivities, identity/tier/domain/disposition/reason contracts; trusted identity and grants abstractions, request and decision. Identity providers must authenticate out of band, including MFA/device trust; there is no production implementation. Request classification must be trusted, never supplied by an agent as authority.
- `src/Aeron.Contracts/ApprovalContracts.cs`: approval lifecycle, minimal audit event and sink, and result contracts. Audit durability is a provider obligation, not implemented here.
- `src/Aeron.Core/FoundationHost.cs`: exposes a policy constructed with null identity and grants; denies rather than inventing identities. It does not register approval or action dispatch.
- `src/Aeron.Infrastructure/RedactingLogger.cs`: scrubs the redaction marker for sensitive structured values. It cannot guarantee unknown secrets or prevent concurrent registration races.
- `src/Aeron.Security/RedactionFilter.cs`: registered short/marker-overlapping secrets suppress fields; pattern timeouts scrub the marker. Trimming, pattern matching, short-secret over-suppression, schema text and unknown secrets limit guarantees. Registration and logging have no global barrier.
- `src/Aeron.Security/OwnerAuthorityGuard.cs`: verifies injected provider output, rejects empty evidence, missing/throwing provider, bad subject or kind; no MFA or OS trust implemented.
- `src/Aeron.Security/PolicyDecisionEngine.cs`: validates known action and sensitivity and exact policy version; default mapping denies unrecognized actions. Explicit action-to-tier/domain mapping, grant check, Owner-only authority changes and approval requirements are conservative engineering interpretations; especially **G2/G3 and L0–L3 are proposed mappings**, not historically established meanings. Secrets, scripts, unvalidated local paths and unverified inference gateway fail closed. Only routine application-state reads can currently allow with trusted identity/grant. Policy evaluation alone is not dispatch.
- `src/Aeron.Security/OperationBinding.cs`: per-instance random HMAC-SHA-256 key, versioned length framing, strict UTF-8, constant-time digest verification; binds ID, action, subject, target, policy version and exact parameter bytes. Key is held in process memory, synchronized and zeroed on disposal; copies/internal crypto state or process compromise are not protected, and there is no durable key or restart recovery. Identity kind and sensitivity are additionally checked in approval state, not in the digest.
- `src/Aeron.Security/OwnerApprovalService.cs`: volatile bounded approvals, trusted injected identity/grants/clock/audit, Owner decisions, expiry, cancel/revoke, and single-use consume. Consumption rechecks policy, full actor identity (including kind), request and binding. A lock serializes in-process consume/revoke; whichever enters first may win. It does not synchronize independent grant revocation or an external action, survive restart, implement protected UI/MFA or authorize side effects. An audit append failure latches unavailability; authenticated revoke/cancel still invalidates in memory even on audit failure. Providers are trusted synchronous callbacks invoked under the lock; reentrancy and blocking behavior need further hardening before production. A backwards clock can delay expiration before first observation.
- `tests/Aeron.Core.Tests/SecurityCompositionTests.cs`: host missing-provider denial.
- `tests/Aeron.Infrastructure.Tests/ShortSecretLoggerTests.cs`: structured fields, exception and marker redaction.
- `tests/Aeron.Security.Tests/OperationBindingTests.cs`: per-field tampering, framing, key separation, invalid Unicode and disposal.
- `tests/Aeron.Security.Tests/OwnerApprovalServiceTests.cs`: identity, approval, actor/target/payload binding, expiry, grant withdrawal, audit failure, replay, identity-kind change, concurrent consumes and consume/revoke. Concurrency tests cover in-process results, not external dispatch or independent store races.
- `tests/Aeron.Security.Tests/SecurityPolicyTests.cs`: invalid/unknown classifications and version, identity failures, missing/failed grants, Owner authority, approval, secret/script/gateway/path denial, proposed G2/G3 mapping.
- `tests/Aeron.Security.Tests/ShortSecretRedactionTests.cs`: short values, marker collisions and long-value redaction.

## Integration and completion boundary

A future trusted action-dispatch boundary must classify the *actual* action/data and resolve the actual target, then revalidate authenticated identity, grants, current policy version, exact operation bytes, approval status, revocation and audit availability immediately at dispatch. It must coordinate these checks with revocation and execution so changes cannot race them; define failure, crash and audit semantics before enabling side effects. A consumed approval is **not** itself authorization for any production action. No executor is currently wired.

Phase A is complete only after actual compilation, new tests and existing tests run successfully (or existing failures are explained), and default-deny, identity failure, privilege escalation, operation tampering, replay/concurrency and integration boundaries are verified. Windows validation and review of the above production gaps remain pending; do not infer acceptance from this patch.
