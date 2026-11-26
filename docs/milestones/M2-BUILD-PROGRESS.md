# M2 BUILD — implementation progress (not milestone acceptance)

## Authority and provenance

The Project Owner explicitly authorized M2 BUILD against specification pack v1.1.1 and subsequently directed implementation to continue. The 15 U-8R selections and the three ADR-AUTH selections are already recorded; none is reselected here. Original U-8 remains separate and unrecovered.

The Owner reports that GPT-6 retrieved and inspected the historical H1–H4 texts in ChatGPT Library. Arena did not independently open or hash those original bytes, and this document does not assert original file versions or signatures. See `docs/specifications/M2/SOURCE-AND-PROVENANCE-REGISTER.md` for the existing evidence boundary.

The Owner confirmed v1.1.1 as the authoritative implementation baseline and granted M2 BUILD. The implementation gate in `docs/governance/U-08R-REPLACEMENT.md` is satisfied for this build by that confirmation; historical source recovery is a separate matter. Original U-8 remains unresolved and original file byte hashes unavailable; no signatures or independent verification are claimed. Earlier blocked-status statements in the pack predate this authorization. U-8R selections are preserved, not reselected.

## Phase A status

**PHASE A — WINDOWS CI BUILD AND TESTS PASSED; SECURITY IMPLEMENTATION INCOMPLETE.** This refers to PR #4's CI, associated with head `b6101fe7990eb8ed7041411676e4dd4b18884e15`, not the later uncommitted correction or Phase B code. The Owner reports 76 tests passed and a zero-warning Release build; GitHub confirms the CI job succeeded, but its logs could not be retrieved in this environment to independently verify those totals. No physical APEX runtime verification or M2 acceptance is implied. See `M2-PHASE-AB-CORRECTION-REVIEW.md` for reconciliation, unvalidated changes and remaining boundaries.

## Current code boundary

- `AeronFoundationHost.SecurityPolicy` exposes a policy instance with **no production identity provider or grant store**. It denies requests rather than authenticating anyone by fallback.
- `OwnerAuthorityGuard` and `PolicyDecisionEngine` use explicitly injected trusted providers and a versioned, conservative mapping of independently recorded G/L constraints. Classification and action type must come from a trusted executor boundary; callers cannot safely supply model-generated claims. There is no production M2 action executor yet.
- `OwnerApprovalService` and `OperationBinding` provide a volatile, in-process implementation for subsequent integration. An injected trusted identity provider, permission store, trusted clock, and durable audit sink are mandatory. None is currently wired into the production application. The service is **not a dispatch boundary**, and successful consumption must not be used as authority for external side effects. It does not establish MFA, a protected UI origin, persistent audit, device trust or crash recovery.
- HMAC-SHA-256 uses a random per-instance key and versioned length framing; inputs are not persisted in approval entries. Disposal/restart invalidates all volatile approvals. This is not a DPAPI vault or durable key strategy. Bindings, action classification and exact bytes must be revalidated at the eventual actual dispatch boundary.
- The in-memory approval cap is an engineering resource bound, not a memory retention decision. An audit write failure latches the service unavailable; there is no in-place audit recovery. Revocation within the service is serialized with consumption; revocation in an independent grant store and external dispatch are not yet atomically coordinated. The clock must be trusted: a backwards wall-clock change before expiry is observed can delay expiration.
- M1 redaction changes are defense in depth. Short registered values suppress matching fields; this can suppress ordinary text and cannot prove that unknown secrets or coincidentally identical schema text are absent. Registration concurrent with logging has no global execution barrier.

## Remaining work

Production MFA/device enrollment and recovery, protected Owner UI, policy provisioning, comprehensive permission/session/device revocation, durable independently anchored audit, Windows vault, privacy/memory controls, emergency response, security notifications, backup/restore, and WPF action-boundary integration remain unfinished. Google Drive remains the recorded destination, not standing approval to upload. No side-effect dispatch is authorized by this prototype.

The available sandbox is Linux without `dotnet`; CI on the earlier PR cannot validate the later uncommitted correction or Phase B changes. APEX is not exposed to this session. Syntax parsing is not compilation or execution. Apart from the earlier CI result noted above, no new build, tests, M2 runtime, acceptance or performance result is claimed here.
