# M2 Security Kernel — Proposed Architecture and Component Specifications

> Status: `PROPOSED / NOT IMPLEMENTATION`.  
> Final pack status: `M2 SPECIFICATION — PROPOSED`.  
> No C# source, project reference, package, test, CI workflow, or OmniRoute configuration was changed.

## 1. Preserved project dependency structure

Actual current project references must be preserved:

- `Aeron.Contracts`: no project or package references; `net10.0`.
- `Aeron.Security`: references `Aeron.Contracts` only; `net10.0`.
- `Aeron.Infrastructure`: references `Aeron.Contracts`, `Aeron.Security`; `net10.0`; `Microsoft.Data.Sqlite`.
- `Aeron.Core`: references `Aeron.Contracts`, `Aeron.Infrastructure`, `Aeron.Security`; `net10.0`.
- `Aeron.Desktop`: references `Aeron.Contracts`, `Aeron.Core`; `net10.0-windows` WPF `WinExe`.
- Future `Aeron.Platform.Windows`: proposed only; must not create a cycle. It should implement `IWindowsCredentialVault` from `Aeron.Contracts`; `Aeron.Security` consumes the interface, and `Aeron.Core`/`Aeron.Desktop` composition wires the implementation. No direct `Aeron.Security` → `Aeron.Platform.Windows` project reference is proposed.
- No premature `Aeron.Gateway`, `Aeron.Memory`, `Aeron.Agents`, `Aeron.Execution`, voice, avatar, or virtual-HQ modules.
- No third-party agent framework; no third-party vault dependency is justified or proposed.

Preserved constraints: C# 14 / .NET 10 LTS, WPF native Windows desktop, deterministic builds, `TreatWarningsAsErrors=true`, Windows 11 25H2 Build 26200 `x64`, ₹0 budget, remote-only inference, no local model weights/runtimes, no paid API fallback, OmniRoute untouched, Owner ultimate authority, default-deny security, permission-controlled tool execution, persistent contextual memory, multi-agent architecture, secure logging/audit/backups, and `%LOCALAPPDATA%\Aeron` user-ACL storage outside cloud-synced `Documents` and OmniRoute directories.

## 2. Proposed interfaces and responsibilities

All interface names below are `PROPOSED`; no code is implemented.

| Interface / component | Responsibility | Proposed contract shape |
|---|---|---|
| `IActionClassifier` | Convert a request into a typed action classification and authority tier | `Classify(ActionRequest) -> ActionClassification` |
| `IDataClassifier` | Classify data as `Secret`, `LocalPrivate`, or `RoutineContext` without emitting raw values | `Classify(RedactedPayload) -> DataClassification` |
| `IDestinationValidator` | Validate exact source/destination and reject ambiguous or unapproved destinations | `Validate(ActionRequest) -> DestinationValidation` |
| `IPolicyVersionProvider` | Provide immutable policy version/hash for evaluation evidence | `Current() -> PolicyVersion` |
| `IParameterHasher` | Bind all material operation parameters securely, separately from redacted display/audit; SHA-256 design proposed | `Compute(CanonicalActionRequest) -> ParameterHash` |
| `IPolicyDecisionEngine` | Deterministic default-deny evaluation across `G1`–`G4` / `L0`–`L3` | `Evaluate(ActionRequest) -> PolicyDecision` |
| `IOwnerAuthorityGuard` | Non-bypassable Owner preemption and authority precedence | `Authorize(ActionRequest, PolicyDecision) -> AuthorityResult` |
| `IOwnerApprovalService` | Explicit, single-use, expiring, parameter-bound approvals | `RequestAsync`, `ValidateBeforeExecutionAsync`, `CancelAsync`, `DenyAsync` |
| `IAuditLedger` | Append-only hash-chained redacted audit records | `AppendAsync`, `VerifyChainAsync`, `LastVerifiedHeadAsync` |
| `IWindowsCredentialVault` | DPAPI/Credential Manager secret lifecycle abstraction | `StoreAsync`, `RetrieveAsync`, `RotateAsync`, `DeleteAsync` |
| `IRedactionFilter` | Existing M1 redaction contract, reused and extended | `Redact`, `ContainsPotentialSecret` |

