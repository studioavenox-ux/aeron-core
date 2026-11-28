# AERON — Repository Comparison Matrix

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. Facts are from GitHub metadata, source reads, and the tarballs noted in [MULTI-REPOSITORY-RESEARCH.md §2](MULTI-REPOSITORY-RESEARCH.md). Nothing was built or run.
- **Time saved** figures are engineering judgement, not measurement. They are relative (Low / Medium / High) and mean "how much of the AERON work this repository could cover if its component were usable as-is". Each is qualified in the notes.

Fields per repository: URL · licence · maintenance · languages · architecture · modules read · dependencies · security model · Windows · remote-inference compatibility · cost · integration complexity · AERON overlap · time saved · limitations.

---

## 1. raydeStar/sir-thaddeus (audited at `845b50c`)

- **URL:** https://github.com/raydeStar/sir-thaddeus
- **Licence:** Apache-2.0 (licence API and `LICENSE`). No NOTICE file.
- **Maintenance:** Active at audit time. ~219 test files under `tests/SirThaddeus.Tests`, not executed.
- **Languages:** C# (.NET 10, `global.json` pins SDK 10.0.103), React/TypeScript (`web/`), Python (`voice-backend`).
- **Architecture:** Photino desktop shell, Kestrel loopback runtime with bearer-token API, WebSocket, IPC, single-agent pipeline, JSON settings, JSONL audit.
- **Modules read:** `ToolPermissionGate`, `InMemoryPermissionBroker`, `EnforcingToolRunner`, `ToolExecutionContext`, `AuditedMcpToolClient`, `RuntimeMcpEnvironmentBuilder`, `StdioMcpToolClient`, `SqliteMemoryStore`, `SchemaInitializer`, `LmStudioClient`, `LlmClientFactory`, `CodexCliLlmClient`, document readers, `DocumentTruncator`, `PromptInjectionGuard`, `RoutinesApi`, `JsonFileRoutineStore`, `SettingsManager`, `AppSettings`, `JsonLineAuditLogger`, `RuntimeBearerAuthMiddleware`, voice backend (`requirements.txt`, `server.py`, `VoiceBackendSupervisor`), tray and hotkey adapters, `ParentProcessWatcher`.
- **Dependencies:** Photino.NET, Serilog, PdfPig, SQLitePCLRaw, Cronos, NCalc, AngleSharp, HtmlAgilityPack, Open-XML-SDK, Playwright, KokoroSharp, ModelContextProtocol (NOASSERTION, see Security review).
- **Security model:** Permission tiers Off / Ask / Always per tool group. Token-bearing tool calls. Bearer auth for runtime API. Regex-based redaction (9 patterns in `AuditSensitiveDataScrubber`). PromptInjectionGuard is marker-based.
- **Windows:** Native Windows target. Photino shell is web-based, which is outside AERON's WPF rule.
- **Remote-inference compatibility:** Partial. `LmStudioClient` is local. It also auto-loads models through `POST /v1/models/load` when no HttpClient is injected, a remote mutation risk. `CodexCliLlmClient` spawns a process. Only the OpenAI-compatible request shape is reusable.
- **Cost:** Apache-2.0, zero licence cost. Upstream NuGet packages carry their own licences (see Security review).
- **Integration complexity:** High as a whole. Medium for selected modules (LLM request shapes, SQLite memory schema, readers).
- **AERON overlap:** High in principle. Overlaps with AERON on permissions, audit, settings, logging, tray, lifecycle, memory, MCP, LLM, readers. AERON's kernel already covers most of the security-critical overlap.
- **Time saved:** Medium. Mainly for document readers, memory schema, and OpenAI-compatible request shapes. Security-critical parts must be rewritten regardless.
- **Limitations:** Local-model-centric. Simulation stubs (`SystemCommandTool`, `FileReadTool`). Permission broker does not consume tokens. Path check is prefix-only. API keys passed to child processes. Model download in voice and KokoroSharp. Upstream `global.json` SDK pin conflicts with AERON's 10.0.100 rollForward.
- **Verdict:** Selective adoption of named modules. No whole-repo adoption. See [SIR-THADDEUS-ADOPTION-AUDIT.md](SIR-THADDEUS-ADOPTION-AUDIT.md).

