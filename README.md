# aeron-core
AERON — An intelligent, multi-agent personal AI assistant for Windows, built with privacy, autonomy and adaptive intelligence.

> **Proprietary Notice:** Copyright (c) 2026 Avenox Studio. All Rights Reserved. See [`LICENSE`](LICENSE).

---

## Project Status

- **Current Milestone:** Milestone 1 — Native Windows Application Foundation
- **Technology Stack:** C# 14 · .NET 10 LTS · WPF (`win-x64` native Windows executable)
- **Minimum Supported OS:** Windows 11 Version 25H2 (Build 26200, `x64`) or newer
- **Reference Development Host (`APEX`):** Windows 11 (`x64`), Intel Core i5 (13th Gen), 16 GB RAM, Intel Integrated Graphics

Milestone 1 intentionally delivers only the verified native Windows desktop foundation: modular solution architecture, application lifecycle management, single-instance coordination, native Win32 system tray integration, secure user-scoped configuration, unconditional secret-redacting logging, SQLite WAL persistence with checksum-verified migrations, initial governance records, automated tests, and Windows GitHub Actions CI.

Foundation adoption: the Sir Thaddeus (Apache-2.0) audit, compatibility verdicts, and proposed selective-adoption decision are recorded in [`docs/foundation/`](docs/foundation/SIR-THADDEUS-ADOPTION-AUDIT.md), with attribution in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). No upstream code has been imported.

Model gateway integration (OmniRoute), full permission/approval enforcement, contextual memory, multi-agent execution, voice interaction, realistic avatars, and the interactive virtual headquarters are deferred to subsequent milestones per [`docs/architecture/BLUEPRINT.md`](docs/architecture/BLUEPRINT.md).

---

## Solution Structure

```text
Aeron.sln
├── src/
│   ├── Aeron.Contracts/          # Zero-dependency contracts, results, config models, and interfaces (net10.0)
│   ├── Aeron.Security/           # Secret redaction filter and fail-closed config policy validator (net10.0)
│   ├── Aeron.Infrastructure/     # Isolated appdata paths, Windows ACLs, secure config store,
│   │                             # redacting JSONL logger, SQLite WAL + migration runner (net10.0)
│   ├── Aeron.Core/               # Lifecycle state machine, root cancellation, typed event bus,
│   │                             # and verified foundation health service (net10.0)
│   └── Aeron.Desktop/            # Native WPF executable, single-instance guard, Win32 system tray,
│                                 # non-experimental dark theme, and MVVM foundation shell (net10.0-windows)
├── tests/
│   ├── Aeron.Security.Tests/         # Secret redaction and config invariant tests (net10.0)
│   ├── Aeron.Infrastructure.Tests/   # Paths, ACLs, config persistence, logger rotation, SQLite tests (net10.0)
│   ├── Aeron.Core.Tests/             # Lifecycle transitions, rollback, cancellation, event bus tests (net10.0)
│   ├── Aeron.Desktop.Tests/          # Single-instance IPC/ACL, close-to-tray, and ViewModel tests (net10.0-windows)
│   └── Aeron.Architecture.Tests/     # TFM, dependency boundary, warning-policy, and scope guard tests (net10.0)
├── docs/
│   ├── architecture/BLUEPRINT.md
│   ├── governance/               # Constraints, Decision Ledger, U-1..U-7, separate U-8 & U-8R, ADRs
│   └── milestones/               # M1 scope & separated CI vs. interactive APEX verification runbook
└── scripts/
    ├── verify-repo-hygiene.ps1   # Automated repository hygiene, secret, and policy check
    └── measure-apex-m1.ps1       # Read-only Windows resource measurement helper for APEX verification
```

---

## Build, Test & Publish (Windows / .NET 10 LTS SDK)

Requires the **.NET 10 LTS SDK** (`10.0.100` or newer `10.0.x` feature band, pinned in [`global.json`](global.json)).

```powershell
# Verify repository hygiene and policy constraints
pwsh -NoProfile -File scripts/verify-repo-hygiene.ps1

# Restore, build (Release, warnings-as-errors), and execute automated tests
dotnet restore Aeron.sln
dotnet build Aeron.sln -c Release --no-restore -warnaserror
dotnet test Aeron.sln -c Release --no-build --verbosity normal

# Publish native Windows x64 executable (framework-dependent)
dotnet publish src/Aeron.Desktop/Aeron.Desktop.csproj -c Release -r win-x64 --self-contained false -warnaserror -o artifacts/publish/win-x64
```

Launching the framework-dependent `Aeron.Desktop.exe` requires the **.NET 10 LTS Windows Desktop Runtime (`Microsoft.WindowsDesktop.App 10.0.x`, `x64`)** on Windows 11 25H2+ (included automatically with the .NET 10 LTS SDK; install `Microsoft.DotNet.DesktopRuntime.10` `x64` when running the standalone published artifact without the SDK).

Runtime application files are stored exclusively under `%LOCALAPPDATA%\Aeron` (`config`, `data`, `data\backups`, `logs`) with current-user Windows ACL protection—never inside the repository, cloud-synced `Documents`, or OmniRoute directories.

---

## Non-Negotiable Core Principles

- **₹0 Budget & Fail-Closed Routing:** Remote-only inference with zero paid fallback and zero local LLM runtimes or model weight downloads.
- **OmniRoute Preservation:** Existing OmniRoute installation, configuration, and data remain untouched.
- **Sovereign Architecture:** Custom AERON engines with zero third-party agent frameworks and zero v1 skill-script execution.
- **Project Owner Authority:** Ultimate Owner control over consequential actions, external disclosures, external communications, and external account modifications, backed by unconditional secret redaction.

For interactive Windows 11 25H2+ verification and empirical resource measurement on `APEX`, follow [`docs/milestones/M1-APEX-VERIFICATION.md`](docs/milestones/M1-APEX-VERIFICATION.md).
