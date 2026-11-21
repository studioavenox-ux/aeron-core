# M2 Implementation Plan — Proposed Stages Only

> Status: `PROPOSED / NOT AUTHORIZED`.  
> This is specification development, not implementation authorization.  
> No code, tests, CI, project references, packages, tags, releases, PR actions, or OmniRoute changes were made.

## 0. Hard gate

Implementation remains blocked until the Owner reviews the specification pack and explicitly authorizes the `M2 BUILD` milestone. The Owner must also resolve the genuinely missing inputs in `OWNER-DECISION-REQUIRED.md`.

## 1. Proposed stages

| Stage | Name | Proposed scope | Exit gate |
|---|---|---|---|
| M2.S0 | Specification review | Owner reviews all `PROPOSED RECONSTRUCTION` sections, `CONFIRMED` U-8R selections, `UNRESOLVED` definitions, contradiction report, threat model, architecture, acceptance criteria, and dependency graph. | Owner accepts/rejects spec pack v1.1.1 and supplies or defers missing historical texts/option definitions. |
| M2.S1 | Security contracts | Add typed M2 contracts only after authorization: action request, classification, policy decision, approval, parameter hash, audit entry, vault interface. | `Aeron.Contracts` remains zero-dependency; architecture tests pass; no secrets in contracts. |
| M2.S2 | Parameter hashing and redaction boundary | Protected exact-operation binding, separate from redacted display and audit; extend redaction test fixtures with synthetic secrets only. | Deterministic hash tests; mismatch tests; redaction tests; no raw secret in logs/audit/prompts. |
| M2.S3 | PolicyDecisionEngine + OwnerAuthorityGuard | Default-deny `G1`–`G4`/`L0`–`L3` evaluation, Owner preemption, precedence, ambiguous-request rejection, policy versioning, evaluation evidence. | Full policy matrix tests; preemption tests; conflict tests; fail-closed tests. |
| M2.S4 | OwnerApprovalService | Explicit approval requests, expiry, cancellation, denial, single-use approval IDs, immediate pre-execution revalidation, self-approval prevention. | Approval lifecycle tests; replay/mismatch/expiry/cancel tests; no side effect before approval. |
| M2.S5 | AuditLedger | Append-only hash-chained SQLite WAL records, startup verification, tamper detection, redacted recovery/failure handling. | Chain verification tests; tamper tests; WAL rollback/crash tests; no secret leakage. |
| M2.S6 | Windows Security Vault | Future `Aeron.Platform.Windows` DPAPI/Credential Manager adapter; current-user ACLs; zero secrets in `aeron.config.json`; backup/recovery constraints. | Windows-only vault/ACL tests; inline-secret rejection; no OmniRoute modification; no paid service. |
| M2.S7 | Security CI and architecture enforcement | Extend Windows CI and architecture/hygiene tests for M2 components, banned packages/weights/frameworks/skill scripts, redaction, audit chain, and project-reference graph. | Windows CI green; zero warnings; all proposed security tests pass; no premature modules. |
| M2.S8 | Owner acceptance and M2 freeze | APEX/Windows interactive verification, evidence preservation, SHA-256 artifact provenance, formal review, M2 release/tag preparation. | Owner acceptance; M2 frozen; M3 remains unstarted and requires approved ₹0 gateway allowlist. |

## 2. Out of scope for M2

- M3 OmniRoute `CLIENT_API` integration, paid APIs, local LLM runtimes, model weights, skill-script execution, third-party agent frameworks, contextual-memory engine, multi-agent engine, compiled tool runner, voice, avatars, virtual HQ, PR #3 merge/approval/closure, M1 release/tag, and any OmniRoute modification.

## 3. Budget and preservation

₹0 budget is preserved: free/OSS and built-in Windows APIs only. Existing project dependency graph is preserved. Application source/tests remain unchanged until explicit M2 BUILD authorization.

## 4. Status

`PROPOSED / NOT AUTHORIZED`. Implementation remains blocked until Owner review and explicit `M2 BUILD` authorization.

## v1.1.1 design-review prerequisites (`PROPOSED`)

S0 must reconcile all 15 Owner-confirmed selections, three ADRs, reported U-6/U-7 and source integrity. GPT-6 inspected H1–H4 text per Owner; Arena has not independently inspected or hashed the originals, and source-integrity and governance gates remain unresolved. S2 must design exact-operation binding that distinguishes values collapsed by redaction, without disclosing secrets. S3/S4 must specify revocation atomicity and already-started irreversible external actions. S5 must design a separately protected checkpoint or equivalent against complete chain replacement and truncation. S6 must treat MFA and device trust as separate unresolved designs; DPAPI/Credential Manager alone do not provide MFA. Specify complete application-data backups versus excluded secrets, restoration and ₹0 before BUILD. These are proposals, not additional historical selections.
