# AERON — Sir Thaddeus Adoption Audit

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `VERIFIED` audit · recommendation `PROPOSED` (requires Project Owner decision)
- **Audit date:** 2026-11-28
- **AERON baseline:** `main` @ `33bbd6a96a074dadf0ee7f96a45412a83745c4eb`; working branch `arena/64f6701f-aeron-core`, branched from that commit
- **Upstream reviewed:** [`raydeStar/sir-thaddeus`](https://github.com/raydeStar/sir-thaddeus) @ `845b50c9b450d77cf7d634dd90bc72bee37844b5` (shallow clone of the default branch, read-only)
- **Companion documents:** [`MIGRATION-MAP.md`](MIGRATION-MAP.md) · [`ADR-FOUND-001-PROPOSED.md`](ADR-FOUND-001-PROPOSED.md) · [`../../THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md)

## 0. Summary

Sir Thaddeus is a substantial, working .NET 10 local-first AI workspace. Its engineering is reusable in parts. Its product topology, its inference assumptions, and its authority model conflict with AERON's ratified constraints. Adopting it as a whole repository would replace the AERON security kernel, and the AERON architecture tests would reject most of it.

**Verdict:** whole-repository adoption is **not compatible** and was **not performed**. A **selective, module-by-module adoption** is compatible, but each module needs its own ADR, Owner authorization, and architecture-test review. This branch therefore delivers the audit, the provenance record, the migration map, and the proposed decision. It does not import upstream source, because no milestone in AERON's governance currently authorizes that, and because the sandbox cannot compile the code (see §5).

The requested branch name `integration/sir-thaddeus-foundation` was not used. The session is bound to `arena/64f6701f-aeron-core`, and the branch name is fixed for this session.

## 1. Upstream architecture (Sir Thaddeus, `VERIFIED`)

| Area | What the source shows | Evidence |
|---|---|---|
| Languages / runtime | C# on `net10.0` (SDK pinned `10.0.103`), plus a React 18 + Vite + TanStack + Zustand + Tailwind workspace under `web/` | `global.json`, `web/package.json` |
| Size | 845 `.cs` files (~203,700 lines including tests and tools); ~223 test source files; 132 mapped HTTP endpoints in `src/Thaddeus.Runtime/Api` | `find`/`wc` over the clone |
| Desktop shell | **Photino.NET 4.0.16** (native window hosting a WebView2 surface) in `src/Thaddeus.Shell` | `src/Thaddeus.Shell/Thaddeus.Shell.csproj` |
| Runtime topology | `Thaddeus.Runtime` is an ASP.NET Core / Kestrel host bound to `IPAddress.Loopback` on an ephemeral port, with a per-launch bearer token (`RuntimeBearerAuthMiddleware`), WebSocket traffic, an IPC endpoint, and a lock file | `src/Thaddeus.Runtime/Program.cs` (line 81), `Hosting/RuntimeBearerAuthMiddleware.cs` |
| Startup | Shell launches the runtime; `ParentProcessWatcher` ends the runtime when its parent dies; `RuntimeStopAllService` handles shutdown | `Program.cs`, `ParentProcessWatcher.cs` |
| Inference | OpenAI-compatible client with a LM Studio flavour (`LmStudioClient`), a factory, a `CodexCliLlmClient` that spawns an external Codex CLI process, and an `OpenAiEmbeddingClient` that calls `/v1/embeddings` and falls back to BM25 | `packages/llm-client/...` |
| Documentation of defaults | README directs users to LM Studio (`127.0.0.1:1234`) or Ollama (`127.0.0.1:11434`) as the default model endpoints | `README.md` |
| Memory | SQLite with FTS5 (`SirThaddeus.Memory.Sqlite`), a Markdown wiki with FTS5 (`SirThaddeus.Wiki`, `LocalWikiStore`), JSON-file thread, memo and routine stores | `packages/memory-sqlite`, `packages/wiki`, `src/Thaddeus.Runtime/Chat/JsonFileThreadStore.cs` |
| Agent / orchestration | Assistant router, turn coordinator, a pipeline with steps, a personality engine, and harness-driven experimentation | `src/Thaddeus.Runtime/Chat/*`, `packages/agent`, `packages/personality-engine` |
| Tools and MCP | `ModelContextProtocol 1.3.0` SDK. MCP tool servers run as separate stdio processes (`apps/mcp-server`). Built-in tool groups include web search, files, browser (Playwright), system commands, and screen capture | `packages/mcp-tools-core`, `packages/tool-runner`, `packages/local-tools` |
| Permissions | `PermissionBroker` issues in-memory tokens with a capability, scope, purpose and duration. Capability groups are `Off` / `Ask` / `Always`, with per-tool overrides. `EnforcingToolRunner` enforces the policy | `packages/permission-broker`, `packages/tool-runner/.../EnforcingToolRunner.cs`, README "Control Is the Feature" |
| Audit | `JsonLineAuditLogger` writes JSONL events, scrubbed by a regex-based `AuditSensitiveDataScrubber`. There is **no** hash chain or tamper-evidence | `packages/audit-log` |
| Voice | Whisper.cpp (local GGML/GGUF model, external process), Piper (local `.onnx` voices), Kokoro via `KokoroSharp.CPU` (ONNX Runtime, `AutoDownloadModel = true` by default, downloads `.onnx` weights from GitHub) | `src/Thaddeus.Runtime/Voice/*`, `src/Thaddeus.Tts.Kokoro/KokoroOptions.cs` |
| Web search | Self-hosted SearXNG launched as a managed localhost process (AGPL-3.0-or-later bundle), plus AngleSharp / HtmlAgilityPack parsing | `packages/runtime-host/.../SearxngHostLauncher.cs`, `apps/searxng/THIRD_PARTY_NOTICES.md` |
| Configuration | `SirThaddeus.Settings.template.json`, `JsonFileSettingsStore` | repository root, `src/Thaddeus.Runtime/Settings` |
| Build / CI | `SirThaddeus.sln`; five GitHub workflows (`ci-pr`, `ci-nightly`, `ci-promote`, `ci-release`, `ci-sbom`); central package management is not used (versions are per-project) | `.github/workflows`, `Directory.Build.props` |
| Research harness | `tools/SirThaddeus.Harness` and `DirectEval` hold benchmark suites and scorers inside the repository. `AGENTS.md` makes the benchmark method a governing rule | `AGENTS.md`, `tools/` |
| Licence / notices | Apache License 2.0 (full text). **No `NOTICE` file.** No per-file copyright headers. The LICENSE appendix is the unfilled template, so no copyright holder is named. Only `apps/searxng` ships a third-party notice | `LICENSE`, `find . -iname "NOTICE*"` returned nothing |

## 2. Existing AERON architecture (`VERIFIED` from `main`)

| Area | What the source shows | Evidence |
|---|---|---|
| Stack | C# 14 · .NET 10 LTS · WPF `WinExe` (`win-x64`), `TreatWarningsAsErrors=true`, no `NoWarn` | `Directory.Build.props`, `global.json` |
| Modules | `Aeron.Contracts` → `Aeron.Security`, `Aeron.Infrastructure` → `Aeron.Core` → `Aeron.Desktop` (WPF). Five test assemblies | `Aeron.sln`, `src/`, `tests/` |
| Security kernel | `IdentityAuthority` (fail-closed), `SecurityPolicyEvaluator` (default-deny), `ApprovalEngine` + `DurableApprovalStore` (single-use, exact-binding), `ActionDispatcher` (audit before effect, epoch guard), `DurableAuditLedger` (hash chain), `PreemptionState` (emergency stop), `OwnerAuthorityGuard`, `WindowsCredentialVault` (**BLOCKED**) | `src/Aeron.Security/`, `docs/specifications/M2/M2-IMPLEMENTED-ARCHITECTURE.md` |
| Infrastructure | `%LOCALAPPDATA%\Aeron` paths, DPAPI protector, current-user ACLs, `SecureConfigStore` (rejects inline credentials), `RedactingLogger`, SQLite WAL + checksum-verified migrations | `src/Aeron.Infrastructure/` |
| Core | `AppLifecycleCoordinator`, `CoreEventBus`, `FoundationHost`, `SecurityComposition` | `src/Aeron.Core/` |
| Desktop | Single-instance guard, Win32 tray, WPF MVVM shell, read-only security surface | `src/Aeron.Desktop/` |
| Architecture tests | Enforce the exact source-project list, forbid `UseWPF` in headless projects, forbid `Microsoft.Data.Sqlite` in Desktop, forbid `HttpClient`/`HttpListener`/`TcpListener` in Security and Desktop, ban LangChain / SemanticKernel / AutoGen / CrewAI / LlamaSharp / Ollama / OnnxRuntime / TorchSharp / TensorFlow / OpenAI / Anthropic, and ban model-weight file types | `tests/Aeron.Architecture.Tests/ArchitectureTests.cs` |
| Not yet built | AI provider / OmniRoute gateway (M3), contextual memory (M5), agents (M6), tool execution (M7), voice (M8), Command Centre (M9) | `docs/milestones/M1-FOUNDATION.md`, `docs/architecture/BLUEPRINT.md` |
| Blocked | Owner MFA (D2-01 is recorded as *Resolved (Conditional)* with mandatory conditions; `MakeCredential` and `GetAssertion` are not authorized; D2-02 to D2-10 are open), vault storage (D2-05/D2-06) | `docs/specifications/M2/M2-DECISION-REGISTER.md`, `OWNER-DECISION-REQUIRED.md` |
| CI | `ci-windows.yml` (Windows, .NET 10 SDK from `global.json`, hygiene scan, APEX measurement self-test, build with warnings-as-errors, test with a 110-test and 60-security-test gate, publish). The last run on `main` (`46143993228`) succeeded | `.github/workflows/ci-windows.yml`, `gh run list` |
| Governance | `CONSTRAINTS.md` C-01…C-12; `DECISION_LEDGER.md`; `ADR-TECH-001`; ADR-AUTH-001…003 (`CONFIRMED` intent, verbatim clauses `UNRESOLVED`). OmniRoute `CLIENT_API` endpoint and the ₹0 provider allowlist are `UNRESOLVED` (pre-M3) | `docs/governance/` |

## 3. Compatibility matrix (`VERIFIED` against both source trees)

Verdicts: **Reuse** (adopt with attribution), **Adapt** (port and change to fit), **Replace** (AERON component supersedes it), **Defer** (possible later, blocked by a decision or milestone), **Exclude** (incompatible; not carried over).

| Upstream component | AERON rule it meets | Verdict | Reason |
|---|---|---|---|
| Photino WebView shell + React workspace | C-02; ADR-TECH-001 ("zero browser wrapper, zero localhost server"); the Desktop architecture test | **Exclude** | Direct conflict with a ratified constraint. The UI must be WPF. |
| Kestrel loopback runtime, bearer-token API, WebSocket, IPC | C-02 ("never a web app or localhost UI"); Desktop/Security bans on HttpListener/TcpListener/HttpClient | **Exclude** as a topology | AERON runs in-process through `Aeron.Core`. Any future network client must be an approved gateway module, not a local server. |
| LM Studio / Ollama / any local `/v1` endpoint as the default | C-03, C-04, C-06; "do not install Ollama, LM Studio or other local runtimes" | **Exclude** | Local inference is forbidden. The only permitted provider is remote, via OmniRoute. |
| `CodexCliLlmClient` (external process) | C-03, G1 allowlist, C-05 | **Exclude** | Not an approved remote gateway. It launches an external process. |
| `OpenAiEmbeddingClient` (`/v1/embeddings`) | G1 (approved provider only); pre-M5 "FTS5 only vs. hybrid" is `UNRESOLVED` | **Defer (M5)** | The pattern could fit OmniRoute. Remote embeddings also raise G2 disclosure questions for memory content. |
| KokoroSharp TTS (ONNX Runtime, auto-downloads model weights) | C-04; the OnnxRuntime architecture ban; "never download model weights" | **Exclude** | Breaks three rules at once. |
| Piper voices (`.onnx`) and Whisper.cpp (`ggml`/`gguf`) | C-04; the model-weight extension ban | **Exclude** | Local model files. Voice must use an approved ₹0 remote STT/TTS pipeline (pre-M8 is `UNRESOLVED`). Indian-accent voice support is a requirement, and no approved provider exists yet. |
| `PermissionBroker` (in-memory tokens, Off/Ask/Always) | C-10, C-11; ADR-AUTH-002/003 (default-deny, parameter-hashed, exact-binding Owner approval) | **Replace** | AERON's `SecurityPolicyEvaluator` + `ApprovalEngine` + `ActionDispatcher` already exceed this model. The upstream token store is volatile and approves by capability category, not by exact parameters. |
| `EnforcingToolRunner`, `SystemCommandTool`, `FileReadTool`, `BrowserNavigateTool`, `ScreenCaptureTool` | C-07 (AERON owns its execution engine); C-09; L2/L3; G4 | **Replace** (M7) | The AERON execution path must go through `ActionDispatcher`. Upstream arbitrary command execution is forbidden. Individual tool logic may be re-authored as compiled, statically registered v1 tools. |
| `ModelContextProtocol 1.3.0` SDK | C-08 (not an agent framework; it is a protocol SDK); G1–G4 for each tool | **Defer (M7)** | Acceptable in principle. Its licence is in transition from MIT to Apache-2.0, so the licence must be verified for the exact version before use. |
| Agent router, turn coordinator, pipeline steps, personality engine | C-07 (AERON owns its agent engine); M6 "bounded adaptive personalities" | **Defer (M4/M6)** | Architecture ideas can be reviewed. Implementation must be AERON-native, or Owner-authorized as a port. |
| Memory SQLite + FTS5 schema (`SirThaddeus.Memory.Sqlite`) | M5, U-7 (`DataClassification`, Owner inspect/edit/forget), pre-M5 FTS5 decision | **Defer (M5)** | Strong candidate for an adapted port once the data-classification design exists. The separate legacy `JsonFileMemoStore` (and `MemosToWikiMigrator`) in the runtime is not a candidate. |
| Markdown wiki (`SirThaddeus.Wiki`, FTS5) | M5; C-12 (no secrets in memory); G2/G4 for AI edits | **Defer (M5+)** | Requires the memory-governance design first. |
| `JsonLineAuditLogger` + regex scrubber | AERON's audit requirement (hash-chained `DurableAuditLedger`) | **Replace** | The AERON ledger is strictly stronger. The scrubber may still serve as a secondary redaction test corpus after review. |
| Upstream `RedactingLogger`-equivalent (Serilog file/console sinks) | C-12 (unconditional redaction) | **Replace** | AERON's `RedactingLogger` is a redaction boundary. Serilog is Apache-2.0 and could be reused later only behind AERON's redaction. |
| `SettingsStore` / `SirThaddeus.Settings.template.json` | C-12 (reject inline credentials), `SecureConfigStore`, `ConfigPolicyValidator` | **Replace** | AERON already has a stricter config path. |
| Thread store, routines, state machine, event bus | M4 vertical slice, `CoreEventBus`, `AppLifecycleCoordinator` | **Replace / Defer** | Overlapping AERON components exist. Keep AERON's; reconsider routines in M7. |
| Tray, single instance, desktop chrome | M1 complete; ADR-TECH-001 | **Replace** | AERON's Win32 tray and `SingleInstanceManager` are already implemented and tested. |
| Document reader (PdfPig, DocumentFormat.OpenXml, SmartReader) | M5 "project and document awareness"; G2 for file content | **Defer (M5)** | PdfPig (Apache-2.0), OpenXML SDK (MIT) and AngleSharp/HtmlAgilityPack (MIT) are permissive. SmartReader's licence could **not** be verified in this audit. |
| Web search (SearXNG AGPL bundle, localhost launcher) | C-02 (localhost service), G1/G2 (external disclosure), AGPL network-use obligations | **Exclude** | AGPL-3.0-or-later plus a managed localhost process. Any search must use an Owner-approved, remote provider. |
| Playwright browser automation | G4/L2 (external actions); C-09 | **Exclude for now** | Browser automation is a consequential action class. Revisit only as a G-gated M7 tool. |
| Harness / benchmark suites (`tools/`, `DirectEval`) | Sir Thaddeus' own research method; keep benchmarks out of production assemblies | **Exclude** | Not product code. Could inform AERON's test method later. |
| Upstream CI workflows | AERON CI already exists and gates the security suites | **Exclude** | AERON's `ci-windows.yml` is stricter for this repo. |

## 4. Licence and dependency findings

### 4.1 Upstream licence (`VERIFIED`)

- Sir Thaddeus is licensed under the **Apache License 2.0**, confirmed by the GitHub licence API (`spdx_id: Apache-2.0`) and by the full text in `LICENSE`.
- Apache-2.0 permits proprietary redistribution and modification. It requires preserving the licence text and copyright/attribution notices, marking modified files, and retaining any `NOTICE` file (none exists upstream).
- Apache-2.0 §6 grants no trademark rights. AERON must not present itself as Sir Thaddeus, and must not use the Sir Thaddeus name for the product.
- No copyright holder is named in `LICENSE` and no per-file headers exist. Attribution therefore cites the repository and its owner (`raydeStar`). It does not assert a specific individual's copyright.

### 4.2 Dependency licences

Verified through GitHub repository metadata (`api.github.com`) unless noted:

| Package (upstream version) | Licence | Verification | Used by AERON now? |
|---|---|---|---|
| Photino.NET 4.0.16 | Apache-2.0 | GitHub `tryphotino/Photino.NET` | No (excluded) |
| KokoroSharp.CPU 0.8.0 | MIT | GitHub `Lyrcaxis/KokoroSharp` | No (excluded) — its model weights are a separate question |
| Microsoft.Playwright 1.59.0 | MIT (repo) | GitHub `microsoft/playwright-dotnet` | No (excluded) |
| ModelContextProtocol 1.3.0 | **Transitioning MIT → Apache-2.0** (repo `LICENSE` text; GitHub reports `NOASSERTION`) | GitHub `modelcontextprotocol/csharp-sdk` | No (deferred). **Verify the exact pinned version's licence before use.** |
| PdfPig 0.1.14 | Apache-2.0 | GitHub `UglyToad/PdfPig` | No (deferred) |
| DocumentFormat.OpenXml 3.5.1 | MIT | GitHub `dotnet/Open-XML-SDK` | No (deferred) |
| Serilog 4.3.1 | Apache-2.0 | GitHub `serilog/serilog` | Not in AERON yet |
| AngleSharp 1.5.2 | MIT | GitHub `AngleSharp/AngleSharp` | No (deferred) |
| HtmlAgilityPack 1.12.4 | MIT | GitHub `zzzprojects/html-agility-pack` | No (deferred) |
| YamlDotNet 18.1.0 (harness only) | MIT | GitHub `aaubry/YamlDotNet` | No (excluded) |
| NCalcSync / NCalc | MIT | GitHub `ncalc/ncalc` (the NCalcSync fork's licence was not separately checked) | No (deferred) |
| Cronos 0.13.0 | MIT | GitHub `HangfireIO/Cronos` | No (deferred to M7 scheduling) |
| NUlid 1.7.3 | MIT | GitHub `RobThree/NUlid` | No |
| Microsoft.Data.Sqlite 10.0.9 | MIT | GitHub `dotnet/efcore` (package lives there) | **Yes** — AERON already uses `Microsoft.Data.Sqlite` 10.0.15 (MIT, same project) |
| SQLitePCLRaw.bundle_e_sqlite3 3.0.4 | Apache-2.0 | GitHub `ericsink/SQLitePCL.raw` | No |
| SmartReader 0.11.0 | **Not verified** (GitHub lookup for the likely repository did not resolve) | — | No (deferred). Verify before use. |
| Interop.UIAutomationClient 10.19041.0 | Microsoft (Windows SDK licence) — not checked | — | No (excluded) |
| SearXNG (bundled in `apps/searxng`) | **AGPL-3.0-or-later** | `apps/searxng/THIRD_PARTY_NOTICES.md` | No (excluded) |
| npm web dependencies (React, Vite, TanStack, Tiptap, Tailwind, etc.) | Not audited in this pass | `web/package.json` | No (excluded with the React UI) |

### 4.3 Restricted or incompatible items

1. **SearXNG (AGPL-3.0-or-later)** — copyleft with network-use obligations. Must not be bundled or linked. Excluded.
2. **Model weights downloaded at runtime** (Kokoro `.onnx` from GitHub; Piper voices; Whisper GGML) — not a licence conflict in itself, but a direct C-04 violation. Excluded.
3. **ModelContextProtocol SDK** — licence transition. Verify before any use.
4. **SmartReader** — licence unverified. Verify before any use.
5. **No NOTICE / no attribution requirement beyond Apache §4** — attribution is still required. It is recorded in `THIRD_PARTY_NOTICES.md`.

## 5. Build and test verification (`VERIFIED` limits)

- The sandbox has **no .NET SDK**, **no PowerShell**, and **no access to `api.nuget.org`**. Local `dotnet build` and `dotnet test` are impossible here. This was confirmed by probing the hosts. Earlier M2 documentation records the same constraint.
- The authoritative build and test verifier is the GitHub Actions Windows workflow, `ci-windows.yml`. Its result on the pushed branch is the only compiled evidence, and it is reported in the pull request, not claimed here.
- This branch changes **only documentation files**. It does not change any `.cs`, `.csproj`, `.props`, `.xaml`, workflow, or test file. The expected CI result is therefore unchanged from `main`, but that is an inference until the workflow runs.
- Application startup, shutdown, OmniRoute connectivity, permission enforcement in a running process, and manual APEX verification were **not** performed and are **not** claimed.

## 6. Security assessment of the upstream model

| Upstream property | Risk to AERON | Disposition |
|---|---|---|
| Runtime bound to loopback with a rotating bearer token | Any local process that can read the lock file or token can call the API. AERON has no such surface. | Do not adopt the topology. |
| Permission policy is per capability, Off/Ask/Always, with in-memory tokens | An "Always" grant is broad and persists for the session. This is weaker than AERON's per-action, parameter-hashed approval. | Do not adopt as the authority. |
| Arbitrary system-command tool behind policy | Violates C-09, L3, and the "no unrestricted tool execution" rule. | Exclude. |
| Regex-based audit scrubbing; JSONL with no tamper evidence | Does not meet AERON's hash-chained audit requirement. | Do not adopt; AERON ledger governs. |
| Auto-download of model weights (Kokoro) | Violates C-04 and creates an unreviewed supply-chain path. | Exclude. |
| Default endpoints to local LM Studio / Ollama | Violates C-03/C-04. | Exclude. |
| Benchmark datasets and harness inside the repository | Not a security issue, but a production-separation concern that AERON's governance would not accept in production assemblies. | Exclude. |

**AERON invariants preserved by this branch:** all existing source files are unchanged. The security kernel, the architecture tests, the hygiene checks, the CI gates, and the M2 decision register are untouched. No credential, key, or Owner factor was created or changed. No decision was recorded or changed.

## 7. Open questions and blocking decisions (`UNRESOLVED`)

These questions need explicit Project Owner decisions. None was assumed.

1. **Adoption scope.** Confirm selective module adoption (recommended in [`ADR-FOUND-001-PROPOSED.md`](ADR-FOUND-001-PROPOSED.md)) rather than whole-repository adoption or reference-only use.
2. **C-07 interpretation.** C-07 requires AERON to own its agent, permission, and execution engines, "designed and implemented natively". Does adopting Apache-2.0 upstream code for *non-engine* modules satisfy C-07? This audit recommends that upstream engine code (permissions, tools, agents) is **not** adopted.
3. **Pre-M3 OmniRoute.** The exact `CLIENT_API` endpoint, the approved ₹0 provider/model/combo allowlist, and the G2 disclosure baseline are all `UNRESOLVED`. No gateway code can be written until they are supplied.
4. **Pre-M5 retrieval.** FTS5-only or hybrid with remote embeddings.
5. **Pre-M8 voice.** An approved ₹0 STT/TTS pipeline with Indian-accent support.
6. **Architecture-test changes.** Adding any project requires changing the exact project list in `ArchitectureTests.cs`. That is a governed change and needs Owner approval.
7. **Licence checks.** Verify the MCP SDK licence for the exact pinned version, and verify SmartReader, before those components are considered.
8. **D2-01 T1/T2, D2-02, D2-05, D2-06, and the rest of the M2 register** remain open. This branch does not change them.

## 8. Recommended integration sequence (`PROPOSED`)

Each step needs its own Owner authorization. Each step must also keep the AERON security suites green, and must not weaken any architecture test.

1. **Now (this branch):** audit, provenance, migration map, proposed ADR. No code.
2. **Provenance hygiene:** add upstream attribution to any file that is later ported (`Adapted from raydeStar/sir-thaddeus @ 845b50c…, Apache-2.0`), and keep `THIRD_PARTY_NOTICES.md` current.
3. **Remote-only gateway (M3, after the OmniRoute decisions):** build an `Aeron.Gateway` module against the OpenAI-compatible `/v1` contract, using AERON's `PolicyDecisionEngine` for G1 guards. Reference the upstream `LlmClientFactory` and `OpenAiEmbeddingClient` for request shape only.
4. **Memory (M5):** port the SQLite/FTS5 schema pattern into an AERON `Aeron.Memory` module, with `DataClassification`, Owner inspect/edit/forget, and redaction on write.
5. **Document reading (M5):** consider the PdfPig / OpenXML readers behind a G2 gate.
6. **Tools (M7):** re-author tools as compiled, statically registered modules routed through `ActionDispatcher`. Do not import the upstream tool runner.
7. **UI (M9):** AERON WPF Command Centre. The upstream React workspace is not reused.

## 9. Evidence index

- Upstream clone: `/tmp/sir-thaddeus` (not in the repository), commit `845b50c9b450d77cf7d634dd90bc72bee37844b5`.
- AERON source references as cited above.
- GitHub API licence checks, run 2026-11-28.
- AERON CI run `46143993228` on `main` (success), from `gh run list`.
