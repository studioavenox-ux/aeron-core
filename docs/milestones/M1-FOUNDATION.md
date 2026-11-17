# Milestone 1 — Native Windows Application Foundation

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

## 1. Authorized Scope (`CONFIRMED`)

- Modular C# 14 + .NET 10 LTS + WPF solution (`Aeron.sln`) with warnings-as-errors and strict TFM enforcement
- Application lifecycle state machine (`Uninitialized → Starting → Running → Stopping → Stopped` / `Faulted`), LIFO shutdown hooks, and root emergency cancellation
- Current-user single-instance coordination (`Mutex` with abandoned-mutex recovery + current-user-SID ACL `NamedPipe` activation channel, zero TCP/localhost sockets)
- Native Win32 system tray service (`Shell_NotifyIconW`) with Close (`X`) → Minimize to Tray default (`Window.CloseToTray = true`), double-click restore, context menu (*Open AERON*, *Minimize to Tray*, *Exit AERON*), Explorer-restart recovery, and idempotent icon cleanup
- Secure typed application configuration in `%LOCALAPPDATA%\Aeron\config\aeron.config.json` with atomic writes, current-user Windows ACLs, inline-secret rejection, and locked policy invariants (including Windows 11 Version 25H2 Build 26200+ minimum baseline)
- Unconditionally redacting structured JSONL logger (`%LOCALAPPDATA%\Aeron\logs\aeron.jsonl`) with size-based file rotation and nested exception scrubbing
- SQLite persistence (`%LOCALAPPDATA%\Aeron\data\aeron_core.db`) with `WAL` journal mode, `foreign_keys=ON`, SHA-256 checksum-verified transactional migrations, pre-upgrade backups, and redacted lifecycle journaling
- Initial architecture and governance documentation (preserving original repo content, keeping original `U-8` and replacement `U-8R` separate, and preserving OmniRoute untouched)
- Automated test suites across 5 test projects and Windows GitHub Actions CI (`.github/workflows/ci-windows.yml`)

## 2. Explicit Milestone 1 Non-Goals

No model/OmniRoute integration, no M2 approval/vault engine, no contextual memory engine, no multi-agent engine, no tool execution or skill scripts, no voice subsystem, and no avatars or virtual headquarters.

## 3. Part A — Automated CI Acceptance Matrix (`windows-latest`)

| ID | Criterion | Verification Target |
|---|---|---|
| **AT-M1-CI-01** | Strict C# 14 + .NET 10 LTS Release build & `win-x64` publish with 0 warnings/errors and zero experimental `WPF0001` theming usage | `ci-windows.yml` + `ArchitectureAndGovernanceTests` |
| **AT-M1-CI-02** | Modular unidirectional dependencies, zero WPF in headless assemblies, zero direct SQLite or sockets in `Aeron.Desktop`, zero banned packages/weights/premature modules | `ArchitectureAndGovernanceTests` |
| **AT-M1-CI-03** | Deterministic lifecycle transitions, invalid-transition rejection, startup rollback to `Faulted`, LIFO hooks, and prompt root cancellation | `LifecycleAndEventBusTests` |
| **AT-M1-CI-04** | Headless single-instance `Mutex` contention, abandoned-mutex recovery, current-user-SID `NamedPipe` ACL, and `ACTIVATE` delivery | `SingleInstanceManagerTests` |
| **AT-M1-CI-05** | Headless ViewModel close-to-tray (`CloseToTray=true`), restore, secondary activation, configurable close preference, idempotent tray disposal, and Windows 11 25H2 (`26200`) baseline evaluation | `MainWindowViewModelAndTrayStateTests` + `RedactingLoggerAndOsBaselineTests` |
| **AT-M1-CI-06** | Isolated appdata creation, `Documents`-folder rejection, Windows current-user ACL enforcement, atomic config persistence, and fail-closed rejection of tampered invariants, inline secrets, and malformed JSON | `AppPathsAndConfigTests` + `ConfigPolicyValidatorTests` |
| **AT-M1-CI-07** | Zero raw secret leakage across log messages, structured properties, and nested exceptions in JSONL output, plus size-based log rotation | `RedactionFilterTests` + `RedactingLoggerAndOsBaselineTests` |
| **AT-M1-CI-08** | SQLite `WAL` + `foreign_keys=ON`, `001_foundation.sql` checksum storage, idempotent restart, transactional rollback on bad SQL, pre-upgrade backup, and tamper detection | `SqlitePersistenceTests` |
| **AT-M1-CI-09** | Preserved baseline content, proprietary notice, separated `U-8`/`U-8R` docs, zero committed secrets/artifacts, and clean PR to `main` | `scripts/verify-repo-hygiene.ps1` + `ArchitectureAndGovernanceTests` |

> **Gate Separation:** Passing Part A Automated CI qualifies the Pull Request for code review. Full Milestone 1 completion additionally requires executing Part B Interactive Verification on Windows in [`M1-APEX-VERIFICATION.md`](M1-APEX-VERIFICATION.md).
