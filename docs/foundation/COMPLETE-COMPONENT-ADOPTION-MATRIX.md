# AERON — Complete Component Adoption Matrix

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. Every row is a recommendation. Nothing is imported, and no row is authorised for implementation until its milestone and Owner decision are recorded.
- **Scope:** every component assessed across the eight repositories and the discovery candidates in [MULTI-REPOSITORY-RESEARCH.md](MULTI-REPOSITORY-RESEARCH.md). Sir Thaddeus rows A1–A13, R1–R13, D1–D8, and X1–X8 are defined in [MIGRATION-MAP.md](MIGRATION-MAP.md) and are referenced here, not repeated.

## 1. Classifications

| Class | Meaning in this matrix |
|---|---|
| **DIRECT ADOPTION** | Permissively licensed, small, self-contained code that can be used with attribution and no re-authoring. **No component qualifies today.** |
| **ADAPTATION** | Pattern or code ported to C# and changed to meet AERON's invariants. Attribution kept where code is derived. |
| **REPLACEMENT** | AERON-owned component supersedes the upstream component. The upstream component is not used. |
| **REFERENCE ONLY** | Design read for understanding. No code is copied, and no behaviour is adopted without a separate design decision. |
| **REJECT** | Incompatible with a non-negotiable, a licence, or AERON security specs. Not to be used in any form. |
| **UNVERIFIED** | Claim or code not confirmed by source read or by a run. Cannot support a decision until verified. |

Evidence paths are relative to each repository root. Tests are cited as "file exists" unless noted.

---

## 2. Sir Thaddeus (`raydeStar/sir-thaddeus`, Apache-2.0)

Rows A1–A13 and R1–R13 are in [MIGRATION-MAP.md](MIGRATION-MAP.md) and are not repeated. The rows below are assessments added in this study.