## 3. A. OwnerAuthorityGuard — `PROPOSED`

Coverage:

- **Owner authority:** ultimate, non-delegable; AERON Core and specialist agents are bounded delegates.
- **Pause/cancel/revoke:** any Owner pause, cancel, revoke, reconfigure, or emergency-stop directive immediately preempts pending approvals and denies new consequential actions.
- **Emergency shutdown:** proposed preemptive state prevents queued actions from starting after the effective revocation boundary; pending approvals are invalidated and preemption audited. For an external side effect already started, cancellation is best-effort: it may be irreversible; record and reconcile the outcome, without promising rollback.
- **Authorization inheritance across agents:** agents inherit only bounded permissions from AERON Core within Owner limits; no agent may delegate Owner authority or bypass Core/Owner oversight.
- **Precedence when instructions conflict:** Owner preemption first; explicit current policy second; default deny last. Untrusted model/agent text never becomes policy.
- **Fail-closed behaviour:** deny if Owner state, policy version, canonical-specification status, approval binding, or audit-chain verification is unavailable.

Failure states: `OwnerPreempted`, `AuthorityStateUnknown`, `PolicyVersionMismatch`, `AgentAuthorityExceeded`, `AuditUnavailable`. All deny by default.

## 4. B. PolicyDecisionEngine — `PROPOSED`

Coverage:

- **G1–G4 governance levels:** preserved from `CONSTRAINTS.md` / `ADR-AUTH-002.md` confirmed rules. `G1` is approved zero-cost remote inference without repeated per-message prompts but with non-bypassable guards; `G2` restricted disclosure requires exact source/destination approval; `G3` external communications and `G4` external account modifications require per-action parameter-hashed approval. M2 defines policy only; M3 OmniRoute `CLIENT_API` integration is out of scope.
- **L0–L3 authority levels:** `L0` isolated AERON internal state; `L1` bounded safe reads/drafts; `L2` consequential local actions require explicit Owner approval; `L3` forbidden local actions hard-denied.
- **Action classification:** every request must have actor, action type, parameters, data classification, destination, policy version, and requested side effect.
- **Data classification:** `Secret` hard-blocked/redacted; `LocalPrivate`/restricted requires `G2` approval; `RoutineContext` only within approved disclosure policy.
- **Destination validation:** exact source and destination must be known; unapproved, ambiguous, changed, or missing destinations deny.
- **Policy versioning:** every decision records immutable policy version/hash; version change invalidates pending approvals.
- **Ambiguous-request rejection:** missing, contradictory, or unclassifiable requests deny with redacted reason.
- **Policy evaluation evidence:** redacted record containing classification, matched rule, policy version, decision, approval requirement, and hash references is written to AuditLedger.

Failure states: `UnclassifiedAction`, `UnknownDataClass`, `UnknownDestination`, `PolicyVersionUnavailable`, `AmbiguousRequest`, `CanonicalSpecUnresolved`. All deny.

## 5. C. OwnerApprovalService — `PROPOSED`

Coverage:

- **Explicit approval requests:** human-readable redacted summary for `G2` restricted disclosure, `G3`, `G4`, and `L2`; no side effect before explicit Owner decision.
- **Parameter-hashed requests:** proposed approval binding must cover every material parameter of the exact operation; redacted display/audit and the protected binding representation must be separate. The safe binding mechanism, including handling secrets and low-entropy values, requires review.
- **Approval expiration:** approvals expire; expired approvals deny.
- **Cancellation and denial:** Owner cancellation, Owner denial, and emergency preemption invalidate pending approvals.
- **Parameter mismatch detection:** recompute the protected binding immediately before execution; mismatch denies. Redaction must not collapse materially different operations.
- **Revalidation immediately before execution:** recheck Owner state, policy version, expiry, single-use state, destination, and hash immediately before side effect.
- **Owner preemption:** OwnerAuthorityGuard cancels/invalidates pending approvals on preemption.
- **Prevention of self-approval:** AERON Core, agents, background services, and tool runners cannot approve their own requests; only explicit Owner decision can set approval state.

