# M2 BUILD — implementation progress (not milestone acceptance)

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.
>
> Last updated: M2 completion-directive work on branch `arena/f3aabf10-aeron-core`,
> branched from `main` at `6a53fed24918b3e76f0f39ad70c5e4218c64bfaa`.

## Authority and provenance

The Project Owner explicitly authorized M2 BUILD against specification pack v1.1.1 and
subsequently directed implementation to continue (Milestone 2 Completion Directive). The
15 U-8R selections and the three ADR-AUTH selections are already recorded; none is
reselected here. Original U-8 remains separate and unrecovered.

The Owner reports that GPT-6 retrieved and inspected the historical H1–H4 texts in
ChatGPT Library. Arena did not independently open or hash those original bytes, and this
document does not assert original file versions or signatures. See
`docs/specifications/M2/SOURCE-AND-PROVENANCE-REGISTER.md` for the existing evidence
boundary.

## What this build actually delivers

The security kernel is now a real, wired, tested subsystem rather than a set of
unconnected prototypes. The authoritative description of the code is
`docs/specifications/M2/M2-IMPLEMENTED-ARCHITECTURE.md`.

| Area | Status | Truthful summary |
|---|---|---|
| Identity authority | `COMPLETE` | Windows SID + approved-factor policy + per-agent ECDSA sessions; fails closed |
| Owner MFA | `BLOCKED` | No approved factor/enrollment/recovery policy → **no Owner session can be minted** (D2-01/D2-02) |
| Authority / permissions | `COMPLETE` for in-scope actions | Deterministic default-deny evaluator; no dispatch path for G1–G4 or L2 mutation |
| Permission store | `PARTIAL` | Volatile; Owner re-grants per process start (default deny on restart) |
| Approval engine | `COMPLETE` | Durable, exact-binding, single-use, Owner-only decision, epoch-guarded dispatch |
| Approval role separation | `PARTIAL` | In-process approver marker only; OS separation blocked on D2-08 |
| Durable audit ledger | `COMPLETE` | Hash chain, truncation/replacement detection, explicit-only crash recovery |
| Independent checkpoint | `PARTIAL` | Same-user file anchor; rollback resistance **not claimed** (D2-07) |
| Windows vault | `BLOCKED` | AES-256-GCM + DPAPI implemented; storage refused pending D2-05/D2-06 |
| Desktop surface | `PARTIAL` | Real status, real audit view, honest refusals; Owner controls locked |
| Tests | `COMPLETE` (authored) | 5 suites retained + 7 new M2 test files; see §Test status |
| Windows CI | `PENDING` | Requires the push of this branch; results reported by GitHub, not here |

## Deliberately unimplemented (not defects)

- **Owner authentication in production.** `UnapprovedOwnerFactorPolicy` never
  authenticates. This is the mandated fail-closed posture while D2-01/D2-02 are `OPEN`.
- **Vault storage.** Refuses until D2-05/D2-06 are resolved.
- **External dispatch.** No gateway, communications, account-modification, or local-mutation
  module exists in M2; those actions deny with `rule.no_m2_dispatch_path`.
- **Real OS isolation between Owner and agents.** Blocked on D2-04; the in-process boundary
  is described as in-process everywhere.

## Corrections made to earlier M2 code

Earlier M2 code bound approvals with a **process-volatile HMAC key** and kept approvals in
a dictionary, and the foundation host exposed a policy with no identity provider. Both were
prototypes. This build:

1. Replaced `OwnerApprovalService` in the production path with `ApprovalEngine` +
   `DurableApprovalStore` + `ActionDispatcher`. The old type is retained **only** as a
   legacy regression fixture (`LegacyApprovalPrototype.cs`) and is not wired.
2. Moved its contract types (`ApprovalEventKind`, `IApprovalAuditSink`, `ApprovalResult`,
   `ApprovalResultCode`) out of `Aeron.Contracts` into the legacy file so new code cannot
   reference them.
3. Changed the operation binding to accept a key from the platform store (DPAPI) with a
   volatile fallback that fails closed across restart, and upgraded framing to V3.
4. Wired a real kernel into `AeronFoundationHost` and the WPF shell, and added the desktop
   security surface.
5. Preserved M1 behavior: config invariants, redaction, SQLite, lifecycle, tray, single
   instance, and the Phase B boundary policy and its tests are unchanged.

## Honest statement of what is not verified

- **No build, test run, or Windows publish was executed in this environment.** The sandbox
  has no .NET SDK, no PowerShell, and no access to `dotnet`/NuGet endpoints. All compiled
  artifacts, test results, and CI status in this milestone must come from GitHub Actions
  on the pushed branch. Nothing in this repository claims otherwise.
- APEX runtime verification and M2 acceptance are not performed and not claimed.
- Syntax-level review and static structural checks were performed; they are **not**
  compilation and must not be described as such.

## Remaining work after this build

1. Resolve `docs/specifications/M2/M2-DECISION-REGISTER.md` entries D2-01 … D2-10.
2. Implement the approved Owner authenticator (D2-01), recovery (D2-02), device policy
   (D2-03), isolation (D2-04), vault policy (D2-05/D2-06), checkpoint protection (D2-07),
   approval role separation (D2-08), and emergency-stop reach (D2-09).
3. Persist permissions and the agent registry across restart once the Owner approves the
   provisioning model.
4. Privacy/memory controls, security notifications, backup/restore, and the M3 `G1`
   gateway remain out of M2 scope.
5. Windows CI green on the final commit and APEX runtime verification remain acceptance
   prerequisites that this environment cannot supply.