| ID | Component | Source (file · class / function) | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| ST-01 | Tool permission tiers Off/Ask/Always | `src/.../Tools/ToolPermissionGate.cs` · `ToolPermissionGate` (lines 25–267) | `tests/.../ToolPermission*` | REJECT | Replaced by `SecurityPolicyEvaluator`, `ApprovalEngine` | — | "Always" persists a grant and writes settings. Session grants are process-scoped. Violates per-action approval (L2) and default-deny |
| ST-02 | Wiki write confirmation | `ToolPermissionGate.cs:161–166` · `ToolCapability.WikiWrite` | Wiki tests (27 files) | REFERENCE ONLY | Memory write approval | M5 | Confirms that writes need a fresh call-scoped confirmation. Pattern matches AERON; re-derive |
| ST-03 | Permission broker token validation | `InMemoryPermissionBroker.cs` · `Validate` (lines 77–97) | Permission tests | REJECT | Replaced by `ActionDispatcher` + `ApprovalEngine.TryConsume` | M2 (done) | Does not consume tokens. Checks revocation, expiry, and capability only. Not single-use. No parameter scope |
| ST-04 | Tool runner enforcement | `EnforcingToolRunner.cs` · `ExecuteAsync` | Enforcement tests | REFERENCE ONLY | `ActionDispatcher` | M2 (done) | Registration, token, and Validate sequence is sound. Missing parameter binding and single-use |
| ST-05 | Path scope check | `ToolExecutionContext.cs` · `IsPathAllowed` | Path tests | REJECT | `Aeron.Execution` path policy | M7 | Uses `StartsWith` without a separator boundary. `C:\Docs` allows `C:\Docs2`. Defect. Must be rewritten with canonical path and boundary check |
| ST-06 | MCP child environment | `RuntimeMcpEnvironmentBuilder.cs`; `StdioMcpToolClient.cs` | — | REJECT | Allowlist environment (MW-05) | M7 | Passes `WEBSEARCH_API_KEY` and `ST_DEEPDIVE_PLACES_API_KEY` into child process. Violates C-12 |
| ST-07 | Preview-then-apply system tools | `mcp-tools-core` · `SystemTools.cs` (`system_execute_preview` / `system_execute_apply`) | MCP tests | REJECT for approvals | `ApprovalEngine` | M7 | Preview is non-consuming and replayable. The apply path is gated by `confirm=true` only. Dotnet allowlist (`--info`, `--version`, `restore`, `build`, `test`) is noted but not adopted |
| ST-08 | File tool previews | `mcp-tools-core` · `FileTools.cs` (860 lines) | MCP tests | REFERENCE ONLY | Preview receipts | M7 | Previews consumed with `TryRemove`. Closer to single-use than ST-07 |
| ST-09 | Panic mode | `IsBlockedByPanic` in MCP path | — | REJECT | `PreemptionState` epoch | M2 (done) | Matches tool names, which is weaker than AERON's epoch-based preemption |
| ST-10 | Shell command tool | `tool-runner` · `SystemCommandTool` | — | REJECT | — | — | Simulation stub. Not a live executor |
| ST-11 | File read tool (tool-runner) | `tool-runner` · `FileReadTool` | — | REJECT | — | — | Simulation stub |
| ST-12 | Audit logger | `JsonLineAuditLogger.cs`; `AuditEvent.cs` | Audit tests (30 files) | REJECT | `DurableAuditLedger` (hash chain, checkpoints) | M2 (done) | JSONL, no hash, no truncation detection, 10 MB rotation with 5 archives |
| ST-13 | Audit sensitive-data scrubber | `AuditSensitiveDataScrubber` | Redact tests (5 files) | ADAPTATION | Extend `RedactionFilter` | M2 follow-up | Upstream has 9 patterns. AERON's `RedactionFilter` has 3 static regexes. Upstream patterns must be reviewed and added individually, with tests |
| ST-14 | Settings store | `SettingsManager.cs` · `AppSettings.cs` | Settings tests (25) | REJECT | `SecureConfigStore` + `ConfigPolicyValidator` | M2 (done) | Non-atomic `File.WriteAllText`. Plaintext API keys. AERON store is atomic with ACLs |
| ST-15 | Runtime bearer auth | `RuntimeBearerAuthMiddleware` | — | REJECT | None (no localhost API) | — | C-02 forbids a localhost service |
| ST-16 | IPC server (named pipe / Unix socket) | `IpcServer` | — | REJECT | In-process composition | — | C-02 |
| ST-17 | Prompt-injection guard | `PromptInjectionGuard.cs` | — | REFERENCE ONLY | Context builder | M5 | Marker-based. Not a security boundary. Must not be relied on for authority |
| ST-18 | Routines store and API | `RoutinesApi.cs`; `RoutineSeeder.cs`; `JsonFileRoutineStore.cs` | — | REFERENCE ONLY | `U-4` scheduler | M7 | Manual run start only. No approval model |
| ST-19 | Scheduled cron parsing | `Cronos` usage (declared, not used in routine path) | — | ADAPTATION | `U-4` scheduler | M7 | Cronos is MIT. Keep the parsing library, not the runner |
| ST-20 | Auto memory extractor | `AutoMemoryExtractor` | Memory tests (36) | REJECT | — | — | Stores conversation chunks without explicit consent |
| ST-21 | Lifecycle (`ParentProcessWatcher`, state machine) | `ParentProcessWatcher`; `RuntimeStateMachine` | Lifecycle tests | REPLACEMENT | `AppLifecycleCoordinator` (existing) | M1 (done) | AERON lifecycle already has tested transitions (`IsTransitionAllowed`) |
| ST-22 | Document truncation | `DocumentTruncator.TruncateWithNotice` | — | ADAPTATION | `Aeron.Documents` | M5 | Small and self-contained. The closest to DIRECT ADOPTION. Still re-authored and tested in AERON |
| ST-23 | Voice backend model downloader | `voice-backend/model_downloader.py`; `KokoroSharpTtsEngine`; `PiperVoiceCatalog`; Whisper GGML | Voice tests (17) | REJECT | Approved ₹0 remote STT/TTS (pre-M8) | M8 | Downloads weights. C-04 |
| ST-24 | Voice supervisor | `VoiceBackendSupervisor.cs` · `server.py` | — | REJECT | — | — | Runs a local Python voice server |
| ST-25 | Local LLM clients | `LmStudioClient.cs` (1,665 lines); `LlmClientFactory.cs` | LlmClient tests (51) | REJECT | Remote OmniRoute gateway (A1) | M3 | Auto-loads models with `POST /v1/models/load`. Local runtime |
| ST-26 | Codex CLI provider | `CodexCliLlmClient.cs` | — | REJECT | — | — | External process, not an approved gateway (X6) |
| ST-27 | OpenAI-compatible request shapes | `packages/llm-client` | LlmClient tests | ADAPTATION | `Aeron.Gateway` (A1) | M3 | Shape only. No auto-load, no streaming dependency assumed |
| ST-28 | Document readers (CSV, DOCX, PDF, PlainText, RTF, XLSX) | `document-reader/Readers/*`; `DocumentReaderFactory.cs` | — | ADAPTATION | `Aeron.Documents` (A5) | M5 | No file-size guards upstream. Add size and page limits |
| ST-29 | SQLite memory schema and FTS5 | `memory-sqlite` · `SqliteMemoryStore.cs` (1,410 lines); `SchemaInitializer.cs` | Memory tests | ADAPTATION | `Aeron.Memory` (A3) | M5 | Parameterised SQL; secret exclusion in SQL; FTS5 external content. Add hash chain or integrity table |
| ST-30 | Memory retrieval scoring | `packages/memory` · `RelevanceGate`, `MemoryRetriever.cs` (384 lines) | — | ADAPTATION | `Aeron.Memory` retrieval (A8) | M5 | Gated by U-7 |
| ST-31 | Persona profile | `PersonalityProfile.cs` | — | ADAPTATION | `Aeron.Agents` (A9) | M6 | Gated by C-07 and roster decision |
| ST-32 | Browser automation (Playwright) | `sir-thaddeus` · Playwright reference | — | REFERENCE ONLY | Deferred M7 tool (R13) | M7 | Playwright downloads browser binaries. See Security review SL-07 |
| ST-33 | Node / React workspace | `web/` | — | REJECT | `Aeron.Desktop` WPF (R1) | — | C-02 |
| ST-34 | Searxng bundle | `apps/searxng` | — | REJECT | Owner-approved remote search (R12) | M7 | AGPL-3.0-or-later (X3) |
| ST-35 | Benchmark harness | `tools/`, `DirectEval`, `SirThaddeus.Harness` | — | REJECT | — | — | Product and research separation (X4) |

