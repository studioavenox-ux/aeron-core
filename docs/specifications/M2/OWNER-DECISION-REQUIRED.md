# Owner decision register — v1.1.1

> `PROPOSED` review items only; do not restate any confirmed selection. GPT-6 source-text review is reported by the Owner; H1–H4 were not accessible to Arena for independent inspection or hashing.

## Historical and evidence questions (`UNRESOLVED`)

1. If available, make H1–H4 accessible for Arena’s independent byte-level integrity, hash, version and per-item provenance verification; GPT-6’s reported text review has already occurred. The Owner's supplied fifteen selections and three ADR decisions are already confirmed and are not requested again.
2. Original U-8 canonical content, source, scope and relationship to U-8R remain unresolved; never merge the two.
3. Exact original U-1–U-7 and ADR-AUTH-001–003 full wording, source versions and missing clauses remain unresolved. The five U-7 records and known U-7-E question are preserved; do not ask the Owner to repeat selections.
4. Full original U-8R.1–.15 question wording and alternative sets remain unresolved except the Owner-reported .6 alternatives. Obtain where available; do not invent or request re-selection.
5. U-8R.5 custom privacy/security follow-up was explicitly not completed: determine its policy without changing selection E.

## Memory design reconciliation (`UNRESOLVED`)

Clarify how permanent conversation archival coexists with asking before storing sensitive/personal information and with review, correction, deletion, retention, backup and recovery controls. Determine sensitivity classification, consent workflow, storage and encryption mechanisms and retention policies without changing the five recorded decisions.

## Design decisions requiring review (`PROPOSED` / `UNRESOLVED`)

6. MFA factors, enrollment, recovery and fallback; device identity/trust and remote revocation; Windows isolation mechanism; encryption algorithms, key management and recovery; security-notification sensitivity, coverage and controls.
7. Complete AERON application-data backup boundaries versus exclusion/protection of credentials and secrets; Drive account/folder permissions, per-operation G2/G4 approvals, retention, integrity, restoration, protected-key recovery and ₹0 quota. Historical 5 TB / ~1% is not current verification. Proposed pre-upload encryption, separate key management, integrity verification and controlled restoration are not separately confirmed historical decisions.
8. Approve/revise the exact-operation binding separate from display redaction, independently protected audit checkpoint or equivalent, and revocation/irreversible-action execution boundary. Define failure and recovery behavior; no promise of reversing an already-started side effect.
9. Review proposed component specs, threat model, dependency graph and acceptance criteria, including how Core-led routine governance and shared authority operate under Owner supremacy without self-elevation. Do not rewrite U-8R.2/.15 or ADR selections.
10. Review and explicitly approve the v1.1.1 specification lock only after evidence and decisions are reconciled. Existing U-8R authoritative-specification gate remains in force unless explicitly reconciled by the Owner. Separate explicit `M2 BUILD` authorization is needed later; this request does not authorize implementation.
