# M2 Acceptance Criteria

> Status: `IMPLEMENTED (BUILD) — ACCEPTANCE NOT YET CLAIMED`.
>
> Section 1 and sections 2–3 were written before M2 BUILD was authorized and are retained
> unchanged for provenance. Section 4 (added by the M2 completion build) records the
> measured state of each acceptance gate against the code that now exists. No gate below is
> marked passed on the basis of a build or test run performed by an agent in a sandbox
> without a .NET SDK: those gates stay `PENDING CI` until GitHub Actions reports them.

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

## 4. Acceptance gate status — M2 completion build

Vocabulary matches the completion directive: `COMPLETE`, `PARTIAL`, `BLOCKED`,
`NOT STARTED`.

| # | Acceptance gate | Status | Evidence in the repository |
|---:|---|---|---|
| 1 | Authorized security components implemented | `PARTIAL` | Kernel implemented (`M2-IMPLEMENTED-ARCHITECTURE.md` §1); Owner MFA and vault storage blocked by open Owner decisions |
| 2 | Production composition wired correctly | `COMPLETE` (code) | `SecurityComposition.CreateKernel` called by `AeronFoundationHost.Create` before lifecycle start |
| 3 | Enforcement integrated with the real action boundary | `COMPLETE` (in-scope actions) | `ActionDispatcher` is the only side-effect gateway; no G1–G4/L2 dispatch exists in M2 |
| 4 | All required Owner decisions resolved | `BLOCKED` | 10 open entries in `M2-DECISION-REGISTER.md` |
| 5 | No unresolved critical security gaps | `BLOCKED` | D2-01/04/07/08 are security-critical and open |
| 6 | Release build zero warnings/errors | `PENDING CI` | Not executed here; no .NET SDK in this environment |
| 7 | All automated tests pass | `PENDING CI` | Test sources authored; execution unverified here |
| 8 | Windows CI green on final commit | `PENDING CI` | Requires pushed branch run |
| 9 | Windows runtime verification | `NOT STARTED` | Requires APEX |
| 10 | Documentation matches implementation | `COMPLETE` | Implemented architecture, decision register, and this table describe the code that exists |

### Per-area gate detail

| Area | Status | Note |
|---|---|---|
| Architecture invariants (acyclic graph, no new packages) | `COMPLETE` | Enforced by `Aeron.Architecture.Tests` |
| Build/CI | `PENDING CI` | See §1–§3 provenance; not run in sandbox |
| Default deny | `COMPLETE` | Every mapped action has explicit tests, including unknown/unversioned/secret |
| Owner authority | `PARTIAL` | Preemption and emergency stop implemented and tested; Owner authentication blocked (D2-01) |
| Approval | `COMPLETE` | Request, binding, expiry, cancel, deny, mismatch, replay, self-approval prevention, pre-execution revalidation all tested |
| Parameter binding | `COMPLETE` | Deterministic length-framed HMAC; distinguishable under identical redaction |
| Audit ledger | `PARTIAL` | Chain, truncation, replacement, crash-tail recovery tested; independent checkpoint blocked (D2-07) |
| Vault | `BLOCKED` | Implementation complete; storage refused pending D2-05/D2-06 |
| Redaction | `COMPLETE` | M1 tests retained; audit detail codes structurally cannot carry free text |
| Backup | `NOT STARTED` | Out of M2 build scope; blocked on D2-06 |
| Crash handling | `PARTIAL` | Audit crash window covered; approval-store crash window covered by atomic writes; execution crash injection not implemented |
| M2 freeze | `NOT STARTED` | Requires Owner acceptance and CI evidence |

### Evidence still required for final acceptance

1. GitHub Actions Windows CI run on the final commit SHA: build, five retained suites plus
   the new M2 suites, hygiene scan, `win-x64` publish, executable verification.
2. APEX interactive verification of the desktop security surface on Windows 11 25H2.
3. Owner resolutions for D2-01 … D2-10, or an explicit Owner acceptance of the documented
   fail-closed posture for each.
4. Owner acceptance statement and, if required, signed artifact provenance.