---

## 3. SenClaw (`SenClaw/senclaw`, MIT, Rust)

| ID | Component | Source (file · item) | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| SC-01 | Egress guard: single outbound gate | `src/security/egress.rs` · `EgressGuard` (line 171), `check` (215), `gate` (353), `Verdict` (120) | Not verified | ADAPTATION | Outbound text gate in front of OmniRoute calls | M3 | Concept only. Rule ordering (first-match) is reusable |
| SC-02 | Self-replication (worm) detector | `src/security/replication.rs` · `score`, `score_with`, `is_replicating` (288–318) | Not verified | REFERENCE ONLY | M5 memory and output checks | M5 | Weights are uncalibrated (`replication.rs` header). Do not cite TPR/FPR |
| SC-03 | Fan-out rule | `src/security/egress.rs` · rule 2 | Not verified | REFERENCE ONLY | Egress policy | M3 | Thresholds must be set by Owner, not copied |
| SC-04 | Plugin and skill scanner | `src/security/scan.rs` · `Severity` (60), `Finding` (174), `ScanPolicy` (213), `ScanReport.verdict` (287) | Not verified | REFERENCE ONLY | M7 third-party tool intake | M7 | Heuristic. Requires trust lifecycle decision |
| SC-05 | Permission bridge auto-accept rules | `src/agent/permission_bridge/bridge.rs` · `add_rule`, `update_rule`, `RuleAction::AutoAcceptAndAllow`, `set_accept_all` (115–125) | `permission_bridge/tests.rs` (919 lines) | REJECT | `ApprovalEngine` | M2 (done) | "Never ask again" persists. Global accept-all. Violates per-action approval |
| SC-06 | Skip-all-agents permissions switch | `src/agent/agent_pool/pool.rs` · `get_skip_perms_for_virtual` (394); `types.rs` `skip_all_agents_permissions` | — | REJECT | — | — | Global bypass for all workers |
| SC-07 | Windows AppContainer + Job Object backend | `src/sandbox/backend/direct_windows.rs` (1,648 lines) | `.github/workflows/sandbox-windows.yml` (intended) | UNVERIFIED | `Aeron.Execution` sandbox (new capability) | M7, after spike | Never run on Windows (its own research note). Loopback blocked. Requires Owner authorisation for a spike |
| SC-08 | Windows sandbox fix list (15 items) | `docs/sandbox-windows-research.md` | — | REFERENCE ONLY | Checklist for SC-07 spike | M7 | Useful known-issues list: ACL merge, environment, command-line quoting, pipe inheritance, job-object failure, AppContainer cleanup, capability probe, mount guards, Store-stub detection, generic ACE, ACL cost, non-fatal interpreter ACL |
| SC-09 | Windows process helpers | `src/sandbox/backend/winproc.rs` (1,059 lines) | 30 tests run on macOS | UNVERIFIED | Same as SC-07 | M7 | Pure string and path logic. Testable on any OS |
| SC-10 | Bash command denylist | `src/tools/bash.rs` · `BANNED_COMMANDS`; `validate_input` (85–100) | `test_banned_commands` (line 501) | REJECT | `ActionDispatcher` exact-argument policy | M7 | First-word match only. Bypassed by `sh -c`, pipes, chained commands |
| SC-11 | Safe `cd` check | `src/tools/bash.rs` · `check_cd_safety` (702) | — | REFERENCE ONLY | Path policy | M7 | Restricts `cd` to child directories. Relevant to path policy design |
| SC-12 | MCP client process spawn | `src/mcp/client.rs` · line 83–85 (`Command::new`, `cmd.envs(env)`) | — | REJECT | `Aeron.Execution` MCP adapter with allowlist env (MW-05) | M7 | Child inherits the parent environment. No `env_clear` |
| SC-13 | Scheduler cron parsing | `src/scheduler/task_scheduler.rs` · `compute_next_run` (126), `normalize_cron_expr` (155) | — | ADAPTATION | `U-4` scheduler | M7 | Local timezone and 5/6-field normalisation are useful. Re-implement or use Cronos (MIT) |
| SC-14 | Scheduler executor, `script` and `script_agent` modes | `src/scheduler/executor.rs` · `execute_script` (157), `execute_script_agent` (186), `run_script_command` (58) | — | REJECT | — | — | Runs shell on a timer with no per-run approval |
| SC-15 | Scheduler watch mode | `src/scheduler/executor.rs` · `execute_watch` (222) | — | REFERENCE ONLY | M7 watch tasks | M7 | Calls MCP tools on a schedule. Needs ApprovalEngine per run |
| SC-16 | Memory schema (FTS5 plus blob vector) | `src/memory/schema.rs` (lines 6–13, 65–86, 149, 187–193); `fts_search.rs` | — | ADAPTATION | `Aeron.Memory` (A3) | M5 | No sqlite-vec. Exact scan over blobs. Acceptable for small stores |
| SC-17 | Embedding providers: OpenAI, OpenRouter | `src/memory/embedding_providers.rs` · `OpenAiProvider` (34), `OpenRouterProvider` (170) | — | ADAPTATION | OmniRoute embeddings (A2) | M5 | Remote. Request shape only |
| SC-18 | Embedding providers: Ollama, local GGUF | `embedding_providers.rs` · `OllamaProvider` (274), `LocalProvider` (369) | — | REJECT | — | — | Local model runtime (C-03/C-04) |
| SC-19 | GNN model weights on disk | `src/memory/cognitive/gnn_sage.rs` · `SageModel::save` (98), `load` (112), `train` (461) | — | REJECT | — | — | Model weight files. Also training pipeline |
| SC-20 | Cognify (LLM triplet extraction) | `src/memory/cognitive/cognify.rs` · `CognifyPipeline` (272); `llm.rs` · `LlmClient` (14) | — | REFERENCE ONLY | Memory design input (U-7) | M5 | Makes unconsented LLM calls on memory content. Requires G2 |
| SC-21 | Local model subsystem | `src/local_models/` (9 files, 2,506 lines); MLX and GGUF | — | REJECT | — | — | Excluded (C-03/C-04) |
| SC-22 | Typed decision engine | `src/decision/` (8 files, 1,867 lines); `decision/client.rs` | — | REFERENCE ONLY | Structured-output design | M6 | Engine is local Laya or hosted `/v1/systemone`. Only the hosted request shape could be considered, after G1 |
| SC-23 | Browser agent risk tiers | `src/browser_agent/policy.rs` · `Tier` (14), `risk_tier` (70), `is_sensitive_host` (185) | Browser tests | ADAPTATION | M7 browser tool policy | M7 | "No model can lower a tier." Human tier for credentials. Gated by G4 |
| SC-24 | Browser extension bridge | `src/browser/bridge.rs` (WebSocket to Chrome extension) | — | REJECT | — | — | Localhost WebSocket (C-02) |
| SC-25 | Hash-chained audit | — (zero matches for `prev_hash`, `hash_chain`) | — | REJECT (absent) | `DurableAuditLedger` (existing) | — | Gap in SenClaw. Not a component |