Failure states: `Pending`, `Approved`, `Denied`, `Expired`, `Cancelled`, `Preempted`, `ParameterMismatch`, `PolicyVersionChanged`, `AlreadyUsed`. Only `Approved` plus successful immediate revalidation may proceed.

## 6. D. AuditLedger — `PROPOSED`

Coverage:

- **Hash-chained audit records:** sequence, UTC time, actor, action classification, policy version, decision, approval hash, previous hash, entry hash, redacted outcome, redacted error code.
- **Append-only persistence:** no update/delete path for committed records; corrections append compensating redacted records.
- **SQLite WAL:** reuse existing `%LOCALAPPDATA%\Aeron\data` SQLite WAL, transactional migration, and checksum discipline.
- **Tamper detection:** proposed startup verification recomputes the chain; an independently protected checkpoint or equivalent must be designed to detect complete replacement and truncation. A hash chain alone does not provide that guarantee. Redaction checks are separate, fail closed, and do not guarantee discovery of every secret.
- **Secret redaction:** every field passes through `IRedactionFilter` before persistence; raw secrets never enter logs, prompts, memory, audit, approval UI, or backups.
- **Audit verification on startup:** startup denies sensitive operations if chain verification fails.
- **Recovery and failure handling:** preserve last verified chain head; deny new sensitive operations; append redacted failure record if storage is available; Owner-reviewed recovery only; no silent repair or truncation.

Failure states: `ChainHeadUnavailable`, `ChainMismatch`, `WriteFailed`, `WALCorrupt`, `RedactionFailed`, `StorageUnavailable`. All deny sensitive operations.

## 7. E. Windows Security Vault — `PROPOSED`

Coverage:

- **Windows DPAPI:** current-user DPAPI protection for local secret blobs is proposed; no raw secret in config. This is not an MFA implementation or proof of the complete encryption and backup scope.
- **Windows Credential Manager:** current-user credential records for secret lifecycle where appropriate; no OmniRoute secret reads.
- **Current-user protection:** DPAPI/Credential Manager scoped to the current Windows user; no machine-wide or cross-user secret exposure.
- **Windows ACLs:** existing `WindowsAclHelper` / `%LOCALAPPDATA%\Aeron` current-user ACL protection; no `Documents` or OmniRoute directories.
- **Secret lifecycle:** create/store, retrieve, rotate, delete, and fail-closed on missing/corrupt/unavailable secret state; no secret values in logs, prompts, memory, audit, or `aeron.config.json`.
- **Backup and recovery constraints:** U-8R `Backup storage = Google Drive` is preserved, but backup design must use restricted private folder, explicit `G2`/`G4` approval, secret redaction, no raw secrets, ₹0 quota, and no unrestricted cloud sync. Backup of DPAPI-protected blobs requires an Owner-approved recovery procedure; no assumption that DPAPI secrets are portable across users/machines.
- **Zero secrets in `aeron.config.json`:** `ConfigPolicyValidator` rejects inline credentials; config stores only non-secret references/identifiers.
- **Third-party vault dependencies:** none proposed. Built-in Windows DPAPI/Credential Manager and free/OSS .NET libraries only. Any future third-party vault dependency would require explicit justification and Owner approval.

Failure states: `VaultUnavailable`, `DecryptFailed`, `CredentialNotFound`, `ACLMismatch`, `InlineSecretRejected`, `BackupUnsafe`. All deny sensitive operations.

## 7A. v1.1 Owner-reported requirements and proposed reconciliation

Source: Owner v1.1 request Parts 2, 4, 8 and 10; GPT-6 inspected H1–H4 text per the v1.1.1 Owner clarification, but Arena did not independently inspect or hash the originals. Fifteen U-8R selections and the three separate ADR decisions are `CONFIRMED`; original questions/alternatives and custom U-8R.5 policy remain `UNRESOLVED`. All mechanisms below are `PROPOSED`, not historical Owner selections.

