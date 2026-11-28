# AERON — Security and Licensing Review

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. Review of candidate components only. It does not change any AERON security policy, decision register entry, or approval.
- **Authority:** AERON security specifications take precedence over every upstream convenience. Where a candidate conflicts with a specification, the candidate loses. Non-negotiables are listed in [MULTI-REPOSITORY-RESEARCH.md §1](MULTI-REPOSITORY-RESEARCH.md).
- **Limit:** No candidate was built or run. Every security statement is from source reading or stated design, and is marked accordingly.

---

## 1. Licence matrix

| Repository / package | Licence | Source of statement | Use status for AERON |
|---|---|---|---|
| raydeStar/sir-thaddeus | Apache-2.0 | Licence API; `LICENSE` | Usable with attribution. Keep upstream notices. No NOTICE file upstream |
| SenClaw/senclaw | MIT | Licence API | Usable with notice. Rust runtime not usable |
| kailiang0120/monaw | Apache-2.0 | README badge; `LICENSE` (full text); licence API | Usable with attribution and NOTICE handling |
| LYiHub/pub-local-jarvis | MIT | Licence API; `LICENSE` | Permitted but **rejected on technical grounds** (local models) |
| isair/jarvis | **Custom non-commercial** | `LICENSE` read in full | **Not usable.** Commercial use needs a separate licence |
| raydeStar/sir-thaddeus-2 | **None** | Repository metadata; no licence file | **Not usable.** Ask the copyright holder for a licence before any reuse. Do not assume |
| be1st6666/guarded-computer-use-mcp | MIT | Licence API | Usable with notice. Reference only in practice |
| ermoluk/mcp-warden | Apache-2.0 | Licence API | Usable with notice. Reference only |
| Abusalman-alhaj/mcp-guardian | **None** | Licence API returned no licence | **Not usable** |
| Cipher208/a-memory | MIT | Licence API | Reference only |
| utsapoddar/engram | MIT | Licence API | Reference only |
| Fukuro99/sqgraph | MIT | Licence API | Reference only |
| KooshaPari/OmniRoute | MIT | Licence API | External service. Not vendored |
| DragonForge-MCP, OpsAgent, permission-aware-mcp | Not checked | — | Not usable until checked |
| ModelContextProtocol C# SDK | **NOASSERTION** | Licence API | **Blocked** until the pinned package's licence is read directly. A7 gate |
| Playwright (.NET) | MIT | Licence API | Permitted. Runtime browser downloads need a decision (SL-07) |
| KokoroSharp | MIT | Licence API | Library is permitted, but its model download is not. Reject use of downloads |
| hexgrad/kokoro (model weights) | Apache-2.0 | Licence API | Weights are excluded by C-04 |
| SearXNG (apps/searxng) | AGPL-3.0-or-later | Upstream tree | **Excluded** (X3). Network-use obligations |
| PdfPig, Serilog | Apache-2.0 | Licence API | Permitted with notice |
| OpenXML SDK, Cronos, AngleSharp, HtmlAgilityPack, NCalc, YamlDotNet, EF Core, dotnet/extensions | MIT | Licence API | Permitted with notice |
| SQLitePCLRaw (transitive) | Apache-2.0 | Licence API (direct package) | Transitive licences not verified. Record in THIRD_PARTY_NOTICES as not verified |
| dotnet/runtime, aspnetcore | MIT (per platform) | Licence API | Platform. Not vendored |

**Licence rules for this programme:**
- Apache-2.0 and MIT code may be adopted with its copyright and licence notices preserved. Upstream copyright lines must not be removed or replaced by AERON copyright. Add a `THIRD_PARTY_NOTICES.md` row for each adapted file.
- Adapted code keeps a comment naming the upstream repository, the upstream file, and the upstream commit.
- A reference reading of a no-licence or non-commercial repository does not grant a licence. Such repositories may inform a design only when the design is re-derived from AERON's own specification. Copying text, structure, or identifiers is prohibited.
- `NOASSERTION` and "not checked" are treated as **no licence**, not as permissive.

---

## 2. Defects found in candidate code

Each defect is recorded because AERON must not reproduce it. Each was confirmed by reading the cited source.