---

## 4. Monaw (`kailiang0120/monaw`, Apache-2.0, Python)

| ID | Component | Source (file · item) | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| MW-01 | Approval ticket lifecycle | `backend/app/agent/approval_broker.py` · `create_ticket` (265), `approve_ticket` (424), `reject_ticket` (443), `mark_applied` (458), `mark_failed` (471), `ticket_validation_error` (366), `_expire_stale_pending` (138) | Backend test suite (106 files) | ADAPTATION | `ApprovalEngine` states | M5/M7 | States pending → approved → applied / failed / expired / superseded are reusable. Persistence is replaced (MW-01 persistence: REPLACEMENT with SQLite and `ActionDispatcher`) |
| MW-02 | One-use approval binding | `backend/app/agent/action_security/approval_binding.py` · `replay_validation`, `claim_ticket` (`_CLAIMED` set) | — | REJECT (as implemented) | `ApprovalEngine.TryConsume` (existing) | M2 (done) | One-use check is an in-process set, lost on restart. Re-claim after restart is possible. AERON's durable consume must be verified (see Security review SL-02) |
| MW-03 | Auto Review (LLM approves actions) | `backend/app/agent/approval_review.py`; `approval_broker.py` · `apply_auto_review` (400) | — | REJECT | — | — | An LLM can approve actions. Violates Owner-only approval |
| MW-04 | Full Access permission mode | `docs/permissions.md` (mode table); code location not read | — | REJECT | — | — | Removes approval prompts for file changes, commands, and app control |
| MW-05 | Allowlist environment builder | `backend/app/agent/sandbox/environment.py` · `default_inherited_env_keys` (138), `is_secret_like_env_key` (123), `is_code_loading_env_key` (90), `build_exec_environment` (154) | Sandbox tests (not read) | ADAPTATION | `Aeron.Execution` process launch | M7 | Strongest pattern reviewed. Allowlist-first, rejects secret-like and code-loading keys, reports blocked keys. Port in C#, extend the secret-prefix list (current list is provider-specific), and add tests |
| MW-06 | Blocked roots and processes | `docs/permissions.md` (Blocked Roots, Blocked Processes); enforcement code not read | — | ADAPTATION | `Aeron.Execution` path and process policy | M7 | Use as policy data. Must add separator-boundary path checks (ST-05) |
| MW-07 | HMAC action attestation | `backend/app/agent/action_security/models.py` · `_ATTESTATION_KEY`, `seal` (96) | — | REFERENCE ONLY | — | — | Key provenance not verified. AERON already uses SHA-256 chain. Do not adopt until key handling is reviewed |
| MW-08 | Windows DPAPI credential protection | `backend/app/agent/chatgpt_account.py` · `CryptProtectData` (88), `CryptUnprotectData` (96) | — | ADAPTATION | Owner vault (D6) | Deferred (D6) | Blocked by D6 and D2-01. Use only when vault is authorised |
| MW-09 | Sandbox capability probe | `backend/app/agent/sandbox/capabilities.py` · `probe_docker` (45), `probe_local_restricted` (86), `probe_capabilities` (123) | — | REFERENCE ONLY | Probe-before-claim pattern | M7 | Same principle as SC-08 item 11. Reference only |
| MW-10 | Pinned image inventory | `backend/app/agent/sandbox/image_inventory.py` | — | REFERENCE ONLY | — | M7 | Only relevant if Docker is ever approved |
| MW-11 | LangChain and LangGraph stack | `pyproject.toml` (`langchain==1.2.15`, `langgraph`) | — | REJECT | — | — | Banned by AERON `ArchitectureTests` |
| MW-12 | Computer use (pywinauto, mss) | `docs/computer-use.md`; `pywinauto`, `mss` dependencies | — | REJECT (for now) | Deferred G4 tool | M7+ | Desktop control needs G4 approval and a separate design |
| MW-13 | Provider adapters (OpenAI, Gemini) | `backend/app/agent/llm_provider_*.py`, `llm_client.py` (located, not read) | — | REFERENCE ONLY | OmniRoute adapter (A1) | M3 | Shape only. Must be re-checked by reading before use |
| MW-14 | ChatGPT account sign-in | `backend/app/agent/chatgpt_account.py`; README "Continue with ChatGPT" | — | REJECT | — | — | Consumer subscription sign-in, not an API key path. Owner decision required before any use |
| MW-15 | Background scheduler | `backend/app/agent/scheduler.py` (located, not read); `croniter` | — | REFERENCE ONLY | `U-4` | M7 | Not read in full |
| MW-16 | Telegram and Composio connectors | `connectors/`; `python-telegram-bot`, `composio-client` | — | REJECT | — | — | Unapproved external services |
| MW-17 | Frozen backend via PyInstaller | `packaging/` | — | REJECT | — | — | AERON installer is M11 and native |

