# AERON — Engineering Architecture Blueprint

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

## 1. Approved Technical Foundation (`CONFIRMED` — 2026-11-17)

| Attribute | Approved Specification |
|---|---|
| **Language** | C# 14 (`LangVersion=14.0`, `Nullable=enable`) |
| **Runtime** | .NET 10 LTS (`global.json` pinned to `10.0.x`; `net10.0` headless modules + `net10.0-windows` desktop shell; end-of-support .NET 8 and STS .NET 9 strictly forbidden) |
| **Desktop UI** | WPF (`WinExe`, `win-x64` native Windows executable; zero browser wrapper, zero localhost server) |
| **WPF Styling & Warnings Policy** | `TreatWarningsAsErrors=true` globally with zero blanket `<NoWarn>` suppressions; standard stable WPF `ResourceDictionary` dark styling (`Themes/FoundationDark.xaml`) in Milestone 1, avoiding experimental `WPF0001` `ThemeMode` APIs |
| **Default Close (`X`) Action** | Minimizes AERON to the Windows system tray (`Window.CloseToTray = true`, Owner-configurable), with explicit *Exit AERON* in the tray menu and main window |
| **Minimum Supported OS Baseline** | **Windows 11 Version 25H2 (Build 26200, `x64`) or newer** |
| **Reference Development Host (`APEX`)** | Windows 11 (`x64`), Intel Core i5 (13th Generation), 16 GB RAM, Intel Integrated Graphics (no discrete GPU) |
| **Local Persistence** | SQLite (`Microsoft.Data.Sqlite`, WAL mode, `foreign_keys=ON`, SHA-256 checksum-verified transactional migrations) isolated under `%LOCALAPPDATA%\Aeron\data` |
| **Designation** | Private repository policy · Proprietary — All Rights Reserved, Avenox Studio |

---

## 2. Measurable Performance Targets (Empirically Verified on `APEX`)

All resource limits are treated as **measurable engineering targets** verified empirically on `APEX` (never assumed from framework selection and never claimed from headless CI):

| Stage | Metric | Target |
|---|---|---|
| **M1 Foundation** | Steady-state idle working set (60s post-launch, `Release` `win-x64`) | **≤ 120 MB** |
| **M1 Foundation** | Steady-state idle CPU (60s, no user input) | **< 1.0%** |
| **M1 Foundation** | Idle Intel integrated GPU utilization | **~0% static window / 0% minimized to tray** |
| **M1 Foundation** | UI thread blocking I/O | **0 synchronous disk or SQLite operations on Dispatcher** |
| **M9 Command Centre** | Active working set (full UI) | **≤ 350 MB** |
| **M10 Avatars & Virtual HQ** | Incremental working set / frame rate on Intel integrated graphics | **≤ 500 MB incremental · 30–60 FPS · 0% GPU when hidden or in HUD-Only mode** |

---

## 3. Target Modular Architecture

Modules are introduced solely in the milestone that implements them, preserving a unidirectional acyclic dependency graph:

1. **`Aeron.Contracts` (`net10.0`, M1):** Shared primitives, result envelopes, typed configuration contracts, event payloads, and interfaces (zero project or package dependencies).
2. **`Aeron.Security` (`net10.0`, M1 → expanded M2):** Unconditional `RedactionFilter` and `ConfigPolicyValidator` in M1; expanded in M2 with `OwnerAuthorityGuard`, `PolicyDecisionEngine` (`G1–G4` and `L0–L3`), parameter-hashed `OwnerApprovalService`, and hash-chained `AuditLedger`.
3. **`Aeron.Infrastructure` (`net10.0`, M1):** Isolated `%LOCALAPPDATA%\Aeron` path manager, Windows current-user ACL protection, atomic `SecureConfigStore`, structured JSONL `RedactingLogger` with size rotation, and SQLite WAL connection factory + checksum-verified `SqliteMigrationRunner`.
4. **`Aeron.Core` (`net10.0`, M1 → expanded M4/M7):** Deterministic `AppLifecycleCoordinator`, root cancellation, `CoreEventBus`, `FoundationHealthService`, and composition root in M1; expanded in M4/M7 with `AeronCoreCoordinator`, `ControlledEvolutionManager` (`U-1`), `AdaptiveModeManager` (`U-2`), `DynamicPrioritizer` (`U-3`), `AdaptiveScheduler` (`U-4`), and `NotificationBroker` (`U-5`).
5. **`Aeron.Desktop` (`net10.0-windows`, WPF `WinExe`, M1 → expanded M4/M8/M9/M10):** `SingleInstanceManager` (user-scoped `Mutex` + current-user-ACL `NamedPipe`), Win32 `SystemTrayService` (`Shell_NotifyIconW`), stable dark theme, and MVVM views (zero direct SQLite or raw network coupling).
6. **Future Milestone Modules (`PROPOSED`):** `Aeron.Platform.Windows` (DPAPI/Credential Manager vault, native Windows 11 toast & audio adapters), `Aeron.Gateway` (M3 remote-only OmniRoute `CLIENT_API` integration), `Aeron.Memory` (M5 contextual memory `U-7`), `Aeron.Agents` (M6 AERON-owned multi-agent engine & message bus `U-6`), and `Aeron.Execution` (M7 compiled v1 tool runner — zero skill scripts).

---

## 4. Dependency-Ordered Milestone Roadmap

| Milestone | Deliverable | Dependencies |
|---|---|---|
| **M1** | Native Windows Application Foundation (solution, lifecycle, single-instance, tray, config, redacting logger, SQLite WAL + migrations, governance docs, automated tests, Windows CI) | Owner authorization (`CONFIRMED`) |
| **M2** | Security Kernel: `ADR-AUTH` governance, `G1–G4` / `L0–L3` policy engine, DPAPI/Credential Manager vault, Owner approval workflow, hash-chained audit ledger | M1 + canonical `ADR-AUTH-001..003` & `U-8`/`U-8R` review |
| **M3** | Remote-Only Model Gateway: OmniRoute `CLIENT_API` streaming integration, standing `G1` provider/model/zero-cost guards, pre-wire `G2` disclosure guard, fail-closed quota handling | M1, M2 + approved ₹0 gateway allowlist |
| **M4** | AERON Core v1 Vertical Command Slice & `U-2` Adaptive Modes | M2, M3 |
| **M5** | Persistent Contextual Memory (`U-7`) with `DataClassification`, FTS5 search, token budgeting, and Owner inspect/edit/forget | M2, M4 |
| **M6** | Custom Multi-Agent Engine, Bounded Adaptive Personalities & Structured Team Communication (`U-6`) | M4, M5 |
| **M7** | Controlled Autonomous Execution (compiled v1 tools under `G2/G3/G4/L2`), Dynamic Prioritization (`U-3`), Adaptive Scheduling (`U-4`), Adaptive Notifications (`U-5`), Controlled Evolution (`U-1`) | M2, M5, M6 |
| **M8** | Natural Voice Interaction (₹0, zero local LLMs/model weights, barge-in, voice-gated approvals) | M4, M6 |
| **M9** | Full Native WPF Command Centre UI (optimized for `APEX` 16 GB RAM & Intel iGPU) | M5, M6, M7 |
| **M10** | Realistic Agent Avatars & Interactive Virtual Headquarters (`U-6`, engineered for Intel integrated graphics with zero-GPU HUD-Only mode) | M6, M8, M9 |
| **M11** | Adversarial Security Hardening, Soak Profiling, Windows Installer & v1.0 Release | M1–M10 |
