# M2 Acceptance Criteria

> Status: `PROPOSED / NOT IMPLEMENTATION`.

## 1. Spec-pack v1.1 acceptance criteria

The specification pack is technically coherent and ready for Owner review when all of the following are true:

1. `docs/specifications/M2/` contains all required README, U-01…U-08R, ADR-AUTH-001…003, threat-model, architecture, implementation-plan, Owner-decision, recovery-matrix, provenance, historical/reconstructed, contradiction, acceptance, and dependency-graph files.
2. Every canonical U/ADR document has distinct recovered/confirmed, `PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW`, unresolved-question, and dependency sections.
3. No reconstructed document is described as an original historical specification.
4. No proposed document is marked `CONFIRMED`.
5. All fifteen U-8R IDs, answer letters and recorded labels are preserved unchanged and marked `CONFIRMED` in the Owner request.
6. Reported labels/descriptions are preserved; original questions and alternatives remain `UNRESOLVED` except the Owner-reported U-8R.6 list. No unreported alternatives are invented.
7. Original U-8 remains separate from replacement U-8R.
8. Existing architectural constraints and D-M1-01…D-M1-06 are preserved.
9. `RECOVERED` content has source/version information in `SOURCE-AND-PROVENANCE-REGISTER.md`.
10. `DECISION-RECOVERY-MATRIX.md` assigns one of `RECOVERED`, `CONFIRMED`, `PROPOSED`, `UNRESOLVED`, or `SUPERSEDED` to every reconstructed decision.
11. `OWNER-DECISION-REQUIRED.md` contains only genuinely missing Owner inputs and excludes known decisions.
12. All 11 threats have attack surface, security objective, prevention, detection, recovery, and required automated tests.
13. All five Security Kernel components cover their required topics.
14. `M2-DEPENDENCY-GRAPH.md` preserves the actual project-reference graph and introduces no premature module or third-party agent framework.
15. No C# source, application test, CI workflow, project reference, package, PR state, tag/release, OmniRoute configuration/data, local model/runtime, or paid API was changed.
16. No commit or push was performed.
17. Final status is `M2 SPECIFICATION v1.1 — PROPOSED`.

## 2. Future M2 BUILD acceptance criteria — NOT AUTHORIZED

These criteria apply only after explicit Owner `M2 BUILD` authorization. None were implemented or executed here.

| Area | Future acceptance criterion |
|---|---|
| Architecture | `Aeron.Contracts` remains zero-dependency; `Aeron.Security` references only `Aeron.Contracts`; `Aeron.Infrastructure` references `Aeron.Contracts` + `Aeron.Security`; `Aeron.Core` references `Contracts` + `Infrastructure` + `Security`; `Aeron.Desktop` references `Contracts` + `Core`. Any future `Aeron.Platform.Windows` reference must be acyclic and Owner-approved. |
| Build/CI | Strict C# 14 / .NET 10 LTS Release build and `win-x64` publish with zero warnings/errors; deterministic build; analyzers; Windows CI green; no banned packages, local model weights, skill scripts, or third-party agent frameworks. |
| Default deny | Every `G1`–`G4` / `L0`–`L3` combination has explicit tests; missing/ambiguous/unversioned/contradictory requests deny. |
| Owner authority | Pause, cancel, revoke, reconfigure, emergency stop, conflicting instructions, and agent inheritance tests prove Owner preemption and fail-closed behavior. |
| Approval | Explicit request, parameter hash, expiry, cancellation, denial, mismatch, replay, self-approval prevention, and immediate pre-execution revalidation tests pass; no side effect before approval. |
| Parameter hash | Protected exact-operation binding distinguishes materially different actions even when display redaction is identical; deterministic canonicalization, secret protection and mismatch denial are tested. |
| AuditLedger | Hash chain verification on startup/CI; protected checkpoint or equivalent detects whole-chain replacement/truncation; append-only; SQLite WAL; redacted recovery; no secret leakage. |
| Vault | DPAPI/Credential Manager current-user round-trip; Windows ACLs; zero secrets in `aeron.config.json`; inline-secret rejection; no OmniRoute secret read/modification; no third-party vault dependency without justification. |
| Redaction | Logs, structured properties, nested exceptions, SQLite journals, approval UI, audit records, and test diagnostics redact synthetic secrets; fail closed on redaction error. |
| Backup | Confirmed `Google Drive` storage choice preserved; restricted private folder; `G2`/`G4` approval; secret redaction; ₹0 quota; no personal filenames/private `Documents` listings; no raw secrets. |
| Crash handling | Approval, audit, vault, and execution preflight crash-injection tests prove no duplicate side effect, no silent repair, and deny-on-uncertain-state. |
| M2 freeze | Owner acceptance evidence, SHA-256 artifact provenance, formal review, and tag/release preparation; M3 remains unstarted. |

## 3. Status

Owner review and unresolved original byte-integrity/version verification remain open; GPT-6 text inspection is reported separately from Arena access; document consistency can be checked separately. Future M2 BUILD acceptance is `NOT AUTHORIZED` and remains blocked until Owner review and explicit authorization.

## v1.1 extra acceptance gates

- Verify Owner-supplied 15 selections and three ADRs, U-8R.6 five alternatives, .4 four encryption elements, .9 six device details, .11 complete AERON application data, .15 bounded authority/no self-elevation, and unresolved .5 policy.
- Historical H1–H4 texts were inspected by GPT-6 per Owner; Arena did not inspect or hash the originals. Do not assert Arena byte-level verification or mark source-level acceptance passed.
- Future tests demonstrate exact binding despite redaction; checkpoint detection of whole-chain replacement/truncation; preemption around execution boundary and honest handling of irreversible external actions; MFA and device trust independently of DPAPI; complete backup scope reconciliation.
- Owner specification lock, existing authoritative U-8R gate, and separate explicit BUILD authorization are all required; no M2 build now.
