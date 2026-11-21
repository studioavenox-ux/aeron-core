# AERON — M1 Finalization & M2 Preparation

> Status: `PROPOSED / PREPARATION ONLY`  
> Scope: Governance closeout preparation, canonical-specification recovery, and M2 architecture planning.  
> Implementation status: `NOT STARTED`. This document is not an M2 specification, does not supply missing canonical text, and does not authorize M2 development.

## 0. Scope, authority and workspace actions

- Current session branch: `arena/e01239ac-aeron-core`, based on `24d0804b445b429909dd16068da7e00cccc603a0`.
- PR #3 remains `OPEN`, `MERGEABLE`, `CLEAN`, head `857b3203fea13239c397113298e8b41b1dc67f93`, with `reviewDecision=""`.
- CI run `44538313577` is `SUCCESS` with 48/48 tests.
- Owner retains merge, review, release/tag, evidence-archive, and M2 implementation authority.
- Workspace actions taken on the authorized branch only:
  - `docs/governance/DECISION_LEDGER.md` modified locally to record `D-M1-06` as `CONFIRMED`.
  - This preparation report created.
  - No commit, no push, no PR merge, no GitHub approval, no tag/release, no application-source change, no test change, no M2 implementation, and no OmniRoute modification.
- The authoritative M1 verification runbook remains on PR #3 head. The ledger patch below is prepared on the session branch; the Owner must decide how/whether to apply it to the authoritative PR branch or post-merge `main`.

---

## 1. Updated Decision Ledger patch

The patch below is `git apply`-ready. It adds `D-M1-06` to the approved M1 decisions as `CONFIRMED`, records the 2026-11-19 Owner decision, records Owner approval as the source, and invents no signature.

```diff
diff --git a/docs/governance/DECISION_LEDGER.md b/docs/governance/DECISION_LEDGER.md
index 0f10c9e..be7efb3 100644
--- a/docs/governance/DECISION_LEDGER.md
+++ b/docs/governance/DECISION_LEDGER.md
@@ -29,7 +29,7 @@ Status vocabulary used strictly across all AERON records:
 
 ---
 
-## 2. Approved Milestone 1 Decisions (`CONFIRMED` — 2026-11-17)
+## 2. Approved Milestone 1 Decisions (`CONFIRMED` — 2026-11-17 and 2026-11-19)
 
 | ID | Decision | Status | Record |
 |---|---|---|---|
@@ -38,6 +38,9 @@ Status vocabulary used strictly across all AERON records:
 | **D-M1-03** | Closing the main window (`X`) minimizes AERON to the system tray by default (`Window.CloseToTray = true`) | `CONFIRMED` | [`ADR-TECH-001-DOTNET10-WPF.md`](ADR-TECH-001-DOTNET10-WPF.md) |
 | **D-M1-04** | Private repository policy with Proprietary — All Rights Reserved, Avenox Studio designation | `CONFIRMED` | [`LICENSE`](../../LICENSE) |
 | **D-M1-05** | Explicit Milestone 1 `BUILD` authorization on branch `arena/8ed7a07b-aeron-core` submitted via reviewable PR to `main` | `CONFIRMED` | [`../milestones/M1-FOUNDATION.md`](../milestones/M1-FOUNDATION.md) |
+| **D-M1-06** | Owner accepts the approximately 280 MB Working Set deviation for M1 closure only; the original M1 target of ≤120 MB is preserved; the performance target is not achieved and must not be claimed as achieved; memory profiling and optimization remain future work; this acceptance does not extend to M2 or later milestones | `CONFIRMED` | Owner approval (source; decision date 2026-11-19) |
+
+> **D-M1-06 source:** Owner approval, decision date 2026-11-19. No signature or external record reference was supplied.
 
 ---
 
```

Validation: `git diff --check` passed; the patch is the only tracked-file change. The PR-head ledger was byte-identical to the local main-based ledger before this patch, so the target path is the same; the patch was not applied to the prohibited PR branch.

---

## 2. M1 outstanding closeout checklist

