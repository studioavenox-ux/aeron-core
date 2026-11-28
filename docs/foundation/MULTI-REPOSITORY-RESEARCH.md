# AERON — Multi-Repository Foundation Research

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. Research and assessment only. No upstream code was imported, copied, compiled, or executed. No dependency was added. No existing AERON implementation was changed. No milestone was started.
- **Branch:** `arena/64f6701f-aeron-core` (base `33bbd6a`, `main`). Draft PR #8 is not for merge.
- **Companion documents:** [REPOSITORY-COMPARISON-MATRIX.md](REPOSITORY-COMPARISON-MATRIX.md) · [COMPLETE-COMPONENT-ADOPTION-MATRIX.md](COMPLETE-COMPONENT-ADOPTION-MATRIX.md) · [SECURITY-AND-LICENSING-REVIEW.md](SECURITY-AND-LICENSING-REVIEW.md) · [ARCHITECTURE-INTEGRATION-PLAN.md](ARCHITECTURE-INTEGRATION-PLAN.md) · [REPLACEMENT-AND-MIGRATION-PLAN.md](REPLACEMENT-AND-MIGRATION-PLAN.md) · [AERON-FOUNDATION-ROADMAP.md](AERON-FOUNDATION-ROADMAP.md)
- **Prior work this set extends:** [SIR-THADDEUS-ADOPTION-AUDIT.md](SIR-THADDEUS-ADOPTION-AUDIT.md), [MIGRATION-MAP.md](MIGRATION-MAP.md), [ADR-FOUND-001-PROPOSED.md](ADR-FOUND-001-PROPOSED.md). The Sir Thaddeus row IDs (A1–A13, R1–R13, D1–D8, X1–X8) are reused unchanged.

## 1. Scope

The directive asks for the strongest open-source components that would accelerate AERON. The selection criterion is engineering quality against AERON's non-negotiables. Feature count is not a criterion.

Non-negotiables applied to every candidate: native Windows; remote-only inference; no local models and no weight downloads; no paid API fallback; OmniRoute preserved; AERON-owned security and authority; default-deny; durable single-use approvals; hash-chained audit; Owner authentication and MFA; existing security architecture tests.

Starting repositories: Sir Thaddeus, SenClaw, Monaw, and "JARVIS Local". Other repositories were discovered and assessed where they were relevant.

Research areas in the directive: personal assistants, multi-agent systems, orchestration, long-term memory, knowledge and RAG, document processing, MCP, browser and desktop automation, tool execution, permissions and approvals, voice, STT/TTS, provider abstraction, OmniRoute-compatible integrations, background services, scheduling, monitoring, native Windows desktop.

## 2. Method and evidence rules

1. **Source over README.** Each candidate was downloaded as a tarball from `codeload.github.com` and extracted to `/tmp/research`. That path is ephemeral and is not committed. Security-relevant files were read directly. README and marketing text were used only to locate code.
2. **Licence.** The GitHub licence API was checked, and the `LICENSE` file was read where it was reachable. Where they disagree or the licence is absent, the status is stated, not inferred.
3. **No execution.** Nothing was compiled, installed, or run. No `dotnet`, `pwsh`, Node, or Python toolchain is available in the sandbox. No upstream test suite was executed. Test statements in these documents mean "the test file exists", not "the test passes".
4. **Local-model dependency.** Checked by searching for model runtimes (Ollama, llama.cpp/GGUF, MLX, ONNX, Whisper, Piper, Kokoro, MiniCPM) and model-download calls (`snapshot_download`, `from_pretrained`, model downloader classes), then by reading each call site.
5. **Classification.** Defined in [COMPLETE-COMPONENT-ADOPTION-MATRIX.md §1](COMPLETE-COMPONENT-ADOPTION-MATRIX.md).
6. **Discovery-only items.** Candidates seen only in search results or repository descriptions are labelled `UNVERIFIED`. They are not used as evidence for any adoption decision.

## 3. Repositories reviewed