---

## 5. JARVIS Local (assumed `LYiHub/pub-local-jarvis`, MIT)

| ID | Component | Source | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| PJ-01 | Model downloader | `src/jarvis_backend/model_download.py` · `download_models` (130); `huggingface_snapshot_download` (13); `model_files_are_valid` (70) | `tests/unit/test_model_download.py` | REJECT | — | — | Downloads model weights (C-04). Mirror fallback |
| PJ-02 | Native omni runtime | `native/src/omni_runtime.cpp`; `native/include/jarvis/runtime.hpp` | — | REJECT | — | — | Local multimodal inference |
| PJ-03 | Local model configuration | `config/default.toml` (`model_path = "models/MiniCPM-o-4_5-gguf"`) | — | REJECT | — | — | Local GGUF model |
| PJ-04 | Lifecycle state machine | `src/jarvis_backend/orchestrator/lifecycle.py` · `LifecycleState`, `Lifecycle`, `InvalidTransition` | — | UNVERIFIED | Possible reference for `AppLifecycleCoordinator` | — | Located, not read in full. AERON's coordinator already has tested transitions, so low priority |
| PJ-05 | Raw-data non-retention principle | README "隐私与数据" (privacy and data) section | — | REFERENCE ONLY | Privacy policy for perception data (future) | Later | Principle only. Implementation not reviewed |

