# AERON — Replacement and Migration Plan

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. No replacement is authorised by this document. Each replacement needs its milestone, its Owner decision, and the proof obligations in [SECURITY-AND-LICENSING-REVIEW.md §5](SECURITY-AND-LICENSING-REVIEW.md).
- **Rule:** Replacing security-critical code requires proof that every relevant invariant is preserved or improved. Where the proof cannot be given, the replacement does not proceed.
- **Relation to MIGRATION-MAP.md:** Rows R1–R13 are already defined there. This document adds replacements found in the wider study and gives the invariant proof for the security-critical ones.

---

## 1. Replacement inventory

### 1.1 Already defined (from MIGRATION-MAP.md)

| Row | Upstream component | AERON replacement | Status on `main` |
|---|---|---|---|
| R1 | Photino shell and React workspace | WPF `Aeron.Desktop` | In place |
| R2 | Kestrel loopback runtime, bearer API, WebSocket, IPC | In-process `FoundationHost`, `SecurityComposition` | In place |
| R3 | `PermissionBroker`, `EnforcingToolRunner` | `SecurityPolicyEvaluator`, `ApprovalEngine`, `ActionDispatcher`, `PreemptionState` | In place (M2) |
| R4 | `JsonLineAuditLogger`, regex scrubber | `DurableAuditLedger` | In place (M2) |
| R5 | JSON settings store | `SecureConfigStore`, `ConfigPolicyValidator` | In place |
| R6 | Serilog-equivalent logging | `RedactingLogger` | In place |
| R7 | Tray, single instance, close-to-tray | `Win32SystemTrayService`, `SingleInstanceManager` | In place (M1) |
| R8 | Event bus, state machine | `CoreEventBus`, `AppLifecycleCoordinator` | In place |
| R9 | Stop and parent-watcher | `AppLifecycleCoordinator`, root cancellation | In place |
| R10 | Local inference clients | Remote OmniRoute gateway | Planned (M3) |
| R11 | Local voice | Approved ₹0 remote STT/TTS | Planned (M8) |
| R12 | Managed SearXNG | Owner-approved remote search | Decision required |
| R13 | Playwright automation | Deferred G4 tool | Deferred |

### 1.2 Added by this study

| Row | Candidate component (repo · source) | AERON replacement | Milestone | Classification |
|---|---|---|---|---|
| RP-01 | SenClaw `permission_bridge` auto-accept rules; sir-thaddeus `ToolPermissionGate` Always grants | `ApprovalEngine`: per-action, no persistent grant for L2 | M2 (done) verify | Replace (behaviour rejected) |
| RP-02 | Monaw `approval_binding.py` in-memory one-use set | `ApprovalEngine.TryConsume` with durable compare-and-set | M2 verify | Replace |
| RP-03 | Monaw `approval_broker.py` JSONL ticket store and `clear_all_tickets` | `ApprovalEngine` persistence in `SqlitePersistence` with checksummed migrations | M5 | Replace |
| RP-04 | Monaw `approval_review.py` (LLM approver) | None. Owner-only approval | — | Remove |
| RP-05 | sir-thaddeus `InMemoryPermissionBroker.Validate` | `ActionDispatcher` + `TryConsume` | M2 (done) | Replace |
| RP-06 | sir-thaddeus `ToolExecutionContext.IsPathAllowed` (prefix) | `Aeron.Execution` path policy with canonical path and separator boundary | M7 | Replace |
| RP-07 | SenClaw `bash.rs` `BANNED_COMMANDS` and first-word check | `ActionDispatcher` exact-argument policy with allowlisted executables and no shell interpretation | M7 | Replace |
| RP-08 | SenClaw `mcp/client.rs` env inheritance; sir-thaddeus `RuntimeMcpEnvironmentBuilder` | Allowlist environment builder, adapted from Monaw `environment.py` | M7 | Replace |
| RP-09 | sir-thaddeus `JsonLineAuditLogger` (hash-free JSONL) | `DurableAuditLedger` | M2 (done) | Replace (already in place) |
| RP-10 | sir-thaddeus `SettingsManager` (non-atomic write, plaintext keys) | `SecureConfigStore` | M2 (done) | Replace (already in place) |
| RP-11 | sir-thaddeus `AuditSensitiveDataScrubber` (9 regexes) | `RedactionFilter` extended with reviewed patterns | M2 follow-up | Extend |
| RP-12 | sir-thaddeus `PromptInjectionGuard` (marker-based) | Not a security control. Context construction rules in AERON, plus `ActionDispatcher` as the real boundary | M5 | Replace (not relied on) |
| RP-13 | sir-thaddeus `AutoMemoryExtractor` | Memory writes require explicit Owner approval | M5 | Replace |
| RP-14 | sir-thaddeus `LmStudioClient` auto-load | Remote gateway, no model management calls | M3 | Replace |
| RP-15 | sir-thaddeus `SystemTools.cs` preview/apply | Single `ApprovalEngine` path for execute | M7 | Replace |
| RP-16 | sir-thaddeus panic by tool name (`IsBlockedByPanic`) | `PreemptionState` epoch | M2 (done) | Replace (already in place) |
| RP-17 | sir-thaddeus voice backend and model downloads | Remote ₹0 STT/TTS | M8 | Replace |
| RP-18 | SenClaw `scheduler/executor.rs` script mode | `Aeron.Scheduling` runner routed through `ApprovalEngine` | M7 | Replace |
| RP-19 | SenClaw `security/egress.rs` concept | Adapted egress guard in `Aeron.Gateway`, Owner-set thresholds | M3 | Adapt |
| RP-20 | guarded `audit.js` digest in redaction | Keyed HMAC or no digest | M2 follow-up | Replace |
| RP-21 | None (no candidate has a Windows-verified sandbox) | AERON sandbox designed after spike, or deferred | M7 | Conditional |