`D-M1-06` is now recorded on the current session branch. M1 closeout is still conditional because the following Owner-controlled tasks remain outstanding.

| # | Task | Current state | Required action | Authority |
|---:|---|---|---|---|
| 1 | APEX raw evidence preservation | `OUTSTANDING`. Raw APEX evidence is not accessible from the PR tree, CI artifacts, PR comments/reviews, tags/releases, repo workspace, or connected knowledge sources. | Owner chooses a durable, Owner-controlled archive location and supplies raw evidence or certifies retention/non-availability. Recommended primary archive: non-cloud-synced local folder on APEX outside `Documents` and outside any OmniRoute directory, with raw artifacts, SHA-256 checksums, and retention note. Repository content should be limited to a sanitized manifest: no personal filenames, no private `Documents` listings, no secrets/tokens/API keys. | Owner |
| 2 | Artifact SHA-256 provenance verification | `OUTSTANDING`. APEX-installed executable ProductVersion was reported as `0.1.0+47c86b6d1e8cda25378c170964d54d6e60b6be18`. The suffix maps to a 2026-11-17 PR merge-ref commit, not current PR head `857b320` or current merge ref `19e7892`. Only docs changed afterwards, so the binary may be code-identical, but that is not proven. | Compute and record SHA-256 of the APEX-installed executable and current CI artifact from run `44538313577`; record run ID, artifact name, ProductVersion, PR head, and hash comparison. Do not assert equality until verified. | Owner / authorized Windows operator |
| 3 | Formal GitHub PR review | `OUTSTANDING`. No formal review/approval exists on PR #3; `reviewDecision=""`; issue/review comments are empty. | Owner or authorized reviewer submits the formal GitHub review. The assistant must not approve the PR. | Owner / reviewer |
| 4 | PR #3 merge authorization | `OUTSTANDING`. PR #3 is open and mergeable, not merged. | Owner exercises final merge authority after review, evidence, provenance, and release/tag decisions. The assistant must not merge. | Owner |
| 5 | M1 release/tag preparation | `OUTSTANDING`. No tags or releases exist. | After merge, prepare a private/tag release such as `v0.1.0-m1` with final commit SHA, SHA-256, CI run ID, PR head, artifact provenance, and D-M1-06 reference. No tag/release was created here. | Owner |

Additional standing M1 qualifications still requiring Owner treatment: the three OS-locked skipped files in the final Documents scan remain a permanent qualification unless the Owner confirms otherwise; memory profiling/root-cause/soak work remains separate and `NOT STARTED`; the sanitized evidence-manifest publication requires explicit Owner approval; the three skipped files were not inspected and the comparison is incomplete for them.

---

## 3. Canonical specification recovery report

### 3.1 Search scope

Authorized GitHub repositories enumerated: `studioavenox-ux/aeron-core`, `avenox-website`, `brainrot-arena`, `museum-of-unfinished-days-`, `shadow-protocol`, `the-last-signal`, `veilbreak-`, and `ZOE`. All were public; `ZOE` had no refs in the mirror scan.

GitHub `aeron-core` refs inspected: five branch heads plus two unique PR merge refs, for seven unique commit SHAs:

- `refs/heads/arena/14c8988d-aeron-core` → `1a32b5b399839ca61d102ff33fbccae67a35b76d`
- `refs/heads/arena/8ed7a07b-aeron-core` and `refs/pull/1/head` → `ffcabbb2c5dc9beb675211b02412b17fa7f8c973`
- `refs/heads/arena/9c2318d0-aeron-core` → `5ea7e410f70579893be99c66d04b4e01902e5b9d`
- `refs/heads/fix/m1-tray-lifecycle` and `refs/pull/3/head` → `857b3203fea13239c397113298e8b41b1dc67f93`
- `refs/heads/main` → `24d0804b445b429909dd16068da7e00cccc603a0`
- `refs/pull/1/merge` → `90651bdab415b20a8cab3e65327868b9837be105`
- `refs/pull/3/merge` → `19e7892d0bd709972b6234b9a7af9e35e4c6b910`

Connected knowledge sources searched: Google Drive, Google Docs, Gmail, Google Calendar, and Notion.

