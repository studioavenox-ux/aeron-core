# AERON — Foundation Roadmap

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Status:** `PROPOSED`. This roadmap is a recommendation for Owner review. It does not start any milestone, does not authorise any import, and does not change the decision register.
- **Milestones referenced** (M1–M11) follow the existing AERON plan. Current state: M1 and M2 complete, with M2 CI at 142 tests passing. M3 onward not started.
- **Basis:** [MULTI-REPOSITORY-RESEARCH.md](MULTI-REPOSITORY-RESEARCH.md), [COMPLETE-COMPONENT-ADOPTION-MATRIX.md](COMPLETE-COMPONENT-ADOPTION-MATRIX.md), [SECURITY-AND-LICENSING-REVIEW.md](SECURITY-AND-LICENSING-REVIEW.md), [ARCHITECTURE-INTEGRATION-PLAN.md](ARCHITECTURE-INTEGRATION-PLAN.md), [REPLACEMENT-AND-MIGRATION-PLAN.md](REPLACEMENT-AND-MIGRATION-PLAN.md).

---

## 1. Summary

No repository reviewed can be adopted whole. On audit tamper evidence and preemption, AERON's kernel is designed to be stronger than every candidate reviewed, and its ledger is the strongest audit design read. Approval consumption is the one area where AERON's durability is still to be verified (V-01). The useful contributions are:

- **Patterns** for environment isolation (Monaw), memory schema (sir-thaddeus, SenClaw), document readers (sir-thaddeus), OpenAI-compatible request shapes (sir-thaddeus), approval lifecycle states (Monaw), and sandbox design (SenClaw, unverified).
- **Leaf libraries** under permissive licences (PdfPig, OpenXML SDK, Cronos, AngleSharp, HtmlAgilityPack) through NuGet.
- **An external gateway** (OmniRoute) and **a protocol** (MCP), both consumed as clients.
- **Defect lists** from each candidate, which AERON uses as negative test cases.

Everything else is rejected or deferred.

---

## 2. Which repositories contribute which capabilities

| Capability | Contributing repository | Form of contribution | Class | Milestone |
|---|---|---|---|---|
| Approval states and lifecycle | Monaw (`approval_broker.py`) | Pattern | ADAPTATION | M5 |
| Durable single-use approval | AERON (existing `ApprovalEngine`); Monaw and thaddeus-2 as negative references | Verify and fix | Existing / REFERENCE | M2 verify |
| Approval digest binding | sir-thaddeus-2 (no licence) and AERON (existing) | Design comparison only | REFERENCE | M2 |
| Audit tamper evidence | AERON (existing `DurableAuditLedger`) | Keep | Existing | Existing |
| Redaction breadth | sir-thaddeus (`AuditSensitiveDataScrubber`) | Pattern review | ADAPTATION | M2 follow-up |
| Redaction digest design (what not to do) | guarded-computer-use-mcp | Defect reference | REJECT (SL-24) | M2 follow-up |
| Secret-safe child environment | Monaw (`sandbox/environment.py`) | Pattern, re-authored | ADAPTATION | M7 |
| Path policy with boundary check | sir-thaddeus defect (negative test); Monaw blocked roots (policy data) | Test and policy | REPLACEMENT | M7 |
| Command execution policy | SenClaw (defect reference); AERON allowlist | Replacement | REPLACEMENT | M7 |
| Windows process sandbox | SenClaw (`direct_windows.rs`, `winproc.rs`) | Spike then decide | UNVERIFIED | M7, after spike |
| Sandbox known-issues checklist | SenClaw (`docs/sandbox-windows-research.md`) | Checklist | REFERENCE | M7 |
| Remote inference gateway | sir-thaddeus (request shapes); OmniRoute (external service) | Shape and API | ADAPTATION / EXTERNAL | M3 |
| Outbound egress guard | SenClaw (`security/egress.rs`) | Concept, re-authored | ADAPTATION | M3 |
| Memory schema and retrieval | sir-thaddeus (`memory-sqlite`); SenClaw (`memory/schema.rs`) | Schema pattern | ADAPTATION | M5 |
| Rebuildable memory index | engram (UNVERIFIED) | Principle | REFERENCE | M5 |
| Temporal facts with history | sqgraph (UNVERIFIED) | Principle | REFERENCE | M5 |
| Document readers | sir-thaddeus (`document-reader`); PdfPig, OpenXML (NuGet) | Adapted readers, libraries | ADAPTATION / LIBRARY | M5 |
| Document truncation | sir-thaddeus (`DocumentTruncator`) | Small adaptation | ADAPTATION | M5 |
| MCP tool protocol | ModelContextProtocol SDK (licence blocked) | Protocol | ADAPTATION, blocked | M7 |
| Third-party tool intake review | SenClaw (`security/scan.rs`) | Concept | REFERENCE | M7 |
| Scheduler parsing | SenClaw (`scheduler/task_scheduler.rs`); Cronos (MIT) | Parsing | ADAPTATION | M7 |
| Browser tool policy tiers | SenClaw (`browser_agent/policy.rs`) | Policy concept | ADAPTATION | M7, G4 |
| Credential storage on Windows | Monaw (DPAPI pattern) | Pattern | ADAPTATION, deferred | D6 |
| Credential broker design | mcp-warden (UNVERIFIED) | Concept | REFERENCE | M7 |
| Approval UI race lesson | guarded-computer-use-mcp | Lesson | REFERENCE | M5 |
| Signed safety switches | guarded-computer-use-mcp | Concept | REFERENCE | M5+ |
| Voice STT/TTS | None adopted. Remote ₹0 provider to be chosen | — | Planned | M8 |
| Command Centre UI | AERON WPF (no upstream UI) | AERON-native | — | M9 |
| Installer | AERON (no upstream packaging) | AERON-native | — | M11 |