---

## 2. SenClaw/senclaw

- **URL:** https://github.com/SenClaw/senclaw (Cargo workspace; crate `senclaw` 0.2.15)
- **Licence:** MIT (licence API).
- **Maintenance:** Created 2026-09-26, last push 2026-11-03, 1 star, 0 issues. Young project with a very large surface (738 `.rs` files, ~599 `.md`, a 171 KB `CLAUDE.md`).
- **Languages:** Rust (daemon, 52 `src/` modules). Flutter desktop app (not read). Chrome extension (not read). Tree-sitter for multi-language code analysis.
- **Architecture:** Rust daemon exposing an axum HTTP/WebSocket gateway, MCP servers as in-process modules (`mcp/*_server.rs`, ~46 files), SQLite via bundled rusqlite, teloxide Telegram channel, portable-pty for terminals.
- **Modules read:** `security/egress.rs`, `security/replication.rs`, `security/scan.rs`, `agent/permission_bridge/bridge.rs` (1,219 lines) and `types.rs`, `agent/agent_pool/types.rs`, `sandbox/backend/direct_windows.rs` (1,648 lines), `sandbox/backend/winproc.rs` (1,059 lines), `docs/sandbox-windows-research.md`, `.github/workflows/sandbox-windows.yml`, `tools/bash.rs`, `mcp/client.rs`, `scheduler/task_scheduler.rs`, `scheduler/executor.rs`, `memory/schema.rs`, `memory/fts_search.rs`, `memory/embedding_providers.rs`, `memory/cognitive/gnn_sage.rs`, `decision/`, `browser_agent/policy.rs`. Enumerated `local_models/`, `browser/`, `control_plane/`, `gateway/`.
- **Dependencies (Cargo):** tokio, axum 0.7, rusqlite (bundled), reqwest (rustls), teloxide, portable-pty, mdns-sd, aes-gcm, tree-sitter, cron, zip.
- **Security model:** Egress guard (self-replication and fan-out rules). Plugin and skill scanner with severity policy. Permission bridge with auto-accept rules, deny rules, and an accept-all flag. Windows AppContainer and Job Object backend (unverified). Bubblewrap and Docker backends on other OSes. Banned-command first-word denylist for bash. No hash-chained audit (zero hits).
- **Windows:** Designed for Windows through `direct_windows.rs`. Per its own research note, the backend has never run on real Windows. Fifteen review fixes were applied on macOS. A workflow exists to run it on `windows-latest`. Local loopback is blocked inside AppContainer, so `execLoopback` and site-allowlist proxies do not work on Windows.
- **Remote-inference compatibility:** Mixed. OpenAI and OpenRouter embedding providers are remote. Ollama and a local GGUF runtime (`LocalProvider`) are local. `local_models/` (MLX, GGUF) is local. The GNN model is saved to and loaded from disk.
- **Cost:** MIT, zero licence cost. Rust toolchain would be a new build dependency for AERON, which is not acceptable as-is.
- **Integration complexity:** Very high as a runtime (Rust daemon, Flutter app, Chrome extension). Medium for individual ideas, which must be re-authored in C#.
- **AERON overlap:** High in the security, sandbox, scheduler, memory, and MCP areas. Also overlaps with the M7 execution scope.
- **Time saved:** Low to Medium. The Windows sandbox design and its fix list are the main asset. Most of the rest needs re-authoring or rejection.
- **Limitations:** Very young and single-author-looking. No audit hash chain. Auto-accept rules bypass per-action approval. `skipAllAgentsPermissions` is a global bypass. Scheduled `script` tasks run shell with no approval. Environment inherited by MCP children. Egress-guard weights are uncalibrated (`replication.rs` header says so explicitly).
- **Verdict:** REFERENCE ONLY for security design. Rust runtime is not compatible. Local model subsystems are excluded.

---

## 3. kailiang0120/monaw

