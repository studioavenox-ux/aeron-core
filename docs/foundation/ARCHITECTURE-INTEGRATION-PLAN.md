# AERON — Architecture Integration Plan

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. Describes how candidate components could fit AERON's architecture if the Owner approves the roadmap. It is not an implementation plan and does not authorise any change.
- **Basis:** [MULTI-REPOSITORY-RESEARCH.md](MULTI-REPOSITORY-RESEARCH.md), [COMPLETE-COMPONENT-ADOPTION-MATRIX.md](COMPLETE-COMPONENT-ADOPTION-MATRIX.md), [SECURITY-AND-LICENSING-REVIEW.md](SECURITY-AND-LICENSING-REVIEW.md).

---

## 1. Fixed constraints

| Constraint | Source | Effect on integration |
|---|---|---|
| Native Windows, WPF shell | C-02, ADR-TECH-001 | No Electron, Photino, Flutter, or web UI. No React bundle |
| No localhost service | C-02 (MIGRATION-MAP R2) | No Kestrel loopback, no IPC endpoint, no localhost WebSocket. In-process composition only |
| Remote-only inference | C-03, C-06 | Only remote OmniRoute `CLIENT_API`, after the pre-M3 decision |
| No local models or weight downloads | C-04 | No Ollama, LM Studio, GGUF, ONNX, Whisper, Piper, Kokoro, or model-download code |
| OmniRoute unchanged | C-05, D7 | AERON consumes OmniRoute as a client. Nothing in this repo modifies it |
| AERON-native agents | C-07 | Agent roles are AERON's; upstream personas are references only |
| Permission and dispatch authority | C-10, C-11, ADR-AUTH-002/003 | All actions pass `SecurityPolicyEvaluator` → `ApprovalEngine` → `ActionDispatcher` |
| No inline credentials | C-12 | Secrets in `SecureConfigStore`; never in arguments, logs, or child environments |
| Architecture tests | `tests/Aeron.Architecture.Tests/ArchitectureTests.cs` | Banned: LangChain, SemanticKernel, AutoGen, CrewAI, LlamaSharp, Ollama, OnnxRuntime, TorchSharp, TensorFlow, OpenAI and Anthropic SDKs, System.Windows.Forms. Model-weight file extensions banned. `HttpClient`, `HttpListener`, `TcpListener`, and sockets restricted in Security and Desktop |
| Toolchain | ADR-TECH-001, `global.json` | .NET 10 SDK, C# 14. Upstream `global.json` pins 10.0.103; AERON uses 10.0.100 with `rollForward: latestFeature`. Reconcile before any port |

---

## 2. Integration options evaluated

Each option is scored against the fixed constraints. "Pass" means the option can satisfy the constraint as designed. "Fail" means it cannot.