### 3.2 Results

- **GitHub direct tree/blob inspection:** all seven unique `aeron-core` refs contain the same six canonical-doc blobs as the local main-based checkout. No alternate canonical body or verbatim historical text was found.
- **GitHub mirror content scan:** exact canonical identifiers and distinctive governance phrases matched only current `aeron-core` documentation, hygiene script, and architecture tests. The other seven owner repositories had no matches; `ZOE` had no refs.
- **GitHub code search caveat:** the code-search API returned zero even for a `D-M1-05` control query, so it was treated as unreliable for negative proof; direct mirror and tree scans were used instead.
- **PR/issue/commit searches:** no issue, PR body, or commit message supplied canonical text. PR #1 body only lists the governance files it added; it does not include canonical clauses.
- **Gmail:** zero messages for exact `ADR-AUTH` identifiers, `U-8R`/`U-8`/`U-1`, canonical-specification phrases, related attachments, and M2 component names; spam/trash included.
- **Notion:** zero results for `ADR-AUTH`, `U-8R`, `U-8`, `AERON`, `canonical specification`, and `Security Kernel`.
- **Google Calendar:** zero events for `ADR-AUTH`, `U-8R`, `AERON`, `canonical specification`, and `Security Kernel`.
- **Google Drive / Google Docs:** zero exact full-text matches for `ADR-AUTH-001`, `ADR-AUTH-002`, `ADR-AUTH-003`, `U-08R-REPLACEMENT`, and `OwnerAuthorityGuard`; zero matching Google Docs with `ADR-AUTH`; zero recent AERON/ADR/U-8/canonical/evidence-named files; zero shared drives. A broad full-text query returned 100 truncated unrelated JS/JSON source artifacts because generic terms such as `canonical` matched code bundles. An exact `U-8R` full-text query returned two unrelated source/build metadata files from an OmniRoute corpus; extracted text showed proxy/egress JS and `package-lock.json` content, not AERON canonical governance text. No secrets, tokens, API keys, personal filenames, or private `Documents` listings were exposed or recorded.
- **Conclusion:** no exact canonical specification text was recovered for any requested U/ADR document. All exact canonical texts remain `UNRESOLVED`. Confirmed principles, mandates, and governance rules in the repository placeholders are not canonical text.

### 3.3 Source/version of available placeholder records

No exact canonical specification document was recovered, so there is no recovered canonical source/version to record. The source/version below is for the available placeholder records only.

Version key `V1`: `studioavenox-ux/aeron-core` at base commit `24d0804b445b429909dd16068da7e00cccc603a0` (`2026-11-17T21:40:44+05:30`) and PR #3 head `857b3203fea13239c397113298e8b41b1dc67f93` (`2026-11-19T13:54:32Z`). The six blob SHAs were identical across all seven unique remote refs inspected. Placeholder audit date in the documents: `2026-11-17`.