- **URL:** https://github.com/kailiang0120/monaw
- **Licence:** Apache-2.0 (README badge and `LICENSE` file, which contains the full Apache text). Licence API also reports Apache-2.0.
- **Maintenance:** Created 2026-05-19, last push 2026-11-27, 89 stars, 6 forks, 1 open issue. Most recently active of the primary repositories.
- **Languages:** Python 3.11 backend (~110k lines including tests; `app/agent/` alone ~77k lines). Electron and React frontend (not read). PowerShell and batch installers.
- **Architecture:** FastAPI backend bundled with PyInstaller for the installer. LangChain 1.2.15 and LangGraph. aiosqlite. JSON and Markdown approval logs. Chrome extension over a local WebSocket.
- **Modules read:** `agent/approval_broker.py` (571 lines), `agent/action_security/approval_binding.py`, `agent/action_security/models.py` (partially), `agent/sandbox/environment.py`, `agent/sandbox/capabilities.py` (partially), `agent/chatgpt_account.py` (DPAPI portion), `docs/permissions.md`. Located but not read in full: `agent/approval_review.py`, `agent/scheduler.py`, `agent/llm_provider_*`, blocked-root enforcement, `agent/sandbox/backend*`.
- **Dependencies:** langchain, langchain-core, langgraph, openai, google-genai, mcp, scrapling, pywinauto and pywin32 (Windows only), comtypes, mss, psutil, croniter, python-telegram-bot, composio-client, tavily-python, tiktoken, pyjwt.
- **Security model:** Four permission modes (`default`, `full_access`, `auto_review`, `custom`). Approval tickets with payload hash and expiry. Action classifier with read-only hints. Blocked roots and processes. Allowlist-first environment construction with secret-key and code-loading denylists. Sandbox probe before claiming availability. `auto_review` uses a tool-free LLM call to approve or deny.
- **Windows:** Windows-first. DPAPI (`win32crypt.CryptProtectData`) for credential storage. Blocked roots default to `C:\Windows`, `C:\Program Files`, `%USERPROFILE%\AppData`, and similar. pywinauto for UI automation.
- **Remote-inference compatibility:** Good. Uses OpenAI and Google Gemini APIs. The "ChatGPT account" connection signs in to a consumer ChatGPT subscription, which is not an API key. Voice input needs a paid API key.
- **Cost:** Apache-2.0, zero licence cost. Runtime depends on paid provider APIs (acceptable only as the Owner-approved provider path) and on Telegram and Composio, which are not approved.
- **Integration complexity:** Very high as a runtime (LangChain and LangGraph, PyInstaller, Electron). Medium for individual patterns, which must be re-authored in C#.
- **AERON overlap:** High in approvals, action security, environment handling, sandbox probing, and permission modes.
- **Time saved:** Medium. The approval lifecycle states and the environment allowlist are directly reusable as patterns. Most of the application would be dead weight in AERON.
- **Limitations:** Approval ticket store is a JSONL file rewritten in full. `clear_all_tickets()` deletes approval history. No hash chain. One-use binding is an in-memory set, so it resets on restart. `full_access` removes the approval prompt. `auto_review` lets an LLM approve actions. LangChain is banned by AERON's `ArchitectureTests`.
- **Verdict:** REFERENCE ONLY for approval lifecycle and environment handling. ADAPTATION for the environment allowlist. REJECT for auto-review, full access, and the LangChain stack.

---

## 4. JARVIS Local — assumed LYiHub/pub-local-jarvis

