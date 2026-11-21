# ADR-AUTH-003 — Consequential Approval, Preemption & Audit Integrity (Part 3)

> Owner-review state: `CONFIRMED` mandate / `UNRESOLVED` verbatim historical text / `PROPOSED` reconstruction.  
> Classification: `PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW` for all newly drafted wording.  
> This document is **not** an original historical ADR.

## 1. Source and version

- Placeholder source: `docs/governance/ADR-AUTH-003.md`, blob `0fc6102e32bfc5b7e77713e829fbdf900fe61dc0`
- Version: `studioavenox-ux/aeron-core` at base commit `24d0804b445b429909dd16068da7e00cccc603a0` and PR #3 head `857b3203fea13239c397113298e8b41b1dc67f93`; blob SHAs identical across all seven unique remote refs inspected.
- Recovered contextual source: `AVENOX Ecosystem Master Blueprint (Working Copy)`, Google Docs version `8`, document version `1.0 | 18 November 2026`; DOCX version `4`, same document version.

## v1.1 historical decision restoration

**`CONFIRMED` selection: B — Complete Owner Authority.** The Owner retains ultimate authority over strategic direction, Core identity, major priorities, consequential decisions, and governance amendments.

Evidence: the Owner's v1.1 request Part 4, which cites `Pasted text(20261116-041642).txt` (H2); the Owner reports GPT-6 inspected the historical texts in the ChatGPT Library and compared authority records; H2 was not available to or independently inspected by Arena. The reported selection/meaning are confirmed by the Owner; original ADR text and exact file wording/version remain `UNRESOLVED`. The repository's existing confirmed governance rules below remain additional mandates, not replacements for this historical selection.

## 2. Recovered / confirmed historical baseline

Exact repository status:

> **Status:** `CONFIRMED` (approval & audit mandate) / `UNRESOLVED` (canonical verbatim historical text)

Exact confirmed rules recovered:

1. All consequential actions (`G2` restricted disclosure, `G3` external communications, `G4` external account modifications, and `L2` consequential local/schedule/evolution operations) require explicit Project Owner approval before execution.
2. Approval decisions are fail-closed on denial, expiration, cancellation, or parameter mismatch (implemented in Milestone 2).
3. All lifecycle, policy, and action events must be recorded through secret-redacting logging and tamper-evident audit trails, with zero exposure of passwords, API keys, or credentials.

Exact unrecovered-text note:

> **Unrecovered Historical Text Note (`UNRESOLVED`):** Exact historical clauses from the original external `ADR-AUTH-003` document will be inserted verbatim when provided by the Project Owner prior to Milestone 2.

Exact recovered AVENOX/AOS context relevant to authority (`RECOVERED` context only):

> **Founder (You)** — Founder and final authority

> **Operating principle:** each AI system has a distinct responsibility and works within defined permissions. The Founder retains final authority. External actions, spending, publishing, account changes, legal commitments and other consequential decisions require explicit human approval.

Boundary: the AVENOX ecosystem blueprint is broader context, not an AERON ADR-AUTH canonical clause.

## 3. PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW

- OwnerApprovalService is proposed for explicit, per-action approval requests bound to a SHA-256 parameter hash.
- Approval must fail closed on denial, expiration, cancellation, parameter mismatch, policy-version change, or Owner preemption, and must revalidate immediately before execution.
- Self-approval by AERON Core, agents, or background services is prohibited.
- AuditLedger is proposed as append-only, hash-chained, SQLite-WAL-backed, redacted, startup-verified, and tamper-detecting.
- Recovery from audit failure must deny new sensitive operations and preserve the last verified chain head.

## 4. Unresolved questions

- Exact historical ADR-AUTH-003 clauses and source/version.
- Exact canonical approval expiry, cancellation, and recovery semantics.
- Whether any additional tamper-evidence or Owner inspection/deletion semantics exist.

## 5. Dependencies with other specifications

- `ADR-AUTH-001.md`, `ADR-AUTH-002.md`.
- `U-01.md`, `U-04.md`, `U-07.md`.
- `SECURITY-ARCHITECTURE.md`, `SECURITY-THREAT-MODEL.md`, `M2-ACCEPTANCE-CRITERIA.md`.

## 6. Status

- Confirmed mandate/rules: `CONFIRMED`.
- Exact verbatim historical clauses: `UNRESOLVED`.
- Recovered broader authority context: `RECOVERED` context only.
- Newly drafted reconstruction: `PROPOSED`.
- Implementation: `NOT AUTHORIZED`.
