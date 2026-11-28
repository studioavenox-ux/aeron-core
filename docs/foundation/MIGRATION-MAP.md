# AERON — Sir Thaddeus Migration Map

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED` — every row is a recommendation. Nothing in this table has been imported, and no row is authorized for implementation until its milestone and Owner decision are recorded.
- **Upstream:** `raydeStar/sir-thaddeus` @ `845b50c9b450d77cf7d634dd90bc72bee37844b5` (Apache-2.0)
- **AERON baseline:** `main` @ `33bbd6a96a074dadf0ee7f96a45412a83745c4eb`
- **Basis:** [`SIR-THADDEUS-ADOPTION-AUDIT.md`](SIR-THADDEUS-ADOPTION-AUDIT.md)

## 1. Current state of this branch

| Item | State |
|---|---|
| Upstream source imported | **None** |
| AERON source changed | **None** (no `.cs`, `.csproj`, `.props`, `.xaml`, workflow, or test file) |
| Files added | `docs/foundation/SIR-THADDEUS-ADOPTION-AUDIT.md`, `docs/foundation/MIGRATION-MAP.md`, `docs/foundation/ADR-FOUND-001-PROPOSED.md`, `THIRD_PARTY_NOTICES.md` |
| Files modified | `README.md` (one link to the foundation documents) |
| Files removed | None |

## 2. Reused — adopt with attribution (`PROPOSED`)

Nothing qualifies for direct reuse today. Permissive upstream code could be reused as-is, but every current candidate is either excluded or tied to a deferred milestone. Reuse becomes available only after an Owner decision.

## 3. Adapted — port and change to fit AERON (`PROPOSED`, all deferred)

| # | Upstream source | AERON target | Milestone | Gate before work starts |
|---|---|---|---|---|
| A1 | `packages/llm-client` — OpenAI-compatible request/response shapes, `LlmClientFactory` | `Aeron.Gateway` (remote-only OmniRoute `CLIENT_API`) | M3 | Exact OmniRoute `CLIENT_API` endpoint, ₹0 provider/model allowlist, G2 disclosure baseline (all `UNRESOLVED`); new project needs an architecture-test change |
| A2 | `packages/llm-client` — `OpenAiEmbeddingClient` | `Aeron.Memory` embedding path | M5 | Pre-M5 FTS5-only vs hybrid decision; G2 for memory content sent remotely |
| A3 | `packages/memory-sqlite` — schema and FTS5 query patterns | `Aeron.Memory` (SQLite WAL, `DataClassification`, Owner inspect/edit/forget) | M5 | `U-7` design; Memory reconciliation decision; retention policy |
| A4 | `packages/wiki` — Markdown page model, revisions, search | `Aeron.Memory` / wiki surface | M5+ | As A3, plus G4 for AI edits |
| A5 | `packages/document-reader` — PDF / DOCX / XLSX / CSV / Markdown extraction | `Aeron.Documents` (project and document awareness) | M5 | G2 for file content; **SmartReader licence check**; PdfPig and OpenXML are permissive |
| A6 | `packages/web-search`, `packages/mcp-tools-core` — HTML parsing helpers (AngleSharp, HtmlAgilityPack) | Read-only tool parsing | M7 | G1 approved provider; both parsers are MIT |
| A7 | `packages/mcp-shared`, `packages/mcp-tools-core` — MCP tool-definition patterns (ModelContextProtocol 1.3.0) | `Aeron.Execution` MCP adapter | M7 | **MCP SDK licence check for the pinned version**; each tool routed through `ActionDispatcher` |
| A8 | `packages/memory` — `RelevanceGate`, `MemoryRetriever`, scoring | `Aeron.Memory` retrieval policy | M5 | Design review against U-7 and token budgeting |
| A9 | `packages/personality-engine` — bounded personality model | `Aeron.Agents` persona constraints | M6 | Pre-M6 roster and personality-bounds decision |
| A10 | `packages/agent` — pipeline steps as design reference only | `Aeron.Agents` | M6 | Must be AERON-native (C-07); reference only, no code copied |
| A11 | `packages/config` — layered config shape | `SecureConfigStore` extension | M3 | Must keep C-12 (no inline credentials) and `ConfigPolicyValidator` |
| A12 | `packages/logging` — Serilog sink configuration | Behind `RedactingLogger` | Later | Redaction must remain unconditional and first in the pipeline |
| A13 | `src/Thaddeus.Runtime/Routines` — `Cronos` cron scheduling | `U-4` adaptive scheduler | M7 | Pre-M7 operational parameters; L2 approval for persistent schedule changes |

## 4. Replaced — AERON component supersedes the upstream component

| # | Upstream component | AERON replacement (already in `main`) | Reason |
|---|---|---|---|
| R1 | Photino shell + React workspace (`src/Thaddeus.Shell`, `web/`) | WPF `Aeron.Desktop` shell, `FoundationDark.xaml` theme, MVVM | C-02, ADR-TECH-001 |
| R2 | Kestrel loopback runtime, bearer-token API, WebSocket, IPC (`src/Thaddeus.Runtime/Program.cs`, `Hosting`, `Ipc`, `Ws`) | In-process `Aeron.Core` composition (`FoundationHost`, `SecurityComposition`) | C-02; zero localhost server; Desktop bans on HttpListener/TcpListener |
| R3 | `PermissionBroker` and `EnforcingToolRunner` | `SecurityPolicyEvaluator`, `ApprovalEngine`, `ActionDispatcher`, `PreemptionState` | C-10, C-11; ADR-AUTH-002/003; default-deny |
| R4 | `JsonLineAuditLogger` + regex `AuditSensitiveDataScrubber` | `DurableAuditLedger` (hash chain, truncation detection) | AERON audit requirement |
| R5 | `Settings` JSON store (`JsonFileSettingsStore`, `SirThaddeus.Settings.template.json`) | `SecureConfigStore` + `ConfigPolicyValidator` | C-12 |
| R6 | `RedactingLogger`-equivalent logging | AERON `RedactingLogger` (`Aeron.Infrastructure`) | C-12 unconditional redaction |
| R7 | Tray, single-instance, close-to-tray | `Win32SystemTrayService`, `SingleInstanceManager` (M1 complete) | ADR-TECH-001 D-M1-03 |
| R8 | Event bus and state machine (`Events`, `State`) | `CoreEventBus`, `AppLifecycleCoordinator` | Existing tested lifecycle |
| R9 | Stop / parent-watcher lifecycle | AERON `AppLifecycleCoordinator` and root cancellation | Existing tested lifecycle |
| R10 | Local inference clients (`LmStudioClient`, `CodexCliLlmClient`) | Remote-only OmniRoute gateway (A1) | C-03, C-04, C-06 |
| R11 | Local voice (Piper `.onnx`, Whisper.cpp GGML, KokoroSharp ONNX) | Approved ₹0 remote STT/TTS (pre-M8 decision) | C-04; no model-weight download |
| R12 | Managed SearXNG localhost search | Owner-approved remote search provider (decision required) | C-02, AGPL exclusion |
| R13 | Playwright browser automation | Deferred G-gated M7 tool (if approved) | G4/L2 |

## 5. Deferred — possible later, blocked by a decision or milestone

| # | Item | Blocked by |
|---|---|---|
| D1 | Contextual memory (`U-7`) | Memory reconciliation decision; M2 Owner authority; pre-M5 retrieval decision |
| D2 | Multi-agent engine and personalities (`U-6`) | Pre-M6 specialist roster; C-07 interpretation |
| D3 | Compiled v1 tool catalog (`U-1`, `U-3`, `U-4`, `U-5`) | Pre-M7 catalog; M2 dispatch path; M5 memory |
| D4 | Voice interaction, push-to-talk, wake word, Indian-accent voice | Pre-M8 approved ₹0 STT/TTS pipeline |
| D5 | Command Centre and avatars | M9/M10; pre-M10 avatar workflow |
| D6 | Owner MFA, recovery, vault storage | D2-01 T1/T2, D2-02, D2-05, D2-06 (all open; not changed here) |
| D7 | OmniRoute `CLIENT_API` gateway | Pre-M3 endpoint and ₹0 allowlist |
| D8 | Windows installer and publish | M11 |

## 6. Excluded — not carried into AERON

| # | Item | Reason |
|---|---|---|
| X1 | Entire upstream repository as a fork or subtree | Would replace AERON's kernel and history; conflicts with architecture tests |
| X2 | `web/` React workspace and npm dependencies | C-02 |
| X3 | `apps/searxng` bundle (AGPL-3.0-or-later) | Copyleft network-use obligations; localhost service |
| X4 | Benchmark and harness suites (`tools/`, `DirectEval`, `SirThaddeus.Harness`) | Product/research separation; AERON governance |
| X5 | Bundled model-download code (KokoroSharp auto-download) | C-04 |
| X6 | `CodexCliLlmClient` external-process provider | C-03, C-05; not an approved gateway |
| X7 | Upstream CI workflows (`ci-pr`, `ci-nightly`, `ci-promote`, `ci-release`, `ci-sbom`) | AERON's `ci-windows.yml` is stricter for this repo |
| X8 | Upstream `.agent/`, `.cursor/`, `.githooks/` tooling | Not AERON governance; not reviewed for reuse |

## 7. Forward sequence (`PROPOSED`)

1. **This branch:** audit, provenance, proposed ADR. Docs only.
2. **Owner decision:** approve or revise [`ADR-FOUND-001-PROPOSED.md`](ADR-FOUND-001-PROPOSED.md), especially the selective-adoption scope and the C-07 interpretation.
3. **M3 gateway** (A1, A11, R10): needs the pre-M3 OmniRoute and allowlist decisions. Adds `Aeron.Gateway`, which needs an architecture-test change.
4. **M5 memory and documents** (A2–A5, A8): needs the U-7 design, the retrieval decision, and the SmartReader licence check.
5. **M7 tools and scheduling** (A6, A7, A13): needs the MCP SDK licence check and compiled tool catalog approval.
6. **M8 voice** (D4): needs an approved remote STT/TTS provider. No upstream voice code is adopted.
7. **M9 Command Centre** (D5): AERON-native WPF. The upstream React UI is not reused.

Every step produces its own pull request, runs the full AERON CI gate, and leaves `main` unmodified until the Project Owner approves a merge.