- **OwnerAuthorityGuard:** Owner supremacy, Core-led routine governance, bounded shared authority, no agent self-elevation, emergency preemption and immediate permission revocation. Revocation propagates across delegated agents; the execution-boundary design must prevent later starts and reconcile already-started actions without promising reversal.
- **PolicyDecisionEngine:** default deny; controlled full autonomy only within delegated permissions; controlled intelligent access; risk-based device identity and authorization; revocation; G1–G4 and L0–L3. No model, message, agent or skill may override policy.
- **OwnerApprovalService:** Core may propose governance-policy amendments, which require Owner approval before effect; consequential actions require the independently mandated Owner approval. Exact operation binding, revocation, cancellation and no self-approval are proposed mechanisms.
- **AuditLedger:** comprehensive audit coverage, logging, integrity/tamper detection, controlled access and recovery, security notifications and revocation evidence, with privacy and secret redaction. Design a separately protected trusted checkpoint or equivalent to detect entire-chain replacement and truncation; define how rollback and checkpoint loss fail closed.
- **Windows Security Vault / authentication boundary:** MFA is confirmed but factors, enrollment and recovery are unresolved. Device trust, per-device permissions and remote revocation require a separate authentication/device layer; DPAPI and Credential Manager alone do **not** implement MFA. Encryption at rest, communications, key management and protected backups are confirmed scope; algorithms, key recovery, credentials, restore portability and enforcement are proposed/unresolved. Never infer the custom U-8R.5 policy.
- **Exact binding review:** materially distinct actions must not map to the same approval representation due to redaction. Use a protected, domain-separated, exact-operation binding separate from human-readable redaction; avoid persisting or exposing secrets and protect against low-entropy guessing. Concrete cryptographic scheme and lifecycle are `PROPOSED` and require review.
- **Recovery safeguards:** pre-upload encryption, separate key management, integrity verification and controlled restoration are `PROPOSED`; do not treat these additional safeguards as historical selections. Complete application-data scope versus excluded credentials/secrets requires explicit reconciliation.

## U-7 memory boundary (v1.1.1)

Five separate records are preserved in `U-07.md`: the Personal Knowledge System direction, privacy preference, D Hybrid Cognitive Memory backend, permanent conversation archive, and D Complete Contextual Memory identity. The identity does not replace the other decisions or establish the complete original U-7 body. Asking before storing sensitive or personal information, never storing passwords/authentication secrets in memory, and the recorded review/export/deletion controls must be reconciled with permanent archival intent; no technical hierarchy or storage implementation is invented. Memory-engine implementation remains outside M2. Any security mechanisms remain `PROPOSED`.

## 8. Data flow

1. Request enters from UI/Core/future agent boundary as untrusted `ActionRequest`.
2. `IRedactionFilter` redacts any display/log payload.
3. `IActionClassifier`, `IDataClassifier`, and `IDestinationValidator` produce typed classifications.
4. `IPolicyVersionProvider` supplies current policy version/hash.
5. `IPolicyDecisionEngine` evaluates default-deny across `G1`–`G4` / `L0`–`L3`.
6. `IOwnerAuthorityGuard` checks Owner state/preemption and precedence.
7. If consequential, `IOwnerApprovalService` creates a parameter-hashed explicit approval request.
8. Immediately before execution, approval is revalidated and the protected exact-operation binding recomputed at the proposed atomic execution boundary.
9. `IAuditLedger` appends redacted pre-execution policy, approval and intent evidence.
10. A side effect starts only after all prior steps allow it and the execution boundary is checked; afterward, append redacted outcome evidence. If an external outcome is uncertain, reconcile it rather than claiming rollback.

## 9. Failure-state summary

Every unknown, missing, expired, mismatched, preempted, unredacted, tampered, or unresolved condition denies. Sensitive operations are blocked on audit-chain failure, policy-version mismatch, unresolved canonical spec, Owner preemption, approval mismatch, vault failure, or unsafe backup state.

## 10. Acceptance criteria

See `M2-ACCEPTANCE-CRITERIA.md`. No implementation or test execution occurred in this specification-development task.

## 11. Status

`PROPOSED / NOT IMPLEMENTATION`. Implementation remains blocked until Owner review and explicit `M2 BUILD` authorization.