---

## 6. isair/jarvis (custom non-commercial licence)

| ID | Component | Source | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| IJ-01 | All source code | `src/**` | — | REJECT | — | — | Non-commercial licence. Commercial use needs separate licence. Copying is not permitted |
| IJ-02 | Local model tiers | `src/jarvis/config.py` (Gemma, Qwen tiers; Ollama pull-name comment) | — | REJECT | — | — | Local models (C-03/C-04) |
| IJ-03 | OpenAI-compatible client | `src/jarvis/llm/openai_compatible.py` | — | REJECT (code) | Equivalent design in A1 | M3 | Licence prevents use. Equivalent design comes from sir-thaddeus A1 (Apache-2.0) |
| IJ-04 | Faster-whisper, Chatterbox, Piper voice stack | `requirements.txt` | — | REJECT | — | M8 | Local models and model downloads |

---

## 7. raydeStar/sir-thaddeus-2 (no licence)

| ID | Component | Source (file · item) | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| T2-01 | Approval digest binding | `Thaddeus.Core/Contracts.cs` line 27 (`Approval` record); `Thaddeus.Infrastructure/CapabilityBroker.cs` ~189; `ConnectedToolConversation.cs` 54, 70 (`ConnectedApprovalDigest`) | — | REFERENCE ONLY | `ApprovalEngine` payload binding (existing) | M2 (verify) | Design idea: digest over run ID, approval ID, exact action, resource version, expiry. AERON already binds exact parameters. Compare, do not copy |
| T2-02 | Approved action not consumed | `ConnectedToolConversation.cs` 86–100 (checks `Decision == "approved"`); `Runtime.cs` 906 (`ReserveTool` increments counter only) | — | REJECT (behaviour) | `ApprovalEngine.TryConsume` | M2 (done) | Defect. A second dispatch of the same approved action is not blocked by the approval itself |
| T2-03 | Single-use pairing codes with hashed storage | `Thaddeus.Host/Security.cs` 37–69 (`Pairs`, `Used`, `CodeHash`) | — | REFERENCE ONLY | Owner device pairing (future, D6) | Deferred | Hash-at-rest and single-use idea. Owner MFA design still blocked (D2-01) |
| T2-04 | Exact read-back hash receipts | `docs/REUSE_LEDGER.md` (cites v1 `PR #329`, not re-verified) | — | REFERENCE ONLY | Verified-effect receipt design | M5/M7 | Write, read back, hash, compare. Evidence is v1 and self-reported |
| T2-05 | Validator exception is not a pass | `docs/REUSE_LEDGER.md` row "Validator exception treated as pass" | — | REFERENCE ONLY | Fail-closed principle | All | Principle. Adopt as policy |
| T2-06 | Bounded repair loop | `docs/REUSE_LEDGER.md` (cites v1 `CompletionValidator.cs`) | — | UNVERIFIED | Output checks | M5+ | Ledger cites v1 files. Not located in v2 source |
| T2-07 | Session cookies with CSRF | `Thaddeus.Host/Security.cs` 17–25 | — | N/A | — | — | AERON has no web UI. Not applicable |
| T2-08 | Tool-call budget | `Runtime.cs` 906 | — | REFERENCE ONLY | Budget per run | M7 | Counter only. Not an approval |
| T2-09 | Source-linked memory | `docs/SOURCE_LINKED_MEMORY.md` (cited in ledger) | — | UNVERIFIED | `Aeron.Memory` provenance | M5 | Not read |