| Document | Available source/version | Exact canonical text recovered? | Status |
|---|---|---|---|
| `U-1` Controlled evolution | `V1`, `docs/governance/U-01-TO-U-07.md`, blob `4598aa24346a09a177311f2ec49a6d40af60e9d8`; core principle `CONFIRMED`, canonical body `UNRESOLVED` | No | `UNRESOLVED` |
| `U-2` Intelligent adaptive modes | `V1`, same file/blob `4598aa24346a09a177311f2ec49a6d40af60e9d8`; core principle `CONFIRMED`, exact mode catalog/transition rules `UNRESOLVED` | No | `UNRESOLVED` |
| `U-3` Dynamic prioritization | `V1`, same file/blob; core principle `CONFIRMED`, exact scoring rules `UNRESOLVED` | No | `UNRESOLVED` |
| `U-4` Adaptive scheduling | `V1`, same file/blob; core principle `CONFIRMED`, exact scheduling rules `UNRESOLVED` | No | `UNRESOLVED` |
| `U-5` Adaptive notifications | `V1`, same file/blob; core principle `CONFIRMED`, exact notification thresholds `UNRESOLVED` | No | `UNRESOLVED` |
| `U-6` Human-like agent team / avatars / autonomy / communication / personalities | `V1`, same file/blob; core principle `CONFIRMED`, exact roster/personality/avatar spec `UNRESOLVED` | No | `UNRESOLVED` |
| `U-7` Complete contextual memory | `V1`, same file/blob; core principle `CONFIRMED`, exact retention/retrieval rules `UNRESOLVED` | No | `UNRESOLVED` |
| Original `U-8` | `V1`, `docs/governance/U-08-ORIGINAL.md`, blob `5f72b9c924a7ac09598893ed55a34268de3a426e`; recovery status `UNRESOLVED` | No | `UNRESOLVED` |
| Replacement `U-8R` | `V1`, `docs/governance/U-08R-REPLACEMENT.md`, blob `422a0c53e72a7b0044e48e5beb0c494c0599a0de`; recovery status `UNRESOLVED` | No | `UNRESOLVED` |
| `ADR-AUTH-001` | `V1`, `docs/governance/ADR-AUTH-001.md`, blob `aa7e8a511ff8e9ee84b10017ca8def6925a9b461`; governance mandate `CONFIRMED`, verbatim historical text `UNRESOLVED` | No | `UNRESOLVED` |
| `ADR-AUTH-002` | `V1`, `docs/governance/ADR-AUTH-002.md`, blob `f568793d402e9c9f3a77de759e1d21378a0af437`; permission/gateway separation mandate `CONFIRMED`, verbatim historical text `UNRESOLVED` | No | `UNRESOLVED` |
| `ADR-AUTH-003` | `V1`, `docs/governance/ADR-AUTH-003.md`, blob `0fc6102e32bfc5b7e77713e829fbdf900fe61dc0`; approval/audit mandate `CONFIRMED`, verbatim historical text `UNRESOLVED` | No | `UNRESOLVED` |

Original `U-8` and replacement `U-8R` are preserved strictly separate. No missing clause was invented.

---

## 4. M2 architecture blueprint

Status: `PROPOSED / NOT STARTED`. This blueprint uses only confirmed repository constraints, the existing modular architecture, and placeholder mandates. It is not canonical ADR/U text. M2 implementation must not begin until all canonical texts are supplied, M1 closeout dependencies are resolved, and the Owner issues explicit M2 `BUILD` authorization.

### 4.1 Architecture preservation and budget

- Preserve the existing unidirectional acyclic dependency graph and target frameworks: `Aeron.Contracts` (`net10.0`, zero dependencies), `Aeron.Security` (`net10.0`), `Aeron.Infrastructure` (`net10.0`), `Aeron.Core` (`net10.0`), `Aeron.Desktop` (`net10.0-windows` WPF `WinExe`), and the future `Aeron.Platform.Windows` module.
- M2 expands `Aeron.Security` only with the planned security-kernel components; it does not create `Aeron.Gateway`, `Aeron.Memory`, `Aeron.Agents`, or `Aeron.Execution`, and does not start M3 OmniRoute `CLIENT_API` integration.
- Preserve C# 14 / .NET 10 LTS, WPF native Windows desktop, deterministic builds, `TreatWarningsAsErrors=true`, and Windows 11 25H2 Build 26200 `x64` minimum.
- Preserve the ₹0 budget: free/OSS tooling and built-in Windows APIs only. No paid APIs, no paid inference fallback, no local LLM runtimes or model-weight downloads, no skill-script execution, no third-party agent framework, and no OmniRoute configuration/data/process modification.
- Runtime files stay only in current-user-ACL-protected `%LOCALAPPDATA%\Aeron` (`config`, `data`, `data\backups`, `logs`), never in the repository, cloud-synced `Documents`, or OmniRoute directories.

### 4.2 OwnerAuthorityGuard

