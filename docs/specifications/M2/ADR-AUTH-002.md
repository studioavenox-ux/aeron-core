# ADR-AUTH-002 — Permission Boundaries & Separated External Authorization (Part 2)

> Owner-review state: `CONFIRMED` mandate / `UNRESOLVED` verbatim historical text / `PROPOSED` reconstruction.  
> Classification: `PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW` for all newly drafted wording.  
> This document is **not** an original historical ADR.

## 1. Source and version

- Placeholder source: `docs/governance/ADR-AUTH-002.md`, blob `f568793d402e9c9f3a77de759e1d21378a0af437`
- Version: `studioavenox-ux/aeron-core` at base commit `24d0804b445b429909dd16068da7e00cccc603a0` and PR #3 head `857b3203fea13239c397113298e8b41b1dc67f93`; blob SHAs identical across all seven unique remote refs inspected.
- Recovered contextual source: `AVENOX Ecosystem Master Blueprint (Working Copy)`, Google Docs version `8`, document version `1.0 | 18 November 2026`; DOCX version `4`, same document version.

## v1.1 historical decision restoration

**`CONFIRMED` selection: C — Restricted Dual Authorization.** Core may propose governance-policy amendments. Owner approval is mandatory before amendments take effect.

Evidence: the Owner's v1.1 request Part 4, which cites `Pasted text(20261116-041642).txt` (H2); the Owner reports GPT-6 inspected the historical texts in the ChatGPT Library and compared authority records; H2 was not available to or independently inspected by Arena. The reported selection/meaning are confirmed by the Owner; original ADR text and exact file wording/version remain `UNRESOLVED`. The repository's existing confirmed governance rules below remain additional mandates, not replacements for this historical selection.

## 2. Recovered / confirmed historical baseline

Exact repository status:

> **Status:** `CONFIRMED` (permission & gateway separation mandate) / `UNRESOLVED` (canonical verbatim historical text)

Exact confirmed rules recovered:

1. AERON enforces an independent, default-deny permission engine separate from AERON Core and agent reasoning.
2. External governance strictly separates four distinct domains:
   - **`G1` Approved Remote Model Inference:** Routine requests over an Owner-approved, zero-cost OmniRoute gateway configuration execute without per-message approval prompts while enforcing approved provider, model, zero-cost, remote-only, redaction, and data-disclosure policies, with **zero paid or local inference fallback**.
   - **`G2` External Data Disclosure:** Governed by data classification and explicit Owner approval before disclosing local/restricted data off-device; secrets are strictly blocked and redacted.
   - **`G3` External Communications:** Mandatory explicit per-action Owner approval before sending any external communication.
   - **`G4` External Account Modifications:** Mandatory explicit per-action Owner approval before altering external accounts/services; mutating OmniRoute or enabling paid charges is permanently prohibited.
3. v1 strictly prohibits dynamic skill-script execution.

Exact unrecovered-text note:

> **Unrecovered Historical Text Note (`UNRESOLVED`):** Exact historical clauses from the original external `ADR-AUTH-002` document will be inserted verbatim when provided by the Project Owner prior to Milestone 2.

Exact recovered AVENOX/AOS context relevant to authority (`RECOVERED` context only):

> **Founder (You)** — Founder and final authority

> **Operating principle:** each AI system has a distinct responsibility and works within defined permissions. The Founder retains final authority. External actions, spending, publishing, account changes, legal commitments and other consequential decisions require explicit human approval.

Boundary: the AVENOX ecosystem blueprint is broader context, not an AERON ADR-AUTH canonical clause.

## 3. PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW

- PolicyDecisionEngine is proposed as deterministic and default-deny, independent of AERON Core and agent reasoning.
- M2 defines `G1`–`G4` and `L0`–`L3` as policy inputs but does not implement M3 OmniRoute `CLIENT_API` integration.
- Action classification, data classification, destination validation, and policy versioning are mandatory before any allow decision.
- Ambiguous or incomplete requests are denied with redacted policy-evaluation evidence.
- No paid/local inference fallback, skill-script execution, or OmniRoute mutation is permitted.

## 4. Unresolved questions

- Exact historical ADR-AUTH-002 clauses and source/version.
- Exact canonical action/data classification taxonomies.
- Exact destination-validation rules and policy-version format.

## 5. Dependencies with other specifications

- `ADR-AUTH-001.md`, `ADR-AUTH-003.md`.
- `U-07.md` for contextual-memory disclosure classification.
- `U-08R.md` for the fifteen separate decisions; original questions and alternatives remain unresolved where not supplied.
- `SECURITY-ARCHITECTURE.md` and `M2-DEPENDENCY-GRAPH.md`.

## 6. Status

- Confirmed mandate/rules: `CONFIRMED`.
- Exact verbatim historical clauses: `UNRESOLVED`.
- Recovered broader authority context: `RECOVERED` context only.
- Newly drafted reconstruction: `PROPOSED`.
- Implementation: `NOT AUTHORIZED`.
