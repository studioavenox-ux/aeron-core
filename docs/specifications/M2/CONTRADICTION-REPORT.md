# Contradiction Report

> Purpose: identify apparent tensions, avoid silent resolution, and define boundaries.  
> Status: `PROPOSED` reconciliations; `UNRESOLVED` where Owner input is genuinely required.

| ID | Apparent tension | Evidence | Current treatment | Required Owner input |
|---|---|---|---|---|
| `CON-01` | U-8R `Backup storage = Google Drive` vs C-12 runtime storage outside cloud-synced `Documents` and prior M1 evidence-archive recommendation for a local Owner-controlled folder with cloud mirror only after `G2`/`G4` approval. | Owner request v1; `CONSTRAINTS.md`; prior `M2-PREPARATION.md`. | No contradiction asserted. Runtime storage and backup storage are distinct. Google Drive selection is `CONFIRMED`; proposed implementation boundary restricts private folder, redaction, `G2`/`G4` approval, ₹0 quota, and no raw secrets. | Approve/reject the proposed backup implementation boundary; do not restate storage choice. |
| `CON-02` | U-8R labels and reported descriptions vs missing full original questions and alternatives. | Owner request v1; no recovered option descriptions in repo, Drive/Docs, Gmail, Calendar, Notion, GitHub issues/PRs/comments. | Fifteen labels/selections `CONFIRMED` by Owner; Owner-reported descriptions preserved; remaining original questions/alternatives `UNRESOLVED`. | Supply original full questions/alternatives where available; do not repeat confirmed selections. |
| `CON-03` | U-8R.5 Custom Security Architecture vs existing C-12 zero-secret-exposure constraint. | Owner request v1; `CONSTRAINTS.md`. | Custom policy follow-up `UNRESOLVED`; C-12 remains independently confirmed. | Clarify the previously undefined custom privacy/security policy. |
| `CON-04` | U-8R.11 complete application data vs exact backup contents and secrets. | Owner request v1. | Complete AERON application data is Owner-reported scope; exact content/secret handling remains unresolved. | Clarify exact inclusion/exclusion and restoration boundaries. |
| `CON-05` | Original U-8 vs replacement U-8R. | `DECISION_LEDGER.md`, `U-08-ORIGINAL.md`, `U-08R-REPLACEMENT.md`, `M1-FOUNDATION.md`. | Kept strictly separate; no conflation or overwritten history. | None unless Owner supplies original U-8 text. |
| `CON-06` | `G1` approved remote model inference vs M2 scope and M3 OmniRoute `CLIENT_API` integration. | `CONSTRAINTS.md`, `ADR-AUTH-002.md`, `BLUEPRINT.md`. | M2 defines `G1` policy semantics only. M3 integration, approved ₹0 gateway allowlist, and OmniRoute `CLIENT_API` remain out of M2 and unmodified. | None for M2; M3 allowlist is a separate future decision. |
| `CON-07` | Google Drive backup vs ₹0 budget. | Owner request v1; C-01. | Proposed boundary requires free quota only; no paid storage or paid API. | Approve/reject free-quota boundary; if paid storage would be required, separate Owner decision is needed because C-01 forbids it. |
| `CON-08` | AVENOX ecosystem blueprint AOS roles vs AERON M2 Security Kernel roles. | Recovered Google Doc/DOCX version `1.0 | 18 November 2026`; user request roles. | Treated as `RECOVERED` context only, not AERON canonical spec. `Arena Agent`, `GPT-6`, and Founder authority are preserved as context; no AERON role policy invented. | None unless Owner wants the AOS context elevated to AERON canonical text. |
| `CON-09` | Drive full-text matches for `Authority policy`, `Backup scope`, `Option C/D/E` vs relevance. | Current connected-source searches. | Matches were unrelated JS/JSON/OmniRoute source artifacts, school/PDF docs, or generic `Option C` terms. OmniRoute docs were read only to confirm they are OmniRoute-specific; no OmniRoute modification or secret exposure. | None; source-noise documented. |
| `CON-10` | U-8R `Google Drive` backup vs OmniRoute preservation. | Owner request v1; C-05. | No contradiction. AERON backup storage is not OmniRoute configuration/data. No OmniRoute secret read or mutation. | None. |
| `CON-11` | Persistent contextual memory and multi-agent architecture vs M2 no premature modules. | Owner constraints; `BLUEPRINT.md`; `M1-FOUNDATION.md`. | Constraints preserved; M2 does not implement `Aeron.Memory`, `Aeron.Agents`, or `Aeron.Execution`. U-6/U-7 reconstructions define boundaries only. | None for M2 specification; future milestone decisions remain separate. |

## U-7 archival and privacy reconciliation (`UNRESOLVED`)

U-7-D records permanent archive of all conversations; U-7-B requires asking before sensitive or personal information is stored; U-7-E records deletion, correction, retention and privacy controls. These are distinct historical records, not a resolved hierarchy. The technical scope, consent, retention and deletion behavior, including backups and recovery, requires Owner review; no automatic exception or precedence is invented. See `U-07.md`.

## Status

`PROPOSED` reconciliations; no historical decision changed. `UNRESOLVED` items are carried into `OWNER-DECISION-REQUIRED.md` without asking the Owner to repeat known decisions.

## v1.1 reconciliation and unresolved conflicts

- `SUPERSEDED`: v1.0 ten-item summary was not the fifteen-decision record; no mappings invented. See `HISTORICAL-CORRECTION-REPORT.md`.
- U-8R.2 Shared Authority and U-8R.15 Shared Governance remain distinct from ADR-AUTH-001 C Core-Led Governance, ADR-AUTH-002 C Restricted Dual Authorization and ADR-AUTH-003 B Complete Owner Authority. Owner-reported reconciliation is bounded delegation and ultimate Owner authority; precise operating mechanics are `PROPOSED`, not a rewriting of either selection.
- Complete AERON application-data backup and no raw secrets/credentials in exposed backup may require exclusions, re-enrollment or protected recovery. No silent definition of “complete” is made; resolve in review.
- H1–H4 text was inspected by GPT-6 per Owner; Arena did not independently inspect original bytes, so byte-level provenance remains unresolved. Historical 5 TB / ~1% utilization is not current verified quota and does not override ₹0.
- Exact-operation binding must not collapse materially distinct values under redaction; local audit chains need an independently protected checkpoint or equivalent against full replacement/truncation; preemption must specify an irreversible external side-effect boundary. All three mechanisms are `PROPOSED` for review.