- Proposed non-bypassable choke point for every consequential action and policy decision.
- Enforces C-10: Project Owner pause, cancel, revoke, reconfigure, and emergency-stop directives preempt AERON Core and every agent immediately.
- Enforces ADR-AUTH-001 confirmed mandate: ultimate authority is non-delegable; AERON Core and specialist agents cannot bypass Owner governance.
- Must fail closed if authority state, policy version, approval binding, or canonical-specification status is unavailable.
- Must not interpret placeholder mandates as canonical verbatim clauses; exact `ADR-AUTH-001` text remains an Owner-supplied dependency.

### 4.3 Default-deny PolicyDecisionEngine

- Proposed independent, deterministic engine in `Aeron.Security`, separate from AERON Core reasoning and agent reasoning.
- Every action request is normalized into an explicit domain, tier, actor, parameters, data classification, disclosure destination, and policy version.
- If a rule is absent, ambiguous, expired, unverifiable, or canonical text is unresolved for a policy-finalization dependency, the result is `DENY`.
- No implicit allow-list inheritance, no paid fallback, no local inference fallback, no dynamic skill script, and no OmniRoute mutation path.
- All decisions are returned as typed result envelopes with redacted reason codes and are written to the audit ledger.

### 4.4 G1–G4 and L0–L3 authority levels

The current repository constraints define the following domains and tiers. Exact canonical `ADR-AUTH-002` verbatim text remains `UNRESOLVED`; this table is a confirmed-constraint summary, not canonical text.

| Domain / tier | Scope | Policy posture |
|---|---|---|
| `G1` Approved Remote Model Inference | Routine inference over an explicitly Owner-approved, zero-cost gateway configuration | No repeated per-message approval prompt, but non-bypassable guards enforce approved provider/model/combo, verified `$0.00`, remote-only routing, pre-wire redaction, disclosure policy, and zero paid/local fallback. Adding/modifying gateways, providers, models, cost rules, or disclosure policies requires explicit Owner approval. M3 only; not implemented in M2. |
| `G2` External Data Disclosure | Local files, private documents, restricted memory, or host artifacts sent off-device | `Secret` data hard-blocked and redacted. `LocalPrivate`/restricted data requires policy clearance and explicit Owner approval specifying exact source and destination. `RoutineContext` flows only within approved `G1` disclosure policy. |
| `G3` External Communications | Email, message, chat, invite, comment, webhook, externally visible post | Agents may stage local drafts only; transmission requires explicit per-action, parameter-hashed Owner approval. |
| `G4` External Account Modifications | External writes, edits, deletes, shares, permission changes, repository mutation, settings | Explicit per-action, parameter-hashed Owner approval. Mutating OmniRoute or enabling paid/billed services is permanently forbidden. |
| `L0` Isolated AERON Internal State | Operations inside `%LOCALAPPDATA%\Aeron` | Allowed only with current-user Windows ACL protection and audit logging. |
| `L1` Bounded Safe Local Reads / Drafts | Policy-permitted non-destructive reads and local staging | Allowed with audit logging. |
| `L2` Consequential Local Actions | Owner file writes/deletes/moves, external process launch, OS-state change, persistent schedule change, controlled-evolution proposal | Mandatory explicit Owner approval before execution; fail closed on denial, expiration, cancellation, or parameter mismatch. |
| `L3` Forbidden Local Actions | v1 skill-script execution, local LLM execution/model downloads, external `.env`/secret reads, disabling redaction/audit, bypassing Owner authority | Hard-denied in code. |

### 4.5 OwnerApprovalService

- Proposed explicit approval lifecycle for `G2` restricted disclosure, `G3`, `G4`, and `L2`.
- Before any consequential side effect, it creates a redacted, human-readable approval request, binds it to a stable action-parameter hash, captures policy version and expiry, and waits for an explicit Owner decision.
- Fail closed on denial, expiration, cancellation, parameter mismatch, policy-version change, missing approval, or Owner preemption.
- No self-approval, no implied consent, no reuse of approvals across changed parameters, and no external transmission from a draft alone.
- Exact `ADR-AUTH-003` clauses remain `UNRESOLVED`; the above is a proposed implementation posture derived from confirmed mandates.

### 4.6 Parameter-hashed approval requests