---

## 8. be1st6666/guarded-computer-use-mcp (MIT, JavaScript)

| ID | Component | Source (file · item) | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| GC-01 | Per-record hash-chained audit | `src/audit.js` · `ensureChain` (lines 113 onwards), `prev`/`hash` fields, `GENESIS` | `test/` folder; `npm run audit:verify` | REFERENCE ONLY | Compare with `DurableAuditLedger` | — | AERON's ledger already has checkpoints and truncation codes. This chain is per segment and self-rewritable |
| GC-02 | Sensitive argument redaction | `src/audit.js` · `DEFAULT_REDACT_KEYS`, `redactArgs`, `redactScalar` | — | ADAPTATION | `RedactingLogger` / `RedactionFilter` | M2 follow-up | Replaces values with `{redacted, chars, sha256[:12]}`. **Do not copy the unkeyed digest.** Short values are brute-forceable (SL-05). Use keyed HMAC or omit the digest |
| GC-03 | Approval gate lock and physical-input filter | `src/approval.js` (gate, `blockedWhileApprovalPending`); `approval.ps1` (`-BlockInjected`) | — | REFERENCE ONLY | Approval UI design | M2/M5 | Identified a real race: a parallel tool call could answer the dialog. AERON's WPF approval must be bound to an Owner session, not synthetic input. Windows behaviour UNVERIFIED |
| GC-04 | Signed switch markers with counter | `src/guard.js` · HMAC over `v1|name|off|at|n` (line 144); replay detection (204, 266–279) | — | REFERENCE ONLY | Signed safety switches (future) | M5+ | Replay defence with counter. Watermark is rollback-able (admitted in the code) |
| GC-05 | Deny-list and launch-list policy | `src/policy.js`; `policy.json` | — | REFERENCE ONLY | Policy data for M7 | M7 | Deny lists ignore confirm, a useful rule |
| GC-06 | Per-segment audit rotation | `src/audit.js` (rotation at `maxBytes`) | — | REJECT (as a design) | — | — | Each segment starts a new chain. Truncation of a whole segment is undetectable without checkpoints |

---

## 9. ermoluk/mcp-warden (Apache-2.0, Rust, README level)