- **URL:** https://github.com/LYiHub/pub-local-jarvis (identity assumed; see [MULTI-REPOSITORY-RESEARCH.md §7](MULTI-REPOSITORY-RESEARCH.md))
- **Licence:** MIT (licence API and `LICENSE`).
- **Maintenance:** Created 2026-07-24, last push 2026-07-24, 524 stars, 101 forks, 8 issues. Last push date equals creation date, so the repository appears to be a single snapshot. Treat as a snapshot.
- **Languages:** Python (`src/jarvis_backend`, ~5,000 lines), native C++ (`native/src/omni_runtime.cpp`), PowerShell (`start-real.ps1`, ~51 KB), GPU verification scripts.
- **Architecture:** FastAPI control plane. Native multimodal runtime for screen and audio. Desktop pet UI. Orchestrator with a lifecycle state machine. Local memory store.
- **Modules read:** `model_download.py` in full. `settings.py` and `config/default.toml` for model path. `README.md`, `PROJECT_OVERVIEW.md`, `pyproject.toml`. `orchestrator/lifecycle.py` located, not read in full.
- **Dependencies:** fastapi, pydantic, uvicorn, huggingface-hub, hf-xet, tqdm. Optional `anthropic` extra, not imported anywhere in `src/`.
- **Security model:** Privacy mode (pause perception by double-click). Raw screen and audio are not persisted long-term, per README. Model verification uses marker files and optional hash checks.
- **Windows:** Windows-only in practice (GPU runtime scripts, PowerShell launcher).
- **Remote-inference compatibility:** **None for inference.** The only network calls are Hugging Face model downloads, with a China mirror fallback (`hf-mirror.com`). The optional image-generation API is for schedule images only and is off by default.
- **Cost:** Zero licence cost. Requires local GPU and model weights. Violates the no-local-model rule.
- **Integration complexity:** Not applicable. Rejected.
- **AERON overlap:** Low to medium. Overlaps with perception, companion UX (out of scope for AERON), and local memory.
- **Time saved:** None. Its core capability is excluded.
- **Limitations:** Downloads model weights by design (`snapshot_download`). Local GGUF model (`models/MiniCPM-o-4_5-gguf`). Native runtime needs GPU.
- **Verdict:** REJECT.

---

## 5. isair/jarvis

- **URL:** https://github.com/isair/jarvis
- **Licence:** "Jarvis AI Assistant License", copyright Baris Sencan. Free for non-commercial use. Any commercial use requires a separate licence from the copyright holder. GitHub reports NOASSERTION. **AERON is a commercial product, so this licence blocks any code reuse.**
- **Maintenance:** Created 2025-08-19, last push 2026-11-19, 2,442 stars, 547 forks, 191 issues. Most popular repository in this study.
- **Languages:** Python (~50,000 lines in `src/`), Playwright, Flask.
- **Architecture:** Desktop application with voice listening, transcript buffer, memory graph, reply engine, dictation, MCP catalogue, OpenAI-compatible LLM client, faiss vector index.
- **Modules read:** `LICENSE` in full. `requirements.txt`. `src/jarvis/config.py` (local model tiers). `src/jarvis/llm/openai_compatible.py` (located, not copied). Local-model references counted across 34 files.
- **Dependencies:** faster-whisper, chatterbox-tts, piper-tts, faiss-cpu, pygame, mcp 1.13.1, playwright, pynput, sounddevice, webrtcvad, CUDA libraries for Windows.
- **Security model:** Not reviewed in depth. Local-first design claim.
- **Windows:** Yes. CUDA and Windows-specific dependencies.
- **Remote-inference compatibility:** Default tiers are local (Ollama-style pull names such as `google/gemma-4-E2B-it` and `Qwen/Qwen3.5-0.8B`, with a comment that the fast tier pull-name only exists on Ollama). An OpenAI-compatible client exists, so a remote path is possible.
- **Cost:** Non-commercial licence. Commercial licence cost undefined.
- **Integration complexity:** Not applicable because of licence.
- **AERON overlap:** Medium: voice, memory graph, reply engine, MCP.
- **Time saved:** None for code. Concept reading is allowed for understanding, but not for copying.
- **Limitations:** Non-commercial licence. Local model and voice-model downloads. Very large surface.
- **Verdict:** REJECT (licence blocks reuse; local-model dependencies also fail C-03/C-04).

---

## 6. raydeStar/sir-thaddeus-2