- Canonicalize action parameters into a stable, versioned, redacted representation; exclude raw secrets entirely.
- Compute a SHA-256 parameter hash over that canonical representation and display/store the hash with the approval request.
- Recompute the hash immediately before execution; any mismatch fails closed without side effects.
- Store approval decisions, expiry, cancellation/preemption state, and policy version in `AuditLedger`; never store raw credentials or unredacted secret-bearing payloads in logs, prompts, memory, or approval records.
- The hash is an integrity binding, not a signature. No Owner signature is invented or required by this preparation report.

### 4.7 Hash-chained AuditLedger

- Proposed append-only, tamper-evident ledger in the expanded `Aeron.Security` / existing SQLite WAL infrastructure.
- Each entry contains sequence, UTC timestamp, actor, request classification, policy version, decision, approval hash, previous-entry hash, entry hash, redacted outcome, and error code.
- Startup and CI verification recompute the chain; any missing, reordered, altered, or unredacted-secret entry fails closed.
- Store under `%LOCALAPPDATA%\Aeron\data` with current-user ACLs and existing transactional migration/checksum discipline.
- All lifecycle, policy, approval, denial, expiration, cancellation, preemption, and action events pass through secret-redacting logging before persistence.

### 4.8 DPAPI / Windows Credential Manager vault

- Proposed future `Aeron.Platform.Windows` adapter, matching the existing BLUEPRINT module plan.
- Use built-in Windows DPAPI current-user protection and Windows Credential Manager for secret-bearing configuration, with no inline credentials in JSON config.
- Vault reads and writes remain local-only; no cloud sync, no OmniRoute secret read, no raw secret in logs/prompts/memory/audit, and no external disclosure without `G2` policy clearance and Owner approval.
- Use ₹0 built-in OS APIs and free/OSS libraries only. No paid secret manager, no local model, no skill script, and no OmniRoute modification.
- `ConfigPolicyValidator` and `RedactionFilter` remain fail-closed gates for forbidden credential properties and secret-like values.

### 4.9 Secret redaction and privacy protection

- Reuse and extend existing `Aeron.Security.RedactionFilter`, `RedactingLogger`, and `ConfigPolicyValidator`; do not replace them with agent-side heuristics.
- Unconditionally redact logs, structured properties, nested exceptions, SQLite journal messages, audit entries, approval UI, and test diagnostics.
- Secrets are hard-blocked from `G2`; `LocalPrivate`/restricted data requires exact source/destination approval; `RoutineContext` only flows inside approved `G1` policy.
- No personal filenames or private `Documents` listings in repository documentation, evidence manifests, logs, or CI output.
- Preserve C-12: zero secret exposure in configs, logs, prompts, or memory; runtime storage only in user-ACL-protected `%LOCALAPPDATA%\Aeron`.

### 4.10 Windows security testing and CI

- Extend the existing Windows GitHub Actions CI and architecture tests only after explicit M2 authorization.
- Proposed test groups: default-deny matrix; every `G1`–`G4`/`L0`–`L3` boundary; approval denial/expiry/cancellation/parameter mismatch; Owner preemption; audit-chain tamper detection; DPAPI/Credential Manager round-trip with synthetic secrets; redaction across logs/errors/structured properties/nested exceptions; `Documents`/OmniRoute path rejection; zero inline credentials; zero banned local-model weights or skill scripts; no premature M3/M4 modules; zero warnings under `TreatWarningsAsErrors=true`.
- Windows-specific tests require a Windows runner or APEX. No tests, builds, or Windows security checks were run in this Linux sandbox.
- CI must not require paid APIs, local model inference, skill-script execution, or OmniRoute changes. Test artifacts may contain synthetic secret strings only after redaction assertions prove they are not emitted.

---

## 5. M2 dependency matrix

Legend: `RECOVERED` means an exact canonical document with source/version is available. `PARTIAL` means only confirmed principles/mandates or placeholder records exist. `UNRESOLVED` means the exact canonical text is unavailable and must be supplied by the Owner. All rows below are gates; no M2 implementation is authorized.