| ID | Component | Source | Tests | Class | AERON target | Milestone | Condition / blocker |
|---|---|---|---|---|---|---|---|
| WD-01 | Secrets never in agent context | README (stated) | — | UNVERIFIED | M7 credential design | M7 | Consistent with AERON C-12. Verify in code before use |
| WD-02 | Per-call allow-lists | README (stated) | — | UNVERIFIED | M7 tool policy | M7 | Consistent with exact-argument binding |
| WD-03 | Native dialog or Telegram approval | README (stated) | — | REJECT (Telegram) | `ApprovalEngine` UI | M5+ | Telegram approval channel rejected |
| WD-04 | Append-only audit including refused calls | README (stated) | — | UNVERIFIED | Audit scope | M7 | Refused calls must be audited in AERON |
| WD-05 | OS keychain storage | README (stated) | — | REFERENCE ONLY | Owner vault (D6) | Deferred | AERON uses DPAPI/ACL path, Owner decision |

---

## 10. Discovery-only candidates

| ID | Candidate | Class | Note |
|---|---|---|---|
| PA-01 | permission-aware-mcp: single-use, args-bound, expiring tokens | UNVERIFIED / REFERENCE ONLY | Closest to AERON's model. Read description only. Licence not checked |
| OA-01 | OpsAgent: write-ahead tool-call journal, crash-injection tests | UNVERIFIED / REFERENCE ONLY | Journal-before-execute and crash tests are good practice. Paid Anthropic dependency excluded |
| EN-01 | engram: Markdown source of truth, rebuildable FTS5 index | UNVERIFIED / REFERENCE ONLY | Index is disposable. Good for M5 design |
| AM-01 | a-memory: 4-tier memory, KG, NaCl SecretBox for auth only | REJECT | Memory data plaintext |
| SG-01 | sqgraph: temporal facts with validity and history | UNVERIFIED / REFERENCE ONLY | 0 stars. Concept of superseded facts kept as history is useful |
| DF-01 | DragonForge-MCP: Phase 0 proposal | REJECT (component) | No implementation. Design ideas UNVERIFIED |
| MG-01 | mcp-guardian (Python) | REJECT | No licence, created 2026-11-25 |
| OR-01 | OmniRoute (KooshaPari, MIT) | EXTERNAL GATEWAY | Not a component. C-05 and D7. Identity to be confirmed (3 repositories share the name) |

---

## 11. Cross-cutting dependencies (libraries)

| ID | Dependency | Licence | Source of reference | Class | Note |
|---|---|---|---|---|---|
| LB-01 | PdfPig | Apache-2.0 | sir-thaddeus | ADAPTATION (A5) | Via NuGet, pinned, reviewed |
| LB-02 | DocumentFormat.OpenXml (Open-XML-SDK) | MIT | sir-thaddeus | ADAPTATION (A5) | Via NuGet |
| LB-03 | Cronos | MIT | sir-thaddeus | ADAPTATION (A13) | Via NuGet |
| LB-04 | Serilog | Apache-2.0 | sir-thaddeus | Later (A12) | Behind `RedactingLogger` |
| LB-05 | AngleSharp, HtmlAgilityPack | MIT | sir-thaddeus | ADAPTATION (A6) | M7 |
| LB-06 | ModelContextProtocol C# SDK | **NOASSERTION (GitHub API)** | sir-thaddeus | **UNVERIFIED** | Licence must be read from the pinned package and file before any use (A7 gate) |
| LB-07 | Playwright (.NET) | MIT | sir-thaddeus | UNVERIFIED for runtime | Browser binary download must be disabled or approved (SL-07) |
| LB-08 | KokoroSharp | MIT | sir-thaddeus | REJECT | Downloads model weights |
| LB-09 | SQLitePCLRaw | Apache-2.0 (per sir-thaddeus) | sir-thaddeus | Transitive not verified | Already used by AERON; check version and notice |

---

## 12. Summary

- **DIRECT ADOPTION:** none qualifies today.
- **Closest to direct adoption:** ST-22 (document truncation), a small self-contained helper. It still requires re-authoring and tests in AERON.
- **Adaptation** is the main route: environment allowlist (MW-05), memory schema (ST-29, SC-16), document readers (ST-28), request shapes (ST-27), redaction patterns (ST-13).
- **Rejected** rows outnumber adopted rows. Most rejections come from local-model dependence, licence, or a security-invariant failure listed in SECURITY-AND-LICENSING-REVIEW §2.
- Per-class totals are not stated, because many rows carry a condition and a single class per row is not always clean.