- **URL:** https://github.com/raydeStar/sir-thaddeus-2 (default branch `master`)
- **Licence:** **None.** No licence file or metadata. Under default copyright law this means no permission to copy, modify, or distribute. Reading for understanding is not a licence grant.
- **Maintenance:** Created 2026-09-09, last push 2026-09-29. Self-described "preview, not production-ready or security-audited".
- **Languages:** C# (ASP.NET Core 10, `src/` ~18,873 lines), React/TypeScript (`web/`), SQLite.
- **Architecture:** Host (ASP.NET Core) with `Thaddeus.Core`, `Thaddeus.Infrastructure`, `Thaddeus.Host`, `Thaddeus.Notifications`. Optional isolated worker for research tasks. Browser/PWA client.
- **Modules read:** `Thaddeus.Core/Contracts.cs` (line 27, `Approval` record), `Thaddeus.Infrastructure/CapabilityBroker.cs` (around line 189), `ConnectedToolConversation.cs` (lines 54, 70, 80–100), `Runtime.cs` (`ReserveTool`, line 906), `Thaddeus.Host/Security.cs` (pairing and sessions), `docs/REUSE_LEDGER.md`, `README.md`, `AGENTS.md`.
- **Dependencies:** ASP.NET Core 10, SQLite. Remaining NuGet list not read.
- **Security model:** Digest-bound approvals (run, approval ID, exact action, resource version, expiry). Hashed pairing codes with single use. HttpOnly same-origin sessions with CSRF. Budget counters per run. No hash-chained audit.
- **Windows:** Yes (Windows preview).
- **Remote-inference compatibility:** Mixed. The README describes a provider called "Luna High" through a separate development bridge whose credentials are excluded from packages. The source quickstart uses a simulated provider.
- **Cost:** Unknown.
- **Integration complexity:** Not applicable without a licence. Ideas only.
- **AERON overlap:** High in approvals, pairing, and provenance.
- **Time saved:** Low. The approval digest design is worth re-deriving. Most of the rest is web-specific.
- **Limitations:** No licence. Approvals are not consumed. `ReserveTool` only increments a counter, so an approved connected action can be re-dispatched. The README states it is not security-audited.
- **Verdict:** REFERENCE ONLY for design ideas. No code reuse without an explicit licence from the copyright holder, which must be requested from the owner, not assumed.

---

## 7. be1st6666/guarded-computer-use-mcp

- **URL:** https://github.com/be1st6666/guarded-computer-use-mcp
- **Licence:** MIT (licence API).
- **Maintenance:** Created 2026-09-09, last push 2026-09-15, 5 stars. Short active period.
- **Languages:** JavaScript (`src/` 2,094 lines plus `server.js`), PowerShell (`approval.ps1`, `host.ps1`, `guard-panel.ps1`), Python (`ocr_rapid.py`).
- **Architecture:** MCP server with 26 Windows computer-use tools. Approval dialog as a PowerShell WinForms window with low-level input hooks.
- **Modules read:** `src/audit.js` (512 lines), `src/approval.js` (257 lines), `src/guard.js` (409 lines, partially). `policy.json`. `test/`, `test-policy.mjs`, `test-typing.mjs` located.
- **Dependencies:** Node.js, npm packages (see `package-lock.json`). Windows PowerShell.
- **Security model:** Policy deny-lists for processes and window titles. Approval gate locks input-injecting tools while a dialog is open. Gate accepts only physical input for the Allow decision. Signed guard switches (HMAC-SHA256 with monotonic counter). Hash-chained audit with redaction of sensitive argument keys.
- **Windows:** Windows-only.
- **Remote-inference compatibility:** Not applicable. No model inference.
- **Cost:** Zero.
- **Integration complexity:** Not applicable as code (JavaScript and PowerShell). Patterns only.
- **AERON overlap:** High in approvals, audit, and computer-use safety (M7 scope).
- **Time saved:** Low to medium, as design input for M5/M7 guard patterns.
- **Limitations:** Audit chain is tamper-evident, not tamper-proof. Rotated segments start new chains. The guard watermark can be rolled back by whoever can write it (the code says so). Redaction uses an unkeyed SHA-256 prefix, which can be brute-forced for short values such as PINs (see Security review). Tests are present but not run.
- **Verdict:** REFERENCE ONLY.

---

## 8. ermoluk/mcp-warden

- **URL:** https://github.com/ermoluk/mcp-warden
- **Licence:** Apache-2.0 (licence API).
- **Maintenance:** Created 2026-10-25, last push 2026-11-10, 14 stars.
- **Languages:** Rust. Read at README and metadata level only.
- **Architecture:** Local credential broker for MCP clients. Secrets stored in OS keychain. Named presets for HTTP, SSH, SFTP, and SQL. Native approval dialog, or Telegram.
- **Security model (as stated):** Secrets never returned to the agent, except `get_secret_for_env`, which always asks first. Per-call allow-lists (HTTP method and path, SSH command regex). Local append-only SQLite audit that includes refused calls.
- **Windows:** Claimed native dialog on macOS and Windows, notification on Linux. Not verified.
- **Remote-inference compatibility:** Not applicable.
- **Cost:** Zero.
- **Integration complexity:** Not applicable as code. Patterns only.
- **AERON overlap:** High for M7 tool credentials and per-call scope.
- **Time saved:** Low. Design input only.
- **Limitations:** Read at README level, so every claim is `UNVERIFIED`. Telegram approval channel is not acceptable for AERON.
- **Verdict:** REFERENCE ONLY (UNVERIFIED).