| Dependency | Exact canonical text recovered? | Current source/version | Status | Owner must supply | M2 gate |
|---|---|---|---|---|---|
| `U-1` | No | `V1`, `U-01-TO-U-07.md`, blob `4598aa24…` | `UNRESOLVED` | Exact verbatim `U-1` canonical body plus source/version/date | Blocks controlled-evolution policy finalization |
| `U-2` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-2` canonical body plus mode catalog/transition rules and source/version | Blocks mode-related policy parameters |
| `U-3` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-3` canonical body plus scoring rules and source/version | Blocks prioritization policy parameters |
| `U-4` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-4` canonical body plus scheduling rules and source/version | Blocks schedule/`L2` policy details |
| `U-5` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-5` canonical body plus notification thresholds and source/version | Blocks notification/approval escalation details |
| `U-6` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-6` canonical body plus roster/personality/avatar spec and source/version | Blocks agent-boundary policy details |
| `U-7` | No | `V1`, same blob | `UNRESOLVED` | Exact verbatim `U-7` canonical body plus retention/retrieval/inspection/edit/delete rules and source/version | Blocks privacy/audit retention policy |
| Original `U-8` | No | `V1`, `U-08-ORIGINAL.md`, blob `5f72b9c9…` | `UNRESOLVED` | Exact verbatim original `U-8`, kept separate from `U-8R`, with source/version | Blocks M2 policy finalization |
| Replacement `U-8R` | No | `V1`, `U-08R-REPLACEMENT.md`, blob `422a0c53…` | `UNRESOLVED` | Exact verbatim replacement `U-8R`, kept separate from original `U-8`, with source/version | Blocks M2 policy finalization |
| `ADR-AUTH-001` | No | `V1`, `ADR-AUTH-001.md`, blob `aa7e8a51…` | `UNRESOLVED` verbatim clauses; mandate `CONFIRMED` | Exact historical clauses/source/version | Blocks OwnerAuthorityGuard finalization |
| `ADR-AUTH-002` | No | `V1`, `ADR-AUTH-002.md`, blob `f568793d…` | `UNRESOLVED` verbatim clauses; separation mandate `CONFIRMED` | Exact historical clauses/source/version | Blocks `G1`–`G4`/`L0`–`L3` policy finalization |
| `ADR-AUTH-003` | No | `V1`, `ADR-AUTH-003.md`, blob `0fc6102e…` | `UNRESOLVED` verbatim clauses; approval/audit mandate `CONFIRMED` | Exact historical clauses/source/version | Blocks approval/audit finalization |
| M1 closeout: raw evidence, SHA-256 provenance, formal review, merge, release/tag | N/A | PR #3 `857b320`, CI `44538313577` | `OUTSTANDING` | Owner decisions/evidence/review/merge/tag | Prerequisite before M2 `BUILD` |
| `D-M1-06` application to authoritative branch | N/A | Patch prepared on session branch | `PREPARED` | Owner decides where/when to apply | Governance consistency gate |
| Exact M2 implementation authorization | N/A | Current user instruction is preparation only | `NOT GIVEN` | Explicit Owner `BUILD` authorization for M2 | Hard gate |
| Windows CI / APEX security-test authorization | N/A | Linux sandbox; no Windows runtime here | `NOT STARTED` | Owner-approved Windows test environment and CI scope | Hard gate before test implementation |
| M3 OmniRoute `CLIENT_API` allowlist | N/A | BLUEPRINT Pre-M3 item | `OUT OF M2 SCOPE` | Approved ₹0 providers/models/combos only before M3; no OmniRoute modification | Boundary, not M2 dependency |

M2 readiness: `NOT READY FOR IMPLEMENTATION`. Preparation is complete; exact canonical text recovery is not complete.

---

## 6. Proposed M2 milestone stages

All stages are `PROPOSED / NOT STARTED`. No stage below has been implemented, committed, pushed, built, or tested.

| Stage | Name | Scope | Gate / exit criteria |
|---|---|---|---|
| M2.0 | Governance recovery & authorization gate | Owner supplies all 12 exact canonical texts with source/version; resolves M1 closeout checklist; applies/accepts `D-M1-06` on authoritative branch; formal PR review, merge, and release/tag decisions; explicit M2 `BUILD` authorization | All canonical texts `RECOVERED`; M1 closeout accepted; Owner authorization recorded. No code changes before this gate. |
| M2.1 | Contracts & policy model | Typed request, decision, data classification, authority-level, approval, audit, and redaction contracts in existing dependency order | Architecture tests confirm no new external dependencies, no premature modules, no secrets in contracts. |
| M2.2 | Default-deny PolicyDecisionEngine + OwnerAuthorityGuard | Deterministic `G1`–`G4`/`L0`–`L3` matrix, Owner preemption, fail-closed policy/version checks | Unit tests cover allow/deny/hard-deny, ambiguity, missing canonical dependency, and preemption. |
| M2.3 | OwnerApprovalService + parameter hashing | Explicit per-action approval lifecycle for `G2`/`G3`/`G4`/`L2`; SHA-256 parameter binding; expiry/cancel/mismatch handling | Tests prove no side effect on denial, expiration, cancellation, mismatch, or policy-version change. |
| M2.4 | Hash-chained AuditLedger | Append-only chain, previous/current hash, policy/approval/outcome records, redaction before persistence, SQLite WAL migration | Startup/CI chain verification and tamper-detection tests pass; no secret leakage. |
| M2.5 | DPAPI / Windows Credential Manager vault | Future `Aeron.Platform.Windows` adapter; current-user DPAPI/Credential Manager; no inline credentials; local-only storage | Windows tests prove round-trip, ACL/path isolation, `Documents`/OmniRoute rejection, and redaction. |
| M2.6 | Windows security CI & architecture enforcement | Extend Windows CI, architecture guard, hygiene scan, synthetic-secret tests, zero-warning build | Windows CI green; 0 warnings/errors; no banned weights/frameworks/skill scripts/paid services/OmniRoute changes. |
| M2.7 | M2 acceptance evidence & Owner review | APEX/Windows interactive verification, evidence manifest, SHA-256/run/artifact provenance, formal review, M2 tag/release preparation | Owner acceptance; M2 frozen; M3 remains unstarted and requires approved ₹0 gateway allowlist. |

---

## 7. Remaining Owner decisions

1. Decide how/whether to apply the `D-M1-06` patch to the authoritative PR branch or post-merge `main`; no signature is needed.
2. Choose/authorize the APEX raw-evidence archive and supply raw evidence, or certify retention/non-availability.
3. Approve publication of a sanitized evidence manifest; explicitly confirm that no personal filenames, private `Documents` listings, secrets, tokens, or API keys are included.
4. Confirm the three OS-locked skipped files remain a permanent qualification to AT-M1-APEX-05, and that those files were not inspected.
5. Resolve artifact provenance by SHA-256 comparison and record run ID, ProductVersion, PR head, and artifact hash.
6. Approve the memory profiling/root-cause/optimization follow-up scope and priority, without starting that work now.
7. Submit or authorize the formal GitHub PR #3 review; assistant must not approve.
8. Exercise final PR #3 merge authority; assistant must not merge. Decide the disposition of redundant PR #1.
9. Approve M1 release/tag naming, privacy level, artifact retention, and provenance contents; no tag/release exists yet.
10. Supply exact verbatim canonical texts with source/version for `U-1`–`U-7`, original `U-8`, replacement `U-8R`, and `ADR-AUTH-001`–`003`; keep original `U-8` and replacement `U-8R` separate.
11. Approve/reject the proposed M2 blueprint, stage order, vault mechanism, approval-binding design, audit-retention policy, Windows CI scope, and privacy/redaction rules.
12. Issue explicit M2 `BUILD` authorization before any implementation. Confirm that M2 remains ₹0, remote-inference-only, with no local model inference, no paid API fallback, no skill-script execution, and no OmniRoute modification.
13. Keep M3 OmniRoute `CLIENT_API` and the approved ₹0 gateway allowlist outside M2; approve them separately before M3.

---

**Final status:** M2 PREPARATION — NOT IMPLEMENTATION. Wait for Owner authorization before starting M2 development.