| Option | Description | Native Windows | No localhost | Remote-only | No weights | Banned deps | Dependency conflicts | Verdict |
|---|---|---|---|---|---|---|---|---|
| **A. Source modules** (copy upstream C# files) | Vendor selected `.cs` files from sir-thaddeus with attribution | Pass | Depends on file (some use Kestrel, IPC) | Depends | Depends | Depends (LLM and voice files fail) | High: upstream references Photino, Kestrel, Serilog, its own abstractions | **Limited.** Only for pure leaf files (e.g., `DocumentTruncator`, parsing helpers), after review |
| **B. NuGet libraries** | Reference third-party packages (PdfPig, OpenXML, Cronos, AngleSharp, HtmlAgilityPack, Serilog) | Pass | Pass | Pass | Pass | Check each | Low to medium: version alignment in `Directory.Packages.props` | **Preferred** for leaf libraries after licence and transitive review |
| **C. Separate process** (SenClaw daemon, Monaw backend, Python/Rust sidecar) | Run another runtime as a child process and talk to it | Partial (Rust and Python not in AERON toolchain) | **Fail** if it uses HTTP or WebSocket. Pass only with stdio pipes and no socket | Depends | Depends | Pass | High: second runtime, second supply chain, second audit trail | **Reject** as default. A separate process cannot hold AERON approvals or audit. Any future sidecar requires its own ADR |
| **D. MCP** (AERON as MCP client to tool servers) | Use MCP stdio or remote servers as tools behind `ActionDispatcher` | Pass | Pass for stdio. Remote MCP needs its own gate | Pass | Pass | Depends on SDK licence (NOASSERTION) | Medium: SDK version, transport | **Adopt as the tool protocol**, behind ApprovalEngine, after SDK licence check (A7 gate). MCP servers are third-party code and need intake review (SC-04 pattern) |
| **E. Remote API** (OmniRoute, Owner-approved search/STT/TTS) | Consume an HTTP API as a client | Pass | Pass (outbound only) | Pass | Pass | Pass | Low | **Adopt** for inference (OmniRoute), search (Owner choice), and voice (₹0 remote, pre-M8) |
| **F. Adapted pattern** (re-authored in C#) | Implement the idea in AERON-owned code, with a comment citing the reference | Pass | Pass | Pass | Pass | Pass | None | **Preferred for all security-critical behaviour** (approvals, audit, env, path, redaction) |

**Decision rule.** Security-critical behaviour is always F. Leaf libraries are B. Tool protocol is D. Inference, search, and voice are E. A is an exception that needs a per-file review record. C is not used.

---

## 3. Target placement inside AERON

Project names are those in the existing AERON solution and the M3+ plan. New projects are shown in *italics* and each requires an architecture-test change.

| Capability | Placed in | Integration option | Source reference | Milestone |
|---|---|---|---|---|
| Approval engine, durable single-use | `Aeron.Security` (existing `ApprovalEngine`) | F | Monaw state machine (MW-01); thaddeus-2 digest idea (T2-01); permission-aware-mcp pattern (PA-01) | M2 verify, M5 extend |
| Dispatch | `Aeron.Security` / `ActionDispatcher` (existing) | F | None needed (existing) | Existing |
| Audit ledger | Existing `DurableAuditLedger` (project location as on `main`) | Keep | None. Design read as stronger than candidates | Existing |
| Redaction | `Aeron.Infrastructure` (`RedactionFilter`, `RedactingLogger`) | F, with pattern review | sir-thaddeus `AuditSensitiveDataScrubber` (ST-13) | M2 follow-up |
| Secret store | `Aeron.Infrastructure` (`SecureConfigStore`, existing) | Keep | Monaw DPAPI pattern (MW-08) deferred | Existing; vault D6 |
| Process launch environment | `Aeron.Execution` (*new*) | F | Monaw `environment.py` (MW-05) | M7 |
| Path policy | `Aeron.Execution` (*new*) | F | sir-thaddeus defect (ST-05); SenClaw `check_cd_safety` (SC-11) | M7 |
| OmniRoute gateway | `Aeron.Gateway` (*new*) | E + F for request shapes | sir-thaddeus A1; Monaw provider shapes (MW-13) | M3 |
| Outbound egress guard | `Aeron.Gateway` (*new*) | F | SenClaw `egress.rs` (SC-01) | M3 |
| Memory store | `Aeron.Memory` (*new*) | F | sir-thaddeus schema (ST-29); SenClaw schema (SC-16); engram rebuildable index (EN-01) | M5 |
| Embeddings | `Aeron.Gateway` via OmniRoute | E | SenClaw OpenAI shape (SC-17) | M5 |
| Document processing | `Aeron.Documents` (*new*) | B + F | PdfPig, OpenXML (LB-01, LB-02); readers (ST-28); truncation (ST-22) | M5 |
| MCP tool adapter | `Aeron.Execution` MCP adapter (*new*) | D | ModelContextProtocol SDK (LB-06, blocked); sir-thaddeus MCP patterns (ST-07) | M7 |
| Scheduler | `Aeron.Scheduling` (*new*) | B (Cronos) + F | SenClaw parsing (SC-13); sir-thaddeus Cronos (ST-19). Runner must route through ApprovalEngine | M7 |
| Browser automation | `Aeron.Execution` browser tool (*new*) | F, deferred | SenClaw tier policy (SC-23); Playwright (LB-07) | M7, G4 |
| Windows sandbox | `Aeron.Execution` sandbox (*new*) | F, after spike | SenClaw AppContainer design (SC-07, SC-08, SC-09) | M7, spike first |
| Voice | `Aeron.Voice` (*new*) | E (approved ₹0 remote only) | None (upstream voice rejected) | M8 |
| Command Centre | `Aeron.Desktop` (WPF, existing) | AERON-native | None. Upstream UI rejected | M9 |

---

## 4. Data flow for an approved action (AERON-owned)

This is the target flow. It uses only AERON components. Upstream components supply patterns, not runtime code.

1. **Request.** An agent (or the Owner) produces a proposed action with exact parameters. The model never receives approval or dispatch tools.
2. **Policy.** `SecurityPolicyEvaluator` classifies the action. Default: deny. Unknown tool, unknown path, or unknown host results in refusal.
3. **Proposal record.** `ApprovalEngine` creates a pending approval bound to: actor session, operation, canonical hash of exact parameters, PreemptionEpoch, expiry. Written durably. Audit event appended.
4. **Owner decision.** Presented in the WPF approval view. Decision requires the Owner session. Consequential decisions require MFA (blocked until D2-01). No LLM, rule, or timer may approve.
5. **Consumption.** `ActionDispatcher` calls `ApprovalEngine.TryConsume(approvalId, actorSessionId, operation, exactParameters)`. The consumed state is written in the same transaction as the dispatch authorisation (V-01). A second call fails.
6. **Environment.** For process actions, the child environment is built by the allowlist builder. Secret-like and code-loading keys are dropped and recorded.
7. **Execution.** Runs in the approved sandbox (or without sandbox only if the Owner approves that mode for that operation).
8. **Result.** Result, exit status, and hash of output are recorded. Failure is recorded as failure, not as silence.
9. **Audit.** Every step above appends to `DurableAuditLedger`. Chain verified on startup. Truncation is reported with the existing failure codes.

Failure at any step fails closed. The approval is not reusable after failure (SL-23 lesson).

---

## 5. Dependency-conflict analysis

| Candidate dependency | Conflict | Resolution |
|---|---|---|
| LangChain, LangGraph (Monaw) | Banned by `ArchitectureTests`; overlaps AERON orchestration | Do not reference. Re-author any pattern |
| Microsoft.SemanticKernel, AutoGen, CrewAI | Banned | Not used |
| Photino.NET (sir-thaddeus) | Web-shell; violates C-02 | Not used |
| Kestrel, ASP.NET runtime API (sir-thaddeus) | Localhost server; violates C-02 | Not used |
| Serilog (sir-thaddeus) | Would be a second logging pipeline next to `RedactingLogger` | Deferred (A12). If adopted, redaction must stay first in pipeline |
| ModelContextProtocol SDK | NOASSERTION licence; version coupling with `System.Text.Json` | Blocked until licence read; pin one version |
| Playwright (.NET) | Runtime browser download | Disable automatic download; Owner decision (Q-05) |
| KokoroSharp, Whisper.cpp, Piper | Model weight download | Rejected (C-04) |
| SQLitePCLRaw | Already used by AERON; transitive licences not verified | Keep one version; record in notices (Q-08) |
| SDK pin 10.0.103 (upstream) vs 10.0.100 (AERON) | Build reproducibility | AERON keeps 10.0.100 with rollForward. No upstream pin copied |
| Rust (SenClaw), Node (guarded, OmniRoute client), Python (Monaw, OpsAgent) | Second toolchain in AERON build | Not added. Patterns re-authored in C# |
| EF Core (sir-thaddeus) | Would replace AERON's SQLite persistence | Not adopted. AERON `SqlitePersistence` with checksummed migrations is kept |

---

## 6. Sandbox integration (SenClaw Windows backend)

This section is a plan only. It is not authorised.

**Why it matters.** AERON's M7 needs a process isolation option on Windows. The SenClaw design (AppContainer for deny-by-default, Job Object for memory limits and tree kill) is the most complete Windows approach reviewed. Per the upstream research note, it has not been run on real Windows.

**Spike plan (requires Owner authorisation, Q-06):**
1. Ask Owner to authorise a Windows-runner spike. Spike is a disposable branch and is not merged.
2. Reproduce the sandbox's own test expectations on `windows-latest`: write refused, read refused, network refused, tree kill on job close, ACL merge preserved, Python start with `SystemRoot` present.
3. Add a test that a sibling path (`C:\Docs2` when `C:\Docs` is allowed) is refused.
4. Record the result as evidence. If any expectation fails, the design is not adopted and the failure is recorded.
5. Only a passing spike may lead to an ADR proposing adaptation.

**Known limits to carry forward:** loopback blocked inside AppContainer (so no local helper services), no interactive terminal, no background run, Space Apps sandbox absent on Windows. AERON must accept these or reject the design.

---

## 7. MCP integration (AERON as client)

- AERON hosts an MCP client inside `Aeron.Execution`. Servers are configured by the Owner. Each server is third-party code and must pass an intake review: licence, source provenance, pinned version or digest, declared tools.
- Every tool call passes `ActionDispatcher`. MCP `annotations` (read-only hints) are advisory only. Classification comes from AERON policy, not from the server.
- Child process environment for stdio servers uses the allowlist builder. Secrets are never passed to a server unless the Owner grants a named secret to that server, which is itself an approval.
- No MCP server runs as a localhost listener. Remote MCP is treated as a remote API under option E, with its own gate.
- Tool list changes (new tool from an existing server) require re-approval. Pattern from Monaw's tool-classification docs, re-authored.

---

## 8. Remote inference integration

- Single gateway: `Aeron.Gateway`, calling OmniRoute `CLIENT_API` only. Endpoint, model, and provider allowlist are pre-M3 Owner decisions. No hard-coded provider keys.
- Request shape: OpenAI-compatible chat completion. Pattern from sir-thaddeus `LlmClientFactory` and request shapes (ST-27), re-authored.
- Forbidden: auto-loading models, model management calls, streaming assumptions that bypass the outbound gate, provider fallback to a paid API.
- Outbound text passes the egress guard (SC-01 pattern) before leaving the process. Thresholds are Owner-set, not upstream values.
- Embeddings use the same gateway (A2, M5).

---

## 9. Testing strategy for integrated components

| Layer | Test type | Evidence |
|---|---|---|
| Approval and dispatch | Unit and property tests for replay, expiry, preemption, actor binding | Required by V-02, V-03 |
| Environment builder | Unit tests: allowlist only, secret keys dropped, code-loading keys rejected, blocked keys reported | Monaw test cases to be re-written, not copied |
| Path policy | Unit tests including sibling-prefix (`C:\Docs2`), junction, and case cases | Defect SL-03 |
| Audit and redaction | Unit tests for chain, truncation, redaction, digest not present | SL-24, SL-25 |
| Memory | Schema tests, FTS5 tests, secret exclusion, deletion | ST-29 defect review |
| Documents | Size and page limits, malformed input, no network | ST-28 gap |
| MCP | Contract tests against a fake stdio server; allowlist env verified | SL-01, SL-08 |
| Architecture | `ArchitectureTests` extended for each new project, never weakened | Existing gate |

Each test suite is run on `windows-latest` through `ci-windows.yml`. No claim of Windows behaviour is made from a non-Windows run.

---

## 10. Non-integration decisions

- **No whole-repo import** of any candidate.
- **No runtime dependency** on SenClaw, Monaw, sir-thaddeus, JARVIS Local, isair/jarvis, sir-thaddeus-2, guarded-computer-use-mcp, or mcp-warden.
- **No new toolchain** (Rust, Python, Node) in the AERON build.
- **No UI reuse** from any candidate.
- **No new process model.** The app stays one process with AERON-owned composition.