---

## 2. Invariant proof for security-critical replacements

Each table lists the invariants the upstream component touches, the AERON behaviour that preserves or improves each one, and the test that must demonstrate it. The AERON column refers to behaviour that must be verified against `main` before any claim (see SECURITY-AND-LICENSING-REVIEW §4).

### 2.1 Approval and permission (RP-01, RP-02, RP-05, RP-15)

| Invariant | Upstream behaviour | AERON behaviour required | Required test (new) | Status |
|---|---|---|---|---|
| Default-deny | sir-thaddeus Off/Ask/Always; SenClaw auto-accept | No grant means refusal. No rule can auto-accept an L2 action | `Default_Deny_When_No_Approval` | Verify (design) |
| Exact-parameter binding | Partial (rule matchers) | Canonical hash of parameters at approval; dispatch refuses if parameters differ | `Changed_Parameters_After_Approval_Refused` | Verify (`TryConsume` signature shows it) |
| Single-use, durable | Absent (sir-thaddeus), in-memory (Monaw) | Consumed state committed with dispatch authorisation; survives restart | `Replay_Consumed_Approval_Refused`, `Replay_After_Restart_Refused` | **V-01, V-02 open** |
| Expiry | Partial | Expiry checked at consumption, not only creation | `Expired_Approval_Refused_At_Consume` | Verify |
| Preemption | Name-based panic (sir-thaddeus) | Epoch change invalidates pending approvals | `Preempted_Approval_Refused` | Verify (`PreemptionState`) |
| Actor binding | Absent | Consumption requires the same actor session | `Other_Session_Cannot_Consume` | Verify |
| No LLM approver | Monaw Auto Review exists | No code path lets a model or tool approve | `No_Tool_Can_Approve` (architecture-level) | New |
| Audit of consumption and failure | Absent | Consumption, refusal, and failure written to ledger | `Consume_And_Refusal_Audited` | Verify (V-04) |

**Proof gate:** RP-02 and RP-05 may not be claimed as complete until V-01 and V-02 pass in CI on `windows-latest`.

### 2.2 Environment and process launch (RP-08)

| Invariant | Upstream behaviour | AERON behaviour required | Required test (new) |
|---|---|---|---|
| No secret in child environment | sir-thaddeus passes API keys; SenClaw inherits all | Allowlist only. Secret-like keys dropped and reported | `Child_Env_Excludes_Api_Keys` |
| Code-loading variables blocked | Monaw has a denylist (`PYTHONPATH`, `NODE_OPTIONS`, `PATH`, and similar) | Same denylist, reviewed against AERON's runtimes | `Code_Loading_Keys_Rejected` |
| Explicit secret only by approval | Absent | A named secret can be passed only to a named server after approval | `Named_Secret_Requires_Approval` |
| Metadata recorded | Monaw returns blocked-key metadata | Metadata written to audit (keys, not values) | `Blocked_Keys_Audited_Without_Values` |

Monaw's secret-prefix list is provider-specific (OPENAI_, GOOGLE_, and similar). AERON must use a rule set that is not provider-specific, and must include any prefix that AERON adds to `SecureConfigStore`.

### 2.3 Path scope (RP-06)

| Invariant | Upstream defect | AERON behaviour required | Required test |
|---|---|---|---|
| Boundary-aware prefix | `StartsWith` allows sibling `C:\Docs2` for `C:\Docs` | Canonical full path, then match with trailing separator | `Sibling_Prefix_Refused` |
| Symlink and junction | Not handled | Resolve final path before check; refuse reparse points outside scope | `Junction_Escape_Refused` |
| Case | Not stated | Case-insensitive on Windows | `Case_Variant_Handled` |
| Blocked roots | Monaw list (`C:\Windows`, `C:\Program Files`, `%USERPROFILE%\AppData`) | Hard-blocked, not overridable by approval | `Blocked_Root_Hard_Refused` |

### 2.4 Audit and redaction (RP-11, RP-20)