---

## 9–16. Discovery-only candidates

| Repository | Licence | Created / pushed | Stars | Summary | Verdict |
|---|---|---|---|---|---|
| Abusalman-alhaj/mcp-guardian (Python) | None | 2026-11-25 / 2026-11-25 | 0 | HITL MCP proxy for Windows, JSONL audit | REJECT: no licence, days old |
| djames1987/DragonForge-MCP (Python) | Not checked | — | — | Phase 0 local MCP gateway. States "no implementation has been qualified". Fail-closed, deny-by-default, trusted-tool lifecycle | REJECT as component. Design ideas UNVERIFIED |
| Mohammmed-Waleed/OpsAgent-AI- (Python) | Not checked | — | — | Agent with write-ahead tool-call journal in SQLite, approval steps, crash injection tests | REFERENCE ONLY (UNVERIFIED). Depends on the paid Anthropic API, which is excluded |
| TanvirIslam-BD/permission-aware-mcp (Python) | Not checked | — | — | Single-use, args-bound, expiring approval tokens; LLM never receives the approve tool | REFERENCE ONLY (UNVERIFIED). Closest match to AERON's approval model |
| Cipher208/a-memory (Python) | MIT | 2026-06-28 / 2026-11-22 | 1 | 4-tier SQLite memory, FTS5 plus binary hybrid, knowledge graph, NaCl SecretBox for auth secrets only | REFERENCE ONLY. Memory data is plaintext SQLite, which conflicts with AERON's `DataClassification` |
| utsapoddar/engram (Python) | MIT | 2026-07-29 / 2026-09-26 | 4 | Markdown as source of truth, SQLite FTS5 index rebuilt from it, optional local embeddings, no network at recall | REFERENCE ONLY (UNVERIFIED). Rebuildable-index principle is useful |
| Fukuro99/sqgraph (Python) | MIT | 2026-10-16 / 2026-10-16 | 0 | Temporal property graph on SQLite plus sqlite-vec, facts with validity periods and superseded history | REFERENCE ONLY (UNVERIFIED). Too new; memory LLM is OpenAI |
| KooshaPari/OmniRoute (TypeScript) | MIT | 2026-05-01 / 2026-11-20 | 18 | Multi-provider AI gateway with OpenAI, Claude, Gemini, and Responses API translation at `/v1`, fallback, MCP, A2A | **EXTERNAL GATEWAY. Not a component.** Keep as OmniRoute per C-05 and D7. Its marketing of "free" and "keyless" providers must be constrained by the ₹0 allowlist |

## 10. Cross-repository summary

| Dimension | Best reviewed | Worst reviewed | Note |
|---|---|---|---|
| Approval authority | AERON (existing) | Monaw auto-review, SenClaw auto-accept | No candidate matches AERON |
| Single-use approval | permission-aware-mcp (UNVERIFIED) | Monaw in-memory set | Monaw's is process-local |
| Audit tamper evidence | AERON `DurableAuditLedger` | sir-thaddeus JSONL (no hash) | guarded's chain is second, per segment |
| Secret isolation | Monaw allowlist env | SenClaw MCP and sir-thaddeus MCP (inherit or pass secrets) | Defects found |
| Windows isolation | SenClaw AppContainer design (UNVERIFIED) | none verified | No Windows run exists |
| Remote-only inference | Monaw (OpenAI, Gemini) | JARVIS Local (HF download) | |
| Licence cleanliness | sir-thaddeus, Monaw, SenClaw (permissive) | sir-thaddeus-2 (none), isair (non-commercial) | |
| Memory fit | sir-thaddeus schema (FTS5, secret exclusion) | a-memory (plaintext) | U-7 still open |
