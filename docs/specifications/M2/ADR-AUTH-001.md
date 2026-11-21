# ADR-AUTH-001 — Core Governance & Ultimate Project Owner Authority (Part 1)

> Owner-review state: `CONFIRMED` mandate / `UNRESOLVED` verbatim historical text / `PROPOSED` reconstruction.  
> Classification: `PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW` for all newly drafted wording.  
> This document is **not** an original historical ADR.

## 1. Source and version

- Placeholder source: `docs/governance/ADR-AUTH-001.md`, blob `aa7e8a511ff8e9ee84b10017ca8def6925a9b461`
- Version: `studioavenox-ux/aeron-core` at base commit `24d0804b445b429909dd16068da7e00cccc603a0` and PR #3 head `857b3203fea13239c397113298e8b41b1dc67f93`; blob SHAs identical across all seven unique remote refs inspected.
- Recovered contextual source: `AVENOX Ecosystem Master Blueprint (Working Copy)`, Google Docs version `8`, document version `1.0 | 18 November 2026`; DOCX version `4`, same document version.

## v1.1 historical decision restoration

**`CONFIRMED` selection: C — Core-Led Governance.** Core leads internal governance and makes routine governance decisions within AERON.

Evidence: the Owner's v1.1 request Part 4, which cites `Pasted text(20261116-041642).txt` (H2); the Owner reports GPT-6 inspected the historical texts in the ChatGPT Library and compared authority records; H2 was not available to or independently inspected by Arena. The reported selection/meaning are confirmed by the Owner; original ADR text and exact file wording/version remain `UNRESOLVED`. The repository's existing confirmed governance rules below remain additional mandates, not replacements for this historical selection.

## 2. Recovered / confirmed historical baseline

Exact repository status:

> **Status:** `CONFIRMED` (governance mandate) / `UNRESOLVED` (canonical verbatim historical text)

Exact confirmed rules recovered:

1. The **Project Owner** holds absolute, non-delegable ultimate authority over AERON, all policies, all data, all permissions, and all agents.
2. **AERON Core** serves as central intelligence and team leader strictly within boundaries established by the Project Owner.
3. **Specialist Agents** act as bounded delegates coordinated by AERON Core and cannot bypass Core oversight or Project Owner governance.
4. Project Owner pause, cancel, revoke, reconfigure, and emergency-stop directives immediately preempt every subsystem.

Exact unrecovered-text note:

> **Unrecovered Historical Text Note (`UNRESOLVED`):** Exact historical clauses from the original external `ADR-AUTH-001` record were absent from connected sources on `2026-11-17` and will be appended verbatim prior to Milestone 2 policy finalization without fabrication.

Exact recovered AVENOX/AOS context relevant to authority (`RECOVERED` context only):

> **Founder (You)** — Founder and final authority

> **Operating principle:** each AI system has a distinct responsibility and works within defined permissions. The Founder retains final authority. External actions, spending, publishing, account changes, legal commitments and other consequential decisions require explicit human approval.

Boundary: the AVENOX ecosystem blueprint is broader context, not an AERON ADR-AUTH canonical clause.

## 3. PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW

- OwnerAuthorityGuard is proposed as the non-bypassable enforcement layer for ultimate Owner authority.
- Authority inheritance across agents must be bounded: no agent may exceed the Owner limits or delegate Owner authority.
- Conflicting instructions resolve in this order: Owner preemption, explicit policy, default deny.
- Pause/cancel/revoke/reconfigure/emergency-stop must invalidate pending approvals and deny new consequential actions.
- Fail closed if Owner authority state, policy version, or canonical-specification status is unavailable.

## 4. Unresolved questions

- Exact historical ADR-AUTH-001 clauses and source/version.
- Whether any additional emergency-stop semantics exist beyond immediate preemption.
- Whether authorization inheritance across agents has a distinct historical formulation.

## 5. Dependencies with other specifications

- `U-06.md` for multi-agent boundaries.
- `U-08R.md` for the separate U-8R.2 Shared Authority and U-8R.15 Shared Governance decisions.
- `ADR-AUTH-002.md` and `ADR-AUTH-003.md`.
- `SECURITY-ARCHITECTURE.md` for OwnerAuthorityGuard.

## 6. Status

- Confirmed mandate/rules: `CONFIRMED`.
- Exact verbatim historical clauses: `UNRESOLVED`.
- Recovered broader authority context: `RECOVERED` context only.
- Newly drafted reconstruction: `PROPOSED`.
- Implementation: `NOT AUTHORIZED`.