| Invariant | Upstream / candidate | AERON behaviour required | Required test |
|---|---|---|---|
| Hash chain across all records | Absent (sir-thaddeus); per segment (guarded) | Existing `DurableAuditLedger` retained | Existing tests |
| Truncation detection | guarded: none across segments | Existing checkpoint codes retained | Existing tests |
| Redaction breadth | sir-thaddeus 9 regexes | Review each pattern for false positives; add accepted ones | `Redaction_Pattern_<Name>` per added pattern |
| No unkeyed digest of secret | guarded `redactScalar` does this | No digest, or HMAC with per-install key held by `SecureConfigStore` | `Redacted_Value_Has_No_Digest` |

### 2.5 Command and tool execution (RP-07)

| Invariant | Upstream defect | AERON behaviour required | Required test |
|---|---|---|---|
| No shell interpretation of approved arguments | SenClaw runs `sh -c` style strings | Executable plus argument vector; no shell unless approved as a shell operation | `Shell_Metacharacters_Not_Interpreted` |
| Allowlist, not denylist | SenClaw first-word denylist | Allowlisted executables and argument schemas | `Unlisted_Executable_Refused` |
| Working directory bound | SenClaw `check_cd_safety` (text parsing) | Working directory is an approved parameter | `Working_Dir_Change_Requires_Approval` |

### 2.6 Scheduler (RP-18)

| Invariant | Upstream defect | AERON behaviour required | Required test |
|---|---|---|---|
| No unattended shell | SenClaw script mode runs on timer | Scheduled actions are proposals; each run needs approval or a pre-approved scope with expiry | `Scheduled_Run_Without_Approval_Refused` |
| Persistent change needs L2 | Not enforced | Creating or changing a schedule is an L2 approval | `Schedule_Change_Requires_L2` |

---

## 3. Migration steps

Steps are ordered. Each step ends with a pull request against the integration branch. No step merges to `main` without Owner approval.

| Step | Scope | Preconditions | Exit evidence |
|---|---|---|---|
| M-S0 | This study reviewed by Owner. ADR-FOUND-001 decision recorded | Owner review | Owner decision note (not authored by the agent) |
| M-S1 | Verify V-01 to V-06 against `main`. Write findings as docs. No code change unless a verified gap is found | M-S0 | Verification note with test names and CI run ID |
| M-S2 | If V-01 or V-02 fails: fix `ApprovalEngine` consumption with tests first | M-S1 | CI run with new replay tests passing; security test count ≥ 60 |
| M-S3 | Redaction review: list each sir-thaddeus pattern, its false-positive risk, and the decision | M-S1 | Review table; tests per accepted pattern |
| M-S4 | M3: remote gateway (R10 replacement). Egress guard adapted (RP-19) | Pre-M3 OmniRoute and allowlist decisions; architecture test change | CI run; no banned dependency |
| M-S5 | M5: memory schema (A3) and document readers (A5) with size limits. Approval persistence (RP-03) | U-7 design; SmartReader licence check; pre-M5 decisions | Tests for secret exclusion, size limits, deletion |
| M-S6 | M7: environment builder (RP-08), path policy (RP-06), command policy (RP-07), MCP adapter (A7) | MCP SDK licence check; compiled tool catalogue approval; G1, G2 | Negative tests in §2 pass on `windows-latest` |
| M-S7 | M7 (conditional): Windows sandbox spike (RP-21) | Owner authorisation (Q-06) | Spike report; ADR if adopted |
| M-S8 | M7: scheduler (RP-18) | U-4 operational parameters; L2 design | Tests in §2.6 |
| M-S9 | M8: remote voice (RP-17) | Approved ₹0 provider | Provider contract test; no model download |

---

## 4. Data migration

- **No upstream user data is imported.** Sir Thaddeus memory, settings, routines, approvals, and audit logs are not migrated. They belong to other installations and are not AERON data.
- **AERON's own data** is schema-versioned through `SqlitePersistence`. Any new table is added with a forward-only migration and checksum (`SqliteMigrationRunner`).
- **Approval history from any prior prototype** is not migrated. If a prototype existed, it is retired, and its records are kept only as an audit artefact if the Owner requests it.

---

## 5. Rollback

- Each step is a separate pull request. Revert is a `git revert` of that pull request.
- Security-critical replacements keep the previous code path behind the composition root until the replacement's tests pass. The previous path is removed in a follow-up step, not in the same change.
- A failed CI gate blocks merge. No gate is weakened to pass.
- If a replacement is found to weaken an invariant after merge, the replacement is reverted and the finding is recorded in a new document.

---

## 6. Completion criteria

A replacement is complete only when:

1. Its row in §1 is marked done with the pull request and CI run ID.
2. Every invariant in §2 for that replacement has a passing test on `windows-latest`.
3. The security test count and total test count thresholds in CI are met or raised. They are never lowered.
4. `THIRD_PARTY_NOTICES.md` is updated for any adapted or referenced upstream file.
5. Owner has approved the merge.