| ID | Repository | Location | Defect | Severity | AERON rule violated | Action |
|---|---|---|---|---|---|---|
| SL-01 | sir-thaddeus | `RuntimeMcpEnvironmentBuilder` / `StdioMcpToolClient` | API-key variables (`WEBSEARCH_API_KEY`, `ST_DEEPDIVE_PLACES_API_KEY`) passed into child MCP process | High | C-12; secret protection | Reject. Use allowlist env (MW-05) |
| SL-02 | sir-thaddeus | `InMemoryPermissionBroker.Validate` (77–97) | Token not consumed. Revocation, expiry, and capability checked only | High | Durable single-use approvals | Reject. Replaced by `ApprovalEngine.TryConsume` |
| SL-03 | sir-thaddeus | `ToolExecutionContext.IsPathAllowed` | Prefix check without separator boundary. `C:\Docs` permits `C:\Docs2` | High | Default-deny path scope | Reject. Canonicalise and enforce boundary |
| SL-04 | sir-thaddeus | `SettingsManager` | Non-atomic `File.WriteAllText` with plaintext API keys | High | C-12; atomic config | Reject. `SecureConfigStore` already atomic |
| SL-05 | sir-thaddeus | `LmStudioClient` (auto-load when no HttpClient injected) | Remote mutation: `POST /v1/models/load` | Medium | Remote-only inference, no unexpected state change | Reject |
| SL-06 | sir-thaddeus | `AutoMemoryExtractor` | Stores conversation chunks without explicit consent | High | Owner consent for memory | Reject. Memory writes need explicit approval (L2) |
| SL-07 | sir-thaddeus | Playwright dependency | Playwright requires a browser install step. Whether sir-thaddeus triggers it automatically was not verified | Medium (unverified) | No weight or binary download without approval | Gate. Verify upstream usage. Disable automatic download. Owner decision for browser runtime |
| SL-08 | SenClaw | `src/mcp/client.rs` 83–85 | `Command::new` then `cmd.envs(env)`; no `env_clear`. Child inherits parent environment | High | C-12; secret protection | Reject. Allowlist env |
| SL-09 | SenClaw | `src/tools/bash.rs` 85–100 | Denylist matches first word only. `sh -c`, pipes, and chained commands bypass | High | Default-deny tool execution | Reject. Exact-argument policy |
| SL-10 | SenClaw | `src/agent/permission_bridge/bridge.rs` `RuleAction::AutoAcceptAndAllow`; `set_accept_all` | Persistent "never ask again" and global accept-all | Critical | Per-action approval; default-deny | Reject |
| SL-11 | SenClaw | `src/agent/agent_pool/types.rs` `skip_all_agents_permissions` | Global bypass of approvals for all workers | Critical | Default-deny | Reject |
| SL-12 | SenClaw | `src/scheduler/executor.rs` `execute_script` | Runs shell on a timer without per-run approval | High | L2 approval for persistent schedule actions | Reject |
| SL-13 | SenClaw | `src/memory/embedding_providers.rs` (Ollama, local GGUF) | Local model runtime | Excluded | C-03, C-04 | Reject |
| SL-14 | SenClaw | `src/memory/cognitive/gnn_sage.rs` `save`/`load` | Model weights written to disk | Excluded | C-04 | Reject |
| SL-15 | SenClaw | `src/security/replication.rs` header | Weights uncalibrated. TPR/FPR not valid for this port | Medium | Evidence for security claims | Reference only. No claims cited |
| SL-16 | SenClaw | No audit hash chain (zero matches) | No tamper evidence in audit | Medium | Hash-chained audit | Gap in source. Keep AERON ledger |
| SL-17 | SenClaw | `direct_windows.rs` (header and `docs/sandbox-windows-research.md`) | Windows sandbox never run on Windows. Loopback blocked inside AppContainer | High (for adoption) | Native Windows verification | UNVERIFIED. Spike needed before any claim |
| SL-18 | Monaw | `agent/action_security/approval_binding.py` `_CLAIMED` | One-use set is in-process and resets on restart | High | Durable single-use | Reject. Persist with SQLite compare-and-set |
| SL-19 | Monaw | `agent/approval_review.py` (Auto Review) | LLM approves or denies actions | Critical | Owner-only consequential approval | Reject |
| SL-20 | Monaw | `docs/permissions.md` Full Access | Removes action approval prompts | Critical | Default-deny | Reject |
| SL-21 | Monaw | `approval_broker.py` `clear_all_tickets` | Clears approval history on request | High | Immutable audit | Reject. AERON audit is append-only and hash-chained |
| SL-22 | Monaw | `approval_broker.py` `_rewrite_tickets` | Full JSONL rewrite of approval records | Medium | Append-only audit | Reject |
| SL-23 | sir-thaddeus-2 | `ConnectedToolConversation.cs` 86–100; `Runtime.cs` 906 | Approved action can be dispatched again. `ReserveTool` only increments a counter | High | Durable single-use approvals | Reject behaviour. Verify AERON `TryConsume` covers replay (see §4) |
| SL-24 | guarded-computer-use-mcp | `src/audit.js` `redactScalar` | Unkeyed SHA-256 prefix (12 hex) of sensitive values. A short PIN or OTP can be recovered by enumeration | Medium | Redaction does not leak secrets | Do not copy. Use keyed HMAC or omit digest |
| SL-25 | guarded-computer-use-mcp | `src/audit.js` per-segment chain | Truncation of a whole rotated segment is not detectable from within the chain | Medium | Truncation detection | AERON checkpoint model is stronger. Keep |
| SL-26 | guarded-computer-use-mcp | `src/guard.js` watermark | Watermark can be rolled back by a writer (admitted) | Medium | Replay defence | Do not rely on counters alone |
| SL-27 | guarded-computer-use-mcp | `src/approval.js` header comment (threat description) | The upstream author documents that a parallel tool call could have answered the approval dialog. Described as found during hardening; not independently reproduced here | High (documented, fixed upstream) | Human-only approval | Lesson: approval UI must not accept actions from the agent's own tool-call path |
| SL-28 | isair/jarvis, pub-local-jarvis | Local model code | Model weight downloads and local inference | Excluded | C-03, C-04 | Reject |
| SL-29 | Monaw | `agent/chatgpt_account.py` | Consumer account sign-in as provider credential | High (for AERON) | No paid-API or account-based fallback without Owner decision | Reject |