---

## 3. Integration order

Order is set by dependency and by security risk. Each phase has entry gates, deliverables, and exit evidence. Each phase is a separate pull request against the integration branch, and none merges to `main` without Owner approval.

### Phase 0 — Owner review of this study (now)

- **Entry:** Study documents on the integration branch (this set).
- **Work:** Owner reads the set. Owner decides on ADR-FOUND-001 and the blocking decisions in §4.
- **Exit:** Owner decision recorded by the Owner. The agent does not record approval on the Owner's behalf.

### Phase 1 — Verify the kernel before taking anything (M2 follow-up)

- **Entry:** Phase 0 exit.
- **Work:**
  - Verify V-01 to V-06 in `SECURITY-AND-LICENSING-REVIEW.md §4` against `main`. Fix gaps with tests first.
  - Review sir-thaddeus redaction patterns (ST-13) one by one. Add only those with tests and no false-positive problem.
  - Design the keyed-redaction approach (no unkeyed digest, SL-24).
- **Exit:** Verification note with test names and CI run IDs. Redaction review table. No new upstream code.

### Phase 2 — Remote gateway (M3)

- **Entry:** Pre-M3 Owner decisions: OmniRoute identity (Q-04), endpoint, ₹0 provider and model allowlist, G2 data review.
- **Work:**
  - `Aeron.Gateway` (new) with OmniRoute `CLIENT_API` client. Request shapes from sir-thaddeus (ST-27), re-authored.
  - Outbound egress guard adapted from SenClaw (RP-19), with Owner-set thresholds.
  - Architecture test change for the new project (reviewed, not weakened).
- **Exit:** CI green. No localhost call. No model management call. Egress guard tests.

### Phase 3 — Memory and documents (M5)

- **Entry:** U-7 memory design approved. Retrieval decision made. SmartReader licence checked (Q-03). Pre-M5 decisions.
- **Work:**
  - `Aeron.Memory` (new): schema from sir-thaddeus (ST-29) with integrity table and Owner delete. No automatic extraction without consent (RP-13).
  - Embeddings via gateway (A2). Rebuildable-index principle (EN-01).
  - `Aeron.Documents` (new): PdfPig and OpenXML via NuGet (LB-01, LB-02); readers adapted (ST-28); size and page limits; truncation (ST-22).
  - Approval persistence moved to SQLite (RP-03), using MW-01 states.
- **Exit:** Tests for secret exclusion, deletion, size limits, malformed input. No network in document readers.

### Phase 4 — Tools and scheduling (M7)

- **Entry:** MCP SDK licence confirmed (Q-02). Compiled tool catalogue approved. G1 and G2 for each tool. Pre-M7 operational parameters for the scheduler.
- **Work, in this order:**
  1. Path policy with boundary check (RP-06), negative tests first.
  2. Environment allowlist (RP-08, from MW-05), negative tests first.
  3. Command policy: exact argument vectors, no shell (RP-07).
  4. MCP adapter through `ActionDispatcher` (D).
  5. Scheduler runner that routes through `ApprovalEngine` (RP-18), using Cronos (LB-03) and SenClaw parsing (SC-13).
  6. Third-party tool intake review (SC-04 pattern).
- **Exit:** Negative tests in REPLACEMENT-AND-MIGRATION-PLAN §2 pass on `windows-latest`.

