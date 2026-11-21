# AERON M2 — SECURITY KERNEL SPECIFICATION PACK v1.1.1

> Status: `AERON M2 SPECIFICATION v1.1.1 — PROPOSED / OWNER REVIEW REQUIRED`<br>
> Owner: Aditya  
> Lead Builder: Arena Agent  
> Technical Architect: GPT-6  
> Scope: Specification development only. **Not implementation authorization.**  
> Version: `v1.1.1`<br>
> Session date: see Owner v1.1 request; date not independently established  
> Implementation status: `BLOCKED` until Owner review and explicit `M2 BUILD` authorization.

## 0. Purpose and boundaries

This pack recovers, reconstructs, and organizes AERON's missing security specifications using decisions already made by the Project Owner. It is a reviewable specification pack, not a build authorization. No C# source, application test, PR, tag/release, OmniRoute file, local model/runtime, or paid API was changed or introduced.

All previously recorded decisions are preserved. No Owner is asked to repeat known decisions.

### Owner-review state legend

| State | Meaning |
|---|---|
| `RECOVERED` | Exact historical content was found with source and version information. |
| `CONFIRMED` | Explicit Owner decision already recorded; not newly drafted here. |
| `PROPOSED` | Newly drafted content awaiting Owner review. |
| `UNRESOLVED` | Insufficient evidence or missing Owner decision; no meaning invented. |
| `SUPERSEDED` | Historical summary corrected by later Owner-provided evidence; retained for traceability only. |

### Known decisions preserved — not asked again

#### U-8R and authority restorations (`CONFIRMED`)

Fifteen individually numbered decisions and labels are preserved in `U-08R.md` and `OWNER-REVIEW-U8R-RESTORATION.md`. Their selections and reported labels are confirmed in the Owner's v1.1 request. The v1.0 ten-item summary is `SUPERSEDED` as the complete U-8R record; no mappings are inferred. Original U-8 remains separate.

ADR-AUTH-001 `C — Core-Led Governance`, ADR-AUTH-002 `C — Restricted Dual Authorization`, and ADR-AUTH-003 `B — Complete Owner Authority` are confirmed by the Owner's v1.1 request. The Owner reports GPT-6 retrieved and inspected all four historical texts in the ChatGPT Library and compared relevant records. Arena could not independently open or hash the files; byte integrity and original versions remain unresolved. The provenance register distinguishes the two forms of access. Original questions and alternatives remain unresolved except the reported U-8R.6 list; U-8R.5 custom policy remains unresolved.

#### Separate U-7 memory records

`U-07.md` distinguishes the Complete Personal Knowledge System direction (no established letter), the sensitive/personal-information consent preference, `D — Hybrid Cognitive Memory`, permanent conversation archival, and the separate `D — Complete Contextual Memory` identity. The latter is not the complete original U-7 decision and does not replace the other records. Archival, deletion, privacy and retention implementation require review.

#### Existing architectural constraints (`CONFIRMED`)

- Native Windows desktop application.
- .NET 10 LTS and WPF.
- ₹0 budget.
- Remote-only model inference.
- No local model weights or inference runtimes.
- No paid API fallback.
- OmniRoute installation must remain untouched.
- Owner retains ultimate authority.
- Default-deny security.
- Permission-controlled tool execution.
- Persistent contextual memory.
- Multi-agent architecture.
- Secure logging, audit, and backups.

Also preserved: `D-M1-01`–`D-M1-06`, including `D-M1-06`'s M1-closure-only approximately 280 MB Working Set deviation, preserved ≤120 MB original target, no claim that the target was achieved, memory work future, and no extension to M2 or later.

### File index

| File | Purpose |
|---|---|
| `U-01.md`–`U-07.md` | Per-decision U-1…U-7 reconstruction. |
| `U-08.md` | Original U-8, kept separate from U-8R. |
| `U-08R.md` | Replacement U-8R: 15 confirmed decisions and explicitly unresolved details. |
| `ADR-AUTH-001.md`–`ADR-AUTH-003.md` | ADR-AUTH reconstructions. |
| `SECURITY-THREAT-MODEL.md` | Defensive M2 threat model. |
| `SECURITY-ARCHITECTURE.md` | Detailed proposed Security Kernel component specifications. |
| `M2-IMPLEMENTATION-PLAN.md` | Proposed stages; not implementation authorization. |
| `OWNER-DECISION-REQUIRED.md` | Consolidated list of only genuinely missing Owner inputs. |
| `DECISION-RECOVERY-MATRIX.md` | State table for every reconstructed decision. |
| `SOURCE-AND-PROVENANCE-REGISTER.md` | Sources, versions, blob hashes, and recovered exact wording. |
| `HISTORICAL-VS-RECONSTRUCTED-REGISTER.md` | Historical content vs newly drafted proposals. |
| `CONTRADICTION-REPORT.md` | Apparent tensions and boundaries. |
| `M2-ACCEPTANCE-CRITERIA.md` | Spec-pack acceptance and future M2 BUILD acceptance. |
| `M2-DEPENDENCY-GRAPH.md` | Component and existing project dependency graph. |
| `OWNER-REVIEW-U8R-RESTORATION.md` | Fifteen-decision review table with evidence boundaries. |
| `HISTORICAL-CORRECTION-REPORT.md` | v1.0 ten-item summary correction. |

### Global status

Every canonical U/ADR document is either `PROPOSED RECONSTRUCTION — REQUIRES OWNER REVIEW` for newly drafted normative wording, `CONFIRMED` for known selections, `RECOVERED` for directly inspected contextual wording or historical excerpts from the Owner-reported GPT-6 source-text review, or `UNRESOLVED` where canonical text/option definitions are missing. No proposed document is marked confirmed.

Implementation remains blocked until the Owner approves the specification lock, the existing authoritative-source gate is satisfied or explicitly reconciled, and the Owner separately authorizes `M2 BUILD`.

**Final status:** `AERON M2 SPECIFICATION v1.1.1 — PROPOSED / OWNER REVIEW REQUIRED`