---

## 3. Approval and audit invariant comparison

AERON invariants come from the existing `main` implementation (`ApprovalEngine`, `ActionDispatcher`, `DurableAuditLedger`, `PreemptionState`) and the non-negotiables. Each candidate is scored only from the source or stated design read in this study.

Legend: **Y** = present in code read · **P** = partial · **N** = absent · **U** = unverified · **—** = not applicable.

| # | Invariant | AERON (`main`, M2) | sir-thaddeus | SenClaw | Monaw | sir-thaddeus-2 | guarded-computer-use-mcp |
|---|---|---|---|---|---|---|---|
| I1 | Default-deny (no grant means refuse) | Y (design; 142 tests in CI) | P (Off/Ask/Always; Always persists) | N (auto-accept rules) | P (`full_access`) | P (per-run decision check) | U |
| I2 | Exact-parameter binding (hash over canonical args) | Y (`TryConsume(approvalId, actor, operation, exactParameters)`) | N | P (rule matchers) | Y (`compute_material_hash`, `payload_hash`) | Y (digest over exact action) | P (policy lists) |
| I3 | Single-use, consumed atomically | Y (`TryConsume`). **Durability to be confirmed (§4, V-01)** | N | N | P (in-memory set) | N (`ReserveTool` counter only) | U |
| I4 | Expiry | Y (design) | P (permission scopes) | P (rule TTL) | Y (`expires_at`) | Y (`Expires`) | U |
| I5 | Revocation / preemption | Y (`PreemptionState` epoch) | P (revocation check) | N | P (`superseded`, `retire`) | N | N |
| I6 | Actor / session binding | Y (`actorSessionId`) | N | N | P (conversation id) | Y (run id) | N |
| I7 | Human-only approval for consequential action | Y (Owner, design; MFA blocked by D2-01) | N | N (auto-accept) | N (Auto Review uses LLM) | P (owner review) | Y (physical input, UNVERIFIED on Windows) |
| I8 | Audit of request, decision, consumption, failure | Y (`DurableAuditKind`) | N (JSONL) | N | P (JSON store) | P (event store) | Y (hash chain, per segment) |
| I9 | Hash-chained, truncation-detecting audit | **Y** (`DurableAuditLedger`, checkpoints, `AUDIT_CHAIN_BROKEN`, `AUDIT_CHECKPOINT_MISSING`, `AUDIT_CHECKPOINT_MISMATCH`) | N | N | N | N | P (per segment; no checkpoint) |
| I10 | No persistent "never ask again" for L2 actions | Y (design) | N (Always) | N (rules) | N (mode-based) | — | Y |
| I11 | Fail closed on error or timeout | Y (design) | P (validator exception handling) | P | Y (`ask` on timeout) | Y (principle, REUSE_LEDGER) | P (deny on timeout) |
| I12 | Redaction unconditional, before logging | Partial (3 static regexes; `RedactingLogger`) | P (9 regexes, not first in pipeline) | N | P (secret-like key filter) | — | Y (keyed-name redaction; digest weakness SL-24) |
| I13 | Secrets not in child environment | Y (design; to be built in M7) | N (SL-01) | N (SL-08) | Y (allowlist) | — | — |
| I14 | Owner auth / MFA | **Blocked** (D2-01 CONDITIONAL; no MakeCredential or GetAssertion authorised) | N | N | N | P (pairing) | N |