### Phase 5 — Windows sandbox decision (M7, conditional)

- **Entry:** Owner authorises a spike (Q-06). Phase 4 exit.
- **Work:** Spike per ARCHITECTURE-INTEGRATION-PLAN §6. Results recorded. ADR proposed only if the spike passes every expectation.
- **Exit:** Spike report. Either an ADR proposing adaptation of SC-07 and SC-09, or a recorded decision to defer sandboxing and use approval-only execution.

### Phase 6 — Browser tool (M7, gated)

- **Entry:** G4 approval. Playwright runtime download decision (Q-05). Browser policy tiers from SC-23 designed.
- **Work:** Deferred R13 tool with Human tier for credentials and Approve tier for consequential actions.
- **Exit:** Tests for tier escalation, sensitive host refusal, and no credential entry by agent.

### Phase 7 — Voice (M8)

- **Entry:** Approved ₹0 remote STT/TTS provider (pre-M8).
- **Work:** `Aeron.Voice` (new), remote only. No upstream voice code.
- **Exit:** Provider contract test. No model download. No local binary.

### Phase 8 — Command Centre (M9) and installer (M11)

- **Entry:** Milestone gates.
- **Work:** AERON-native WPF. No upstream UI. Installer per M11.
- **Exit:** Per AERON milestone criteria.

---

## 4. Blocking Owner decisions

These are not decided by this study. Each must be decided by the Owner.

| # | Decision | Blocks | Source |
|---|---|---|---|
| OD-1 | Approve or revise ADR-FOUND-001 (selective-adoption scope; C-07 interpretation) | Phase 1 onward | ADR-FOUND-001-PROPOSED |
| OD-2 | Confirm JARVIS Local identity (assumed LYiHub/pub-local-jarvis; isair/jarvis also reviewed) | Recording the candidate set | MULTI-REPOSITORY-RESEARCH §7 |
| OD-3 | Confirm which OmniRoute upstream C-05 refers to; approve endpoint and ₹0 allowlist | Phase 2 | MULTI-REPOSITORY-RESEARCH §7; D7 |
| OD-4 | D2-01 status (MakeCredential and GetAssertion) and Owner MFA design | Phase 2 onward for consequential approvals | OWNER-DECISION-REQUIRED (D2-01 CONDITIONAL) |
| OD-5 | Memory reconciliation and U-7 design | Phase 3 | MIGRATION-MAP D1 |
| OD-6 | SmartReader licence (Q-03) | Phase 3 | MIGRATION-MAP A5 |
| OD-7 | MCP SDK licence (Q-02) | Phase 4 | MIGRATION-MAP A7 |
| OD-8 | Whether to ask the copyright holder of sir-thaddeus-2 for a licence (Q-01) | Any reuse of that repository | This study |
| OD-9 | Windows sandbox spike authorisation (Q-06) | Phase 5 | This study |
| OD-10 | Playwright runtime download policy (Q-05) | Phase 6 | This study |
| OD-11 | Approved ₹0 remote STT/TTS provider | Phase 7 | MIGRATION-MAP D4, R11 |
| OD-12 | Remote search provider replacing SearXNG | Phase 4 (search tool) | MIGRATION-MAP R12 |
| OD-13 | Whether consumer ChatGPT account sign-in is ever acceptable (Q-07) | Not planned. Default: no | This study |

---

## 5. Exit criteria for the foundation programme

The foundation programme is considered complete when:

1. Every row in COMPLETE-COMPONENT-ADOPTION-MATRIX marked ADAPTATION or REPLACEMENT is either implemented with its proof, or formally dropped by Owner decision.
2. Every UNVERIFIED row is either verified by source and run evidence, or closed as REJECT or REFERENCE ONLY.
3. No REJECT row appears in the codebase, in a dependency, or in a configuration.
4. `THIRD_PARTY_NOTICES.md` lists every adapted, referenced, or consumed upstream item, with licence and commit.
5. The security test count and total test count in CI are at or above their thresholds, and have not been lowered.
6. The Owner has reviewed and approved each merge to `main`.

---

## 6. What this roadmap does not claim

- It does not claim any upstream component works on Windows. None has been run on Windows by this study.
- It does not claim any time or cost saving as measured. Relative judgements are in REPOSITORY-COMPARISON-MATRIX.
- It does not claim that AERON's own components have been verified beyond what `main` CI shows. V-01 to V-06 are open.
- It does not start M3, M5, M7, or any other milestone.
- It does not change any decision register entry.