| # | Repository | Licence (API / file) | Language | Last push | Role in this study | Read depth |
|---|---|---|---|---|---|---|
| 1 | [raydeStar/sir-thaddeus](https://github.com/raydeStar/sir-thaddeus) @ `845b50c` | Apache-2.0 | C# (.NET 10) | audited at `845b50c` | Primary | Security, memory, tools, MCP, LLM, voice, readers, scheduling |
| 2 | [SenClaw/senclaw](https://github.com/SenClaw/senclaw) | MIT | Rust (+ Flutter app, not read) | 2026-11-03 | Primary | Security, permission bridge, Windows sandbox, memory, scheduler, bash tool, MCP client, browser agent |
| 3 | [kailiang0120/monaw](https://github.com/kailiang0120/monaw) | Apache-2.0 | Python backend, Electron/React UI (UI not read) | 2026-11-27 | Primary | Approvals, action security, environment sanitiser, permission modes, sandbox |
| 4 | **JARVIS Local** — assumed [LYiHub/pub-local-jarvis](https://github.com/LYiHub/pub-local-jarvis) | MIT | Python + C++ runtime | 2026-07-24 | Primary (identity assumed, see §7) | Model download, inference path, lifecycle |
| 5 | [isair/jarvis](https://github.com/isair/jarvis) | "Jarvis AI Assistant License" (non-commercial). GitHub reports NOASSERTION | Python (~50k lines) | 2026-11-19 | Secondary, read to licence level | Licence, local-model dependencies |
| 6 | [raydeStar/sir-thaddeus-2](https://github.com/raydeStar/sir-thaddeus-2) | **None** | C# + TypeScript | 2026-09-29 | Secondary | Approval digests, pairing, reuse ledger |
| 7 | [be1st6666/guarded-computer-use-mcp](https://github.com/be1st6666/guarded-computer-use-mcp) | MIT | JavaScript + PowerShell | 2026-09-15 | Secondary | Hash-chained audit, approval gate, signed switches |
| 8 | [ermoluk/mcp-warden](https://github.com/ermoluk/mcp-warden) | Apache-2.0 | Rust | 2026-11-10 | Secondary (README and metadata only) | Credential-broker design |
| 9 | [Abusalman-alhaj/mcp-guardian](https://github.com/Abusalman-alhaj/mcp-guardian) | None | Python | 2026-11-25 | Discovery only | Rejected: no licence, 0 stars, created 2026-11-25 |
| 10 | [djames1987/DragonForge-MCP](https://github.com/djames1987/DragonForge-MCP) | Not checked | Python | — | Discovery only | Phase 0 proposal, no implementation |
| 11 | [Mohammmed-Waleed/OpsAgent-AI-](https://github.com/Mohammmed-Waleed/OpsAgent-AI-) | Not checked | Python | — | Discovery only | README. Depends on a paid remote LLM API |
| 12 | TanvirIslam-BD/permission-aware-mcp | Not checked | Python | — | Discovery only | Listing description only |
| 13 | [Cipher208/a-memory](https://github.com/Cipher208/a-memory) | MIT | Python | 2026-11-22 | Discovery only | README |
| 14 | [utsapoddar/engram](https://github.com/utsapoddar/engram) | MIT | Python | 2026-09-26 | Discovery only | README |
| 15 | [Fukuro99/sqgraph](https://github.com/Fukuro99/sqgraph) | MIT | Python | 2026-10-16 | Discovery only | README. 0 stars |
| 16 | [KooshaPari/OmniRoute](https://github.com/KooshaPari/OmniRoute) | MIT | TypeScript | 2026-11-20 | **External gateway, not a component** (C-05, D7) | Metadata and README |

## 4. Discovery sweep

Queries run in this study:

- `open source Windows MCP server approval human-in-the-loop tool permissions audit log C# .NET`
- `github .NET WPF AI agent MCP client tool approval SQLite local-first open source`
- `OmniRoute AI gateway github open source OpenAI compatible routing`
- `open source C# .NET local memory knowledge graph RAG SQLite FTS5 agent library MIT`
- GitHub repository search for SenClaw, Monaw, JARVIS Local, and sir-thaddeus (metadata only)

**Limits.** These searches are not an exhaustive survey. No permissively licensed .NET-native candidate with a complete approval-and-audit stack was found. Quality claims about discovery candidates come from their own descriptions and were not tested.

## 5. Coverage by research area

| Area | Candidates reviewed | Coverage |
|---|---|---|
| Personal assistants | sir-thaddeus, sir-thaddeus-2, JARVIS Local, isair/jarvis, Monaw | Covered. No adoption |
| Multi-agent / orchestration | SenClaw `agent/agent_pool`, Monaw (LangGraph), sir-thaddeus (single-agent pipeline) | Covered at module level |
| Long-term memory | SenClaw `memory/`, sir-thaddeus `memory`, sir-thaddeus-2, a-memory, engram, sqgraph | Covered |
| Knowledge / RAG | sir-thaddeus wiki and readers, SenClaw `memory/cognitive`, engram | Partial |
| Document processing | sir-thaddeus `document-reader` (PdfPig, OpenXML, CSV, RTF, XLSX) | Covered |
| MCP | sir-thaddeus `mcp-tools-core`, SenClaw `mcp/`, Monaw MCP bridge, mcp-warden, permission-aware-mcp | Covered |
| Browser / desktop automation | SenClaw `browser/` and `browser_agent/`, Monaw (pywinauto, Chrome extension), guarded-computer-use-mcp, sir-thaddeus (Playwright) | Covered. No adoption |
| Tool execution | SenClaw `tools/bash.rs`, sir-thaddeus `EnforcingToolRunner`, Monaw `agent/sandbox/` | Covered |
| Permissions / approvals | All of the above, plus guarded, mcp-warden, OpsAgent, permission-aware-mcp, thaddeus-2 | Covered |
| Voice STT/TTS | sir-thaddeus `voice-backend`, JARVIS Local, isair/jarvis | **Gap.** Only local stacks were reviewed. No ₹0 remote STT/TTS candidate was evaluated |
| Provider abstraction | sir-thaddeus `LlmClientFactory`, SenClaw `providers`, Monaw providers, isair `openai_compatible.py`, OmniRoute | Covered (adapter shape only) |
| OmniRoute-compatible | OmniRoute | **Gap.** Identity confirmed; no OmniRoute client code reviewed. See §7 |
| Background services / scheduling | SenClaw `scheduler/`, Monaw `agent/scheduler.py` (located, not read), sir-thaddeus Routines | Covered |
| Monitoring / observability | Not systematically researched | **Gap** |
| Native Windows desktop | Monaw (Electron shell, not read), sir-thaddeus Photino (rejected), guarded (PowerShell) | **Partial.** No WPF-native reference was reviewed. AERON's own WPF shell is the baseline |

## 6. Headline findings

1. **No candidate is adoptable as a whole.** Each reviewed repository is a different product shell (Electron, Photino, Flutter with a Rust daemon, web UI, Python desktop pet), or depends on local models, or both.
2. **No candidate has approval authority as strong as AERON's.** Specific gaps: "never ask again" auto-accept rules and a global accept-all flag (SenClaw); an LLM that approves actions and a Full Access mode with no prompt (Monaw); a one-use check held only in process memory (Monaw); an approved action that is never marked consumed (sir-thaddeus-2); per-dialog decisions with no durable token (guarded-computer-use-mcp).
3. **Audit.** Only one candidate has a per-record hash chain (guarded-computer-use-mcp, JavaScript). That chain resets per rotated segment and is self-rewritable, so it is weaker than AERON's `DurableAuditLedger` with checkpoints and truncation codes. No audit code is adopted. Ideas only.
4. **Secret handling.** Monaw's allowlist-first environment builder is the best pattern reviewed. Two defects were found in candidates: SenClaw's MCP client passes the full parent environment to child servers, and sir-thaddeus passes API-key variables to child MCP processes.
5. **Windows isolation.** SenClaw has the most complete Windows sandbox design reviewed (AppContainer plus Job Object). Its own documentation states that it has never run on real Windows. It is the most promising Windows-isolation reference and remains `UNVERIFIED`.
6. **Memory.** Every memory candidate uses FTS5 plus embeddings. Several require remote embedding providers, and some ship local-model paths. The SQLite schema patterns in SenClaw and sir-thaddeus are adaptable. AERON's U-7 memory design is still undecided and gates this work.
7. **Licensing.** sir-thaddeus-2 (no licence) and isair/jarvis (non-commercial) cannot contribute code. Several discovery candidates have no licence, or are only days old.
8. **Integration model.** No runtime embedding of the Rust, Python, or Node candidates fits C-02 (no localhost service) and the `ArchitectureTests` ban list. Integration is by adapted pattern, re-authored in C# within the existing AERON projects.

## 7. Limits and open items

- **No build or test evidence** exists for any upstream code. Windows behaviour is unverified for all of it.
- **JARVIS Local identity is assumed.** The name matches two repositories. The assumed one is LYiHub/pub-local-jarvis (a Chinese-language local multimodal desktop pet). isair/jarvis is also reviewed. The Owner should confirm which was meant.
- **sir-thaddeus-2 reuse ledger** pins `d0cac1e`, which differs from the `845b50c` audited earlier. The v1 file paths it cites were not re-verified.
- **OmniRoute identity.** Search results show at least three repositories with the OmniRoute name (KooshaPari, backryun, HotOpenSourcing). Only KooshaPari/OmniRoute was checked (MIT, TypeScript). The Owner must confirm which upstream C-05 refers to and pin it.
- **Partly read repositories.** In Monaw, the scheduler, provider adapters, blocked-root enforcement, and HMAC attestation were located but not read in full. Their statuses are marked accordingly.
- **Not researched.** Remote STT/TTS, monitoring, and a WPF-native reference (see §5).

## 8. Document index

| Deliverable | Purpose |
|---|---|
| MULTI-REPOSITORY-RESEARCH.md | This document: method, scope, coverage, limits |
| REPOSITORY-COMPARISON-MATRIX.md | Per-repository facts: licence, maintenance, architecture, security model, Windows, remote inference, cost, complexity, overlap, time saved, limitations |
| COMPLETE-COMPONENT-ADOPTION-MATRIX.md | Per-component classification with source file, class or function, and test evidence |
| SECURITY-AND-LICENSING-REVIEW.md | Licence matrix, defects found, approval and audit invariant comparison, proof obligations |
| ARCHITECTURE-INTEGRATION-PLAN.md | Integration options evaluated, target placement in AERON, dependency-conflict analysis |
| REPLACEMENT-AND-MIGRATION-PLAN.md | Components to replace, the invariants each replacement must preserve, migration steps |
| AERON-FOUNDATION-ROADMAP.md | Capability-to-repository contributions, integration order, blocking Owner decisions |

**Status of all seven:** `PROPOSED`. None is approved for implementation. The Owner's review is required first.