**Reading this table:** On I5, I8, and I9, AERON's design is read as stronger than every candidate. On I3 (single-use) the design is present but durability is unverified (V-01), so it is not yet claimed as stronger. The gaps AERON still has are I3 durability (verify), I12 (redaction breadth, where sir-thaddeus has more patterns to review), I13 (not yet built), and I14 (blocked by Owner decision). Candidates are useful for I2, I4, I11, I13, and I12 pattern breadth, and for the Monaw approval state machine.

---

## 4. Verification items before any replacement claim

These must be confirmed by reading the `main` implementation and its tests. They are not assumed.

| ID | Verification | Why |
|---|---|---|
| V-01 | `ApprovalEngine.TryConsume` persists the consumed state within the same transaction that authorises dispatch, and survives restart | Monaw's one-use set is in-memory. SL-23 shows that an approval without consumption is replayable. AERON must not inherit either gap |
| V-02 | Replaying a consumed approval ID fails with a test | Required by invariant I3 |
| V-03 | Approval is bound to actor session and PreemptionEpoch, and changes after preemption are rejected | I5, I6 |
| V-04 | `DurableAuditLedger` records consumption and failure events, not only grant events | I8 |
| V-05 | `RedactionFilter` covers the nine patterns reviewed in sir-thaddeus `AuditSensitiveDataScrubber` (after each is reviewed for false positives) | I12 |
| V-06 | Count of security tests stays ≥ 60 and total passed ≥ 110 in CI (`ArchitectureTests.cs`) | Existing gate |

If any V item fails, the related replacement is blocked until a fix is merged with its own tests.

---

## 5. Proof obligations for security-critical replacement

Any component in the security path (approval, permission, audit, redaction, secret handling, process launch, path scope, sandbox) replaces an upstream component only when all of the following hold:

1. **Invariant table.** Every invariant in §3 that the upstream component touches is listed, with the new AERON behaviour and the test that demonstrates it.
2. **Non-regression.** The new component preserves or improves every invariant. A regression on any invariant blocks the change.
3. **Negative tests.** For each defect in §2 relevant to the component, a test demonstrates that the defect is absent (for example, a replay test, a prefix-boundary test, an environment-leak test).
4. **No weakened test.** Existing AERON tests are not modified to pass. The `ArchitectureTests` ban list and count thresholds are not lowered.
5. **Evidence.** The proof is recorded in the PR description with the CI run ID. Manual verification is not claimed unless performed and recorded.
6. **Owner review.** Replacement of security-critical code requires Owner approval. This review does not provide it.

---

## 6. Prohibited patterns (no-go list)

These are rejected in any form, including partial ports:

- Auto-approval rules, "never ask again", or accept-all flags that bypass per-action approval (SL-10)
- Any approver that is an LLM or a tool-free model call (SL-19)
- Full Access or equivalent modes that skip approval for L2 actions (SL-20)
- Passing the parent environment or API-key variables to child processes (SL-01, SL-08)
- Denylists as the primary control for shell execution (SL-09)
- Prefix-only path checks (SL-03)
- Unkeyed digests of sensitive values in logs (SL-24)
- Audit segments without cross-segment checkpoints (SL-25)
- Local model runtimes, model downloads, or weight files (C-03, C-04; SL-13, SL-14, SL-28)
- Paid API fallbacks (including Anthropic-only paths in OpsAgent)
- Consumer account sign-in as a provider credential without Owner decision (SL-29)
- Localhost servers, IPC endpoints, or browser WebSocket bridges (C-02; ST-15, ST-16, SC-24)
- Telegram or other external approval channels (WD-03)
- LangChain, LangGraph, Semantic Kernel, AutoGen, CrewAI (banned by `ArchitectureTests`)
- Copying code from no-licence or non-commercial repositories (§1)

---

## 7. Open licensing and security questions

| # | Question | Owner or action |
|---|---|---|
| Q-01 | Licence for `raydeStar/sir-thaddeus-2`. Any reuse requires explicit written licence from the copyright holder | Owner to decide whether to ask |
| Q-02 | Licence for the ModelContextProtocol C# SDK pinned version (NOASSERTION) | Read package and file before A7 |
| Q-03 | SmartReader licence (listed as "unverified" in MIGRATION-MAP A5) | Read package before A5 |
| Q-04 | Identity of the OmniRoute upstream used by C-05 (three repositories share the name) | Owner decision, pre-M3 |
| Q-05 | Whether Playwright browser downloads are permitted at runtime | Owner decision, before M7 browser tool |
| Q-06 | Whether SenClaw's Windows sandbox may be spiked on a Windows runner | Owner authorisation required |
| Q-07 | Whether the consumer ChatGPT account sign-in (Monaw) is ever acceptable | Owner decision. Default: not acceptable |
| Q-08 | Transitive licence list for SQLitePCLRaw and other AERON dependencies | Action: dependency licence scan before M3 |
