# Source and Provenance Register

> Purpose: preserve original source references, version information, and exact recovered wording.  
> No secrets, tokens, API keys, personal filenames, or private `Documents` listings are recorded.

## v1.1.1 historical source inspection and provenance correction

**A. GPT-6 / ChatGPT-side text inspection:** The Owner's v1.1.1 clarification reports that GPT-6 successfully retrieved and inspected the text of all four historical files in the ChatGPT Library during review, and compared relevant U-8R, authority and U-7 records against the v1.1 restoration. This is a reported review by GPT-6, not an inspection performed by Arena. The Owner's v1.1 and v1.1.1 requests provide the selections and excerpts recorded in this pack. The clarification does not provide file-specific passages for every individual decision; no such mapping is invented.

**B. Arena workspace access:** H1–H4 were not mounted or present in Arena's working directory; the earlier exact-name Drive search also returned no matches. Arena did not independently open or hash the original files. The previous statement that the historical texts had not been inspected at all is corrected: GPT-6's reported text review and Arena's independent access are different evidence fields.

**C. Remaining verification:** Original file byte-level integrity and file hashes have not been verified or captured by Arena. Exact original file versions cannot be independently established from the workspace. Missing original question wording and alternatives remain `UNRESOLVED`; the review does not authorize inventing them. Source availability for independent verification does not negate the reported GPT-6 inspection.

| ID | Historical source named in Owner clarification | Owner-described content | Date / version | GPT-6 text review (Owner-reported) | Arena access and integrity |
|---|---|---|---|---|---|
| H1 | `Pasted text(20261116-031230).txt` — AERON — COMPLETE PROJECT HISTORY & MASTER DEVELOPMENT HANDOVER | Project history; U-1–U-8; U-8R; U-6/U-7; authority. | Prepared 16 November 2026; version unknown. | Retrieved and inspected in ChatGPT Library. | Not mounted/opened/hashed by Arena. |
| H2 | `Pasted text(20261116-041642).txt` | U-8R selection register and authority ADRs. | Filename dated 2026-11-16; version unknown. | Retrieved and inspected in ChatGPT Library. | Not mounted/opened/hashed by Arena. |
| H3 | `Pasted text(20261116-042926).txt` | Additional U-8R, authority, U-6/U-7 reference. | Filename dated 2026-11-16; version unknown. | Retrieved and inspected in ChatGPT Library. | Not mounted/opened/hashed by Arena. |
| H4 | `Pasted text(20261116-043907).txt` | Additional U-8R, authority, U-6/U-7 reference. | Filename dated 2026-11-16; version unknown. | Retrieved and inspected in ChatGPT Library. | Not mounted/opened/hashed by Arena. |

**U-7 provenance:** `U-7-A`–`U-7-E` are separate pack-local traceability labels, not asserted original question numbers. Evidence for each is the Owner's v1.1.1 clarification of GPT-6's review of H1–H4 as a group. The clarification does not identify the particular file for each U-7 item. The U-7-E question is reproduced as supplied by the Owner; Arena cannot independently verify its original bytes. Other original U-7 question wording and all full alternative sets were not supplied in this clarification.

`RECOVERED` distinguishes reported historical content from `PROPOSED` engineering; `CONFIRMED` marks Owner-established selections. Original byte integrity, file hashes, exact versions and unavailable wording remain `UNRESOLVED`. Earlier connected-source searches below are historical observations, not statements about ChatGPT Library contents.

## 1. Owner request

| Field | Value |
|---|---|
| Source | `AERON SEASON 2 — M2 GOVERNANCE SPECIFICATION RECONSTRUCTION`, current conversation message |
| Owner | Aditya |
| Lead Builder | Arena Agent |
| Technical Architect | GPT-6 |
| Session date | 2026-11-18 UTC |
| Version | conversation message `v1`; no external file ID or signature supplied |
| Recovered exact decisions | The v1.0 ten-item summary, now `SUPERSEDED` as the complete U-8R record; see correction report. |

## 2. Local repository source versions

| Path | Git blob SHA | SHA-256 | Version / notes |
|---|---|---|---|
| `docs/governance/CONSTRAINTS.md` | `57f9cb264df91f946d10c6b59f1886cc6828ac42` | `4e55057ea0afa91df966a47f39714a778a275be5eb562d4fdbaa2d8934ac0b37` | Base `24d0804…`; PR head `857b320…` identical for governance docs. |
| `docs/governance/DECISION_LEDGER.md` | `be7efb3a053842b92f502a44e438c74740f16115` | `f2518c67199cadc9975a444a82a5ce8a0aa1765f22caeb173bc38963cb68ae64` | Locally modified in prior task to record D-M1-06; uncommitted on authorized branch. |
| `docs/governance/U-01-TO-U-07.md` | `4598aa24346a09a177311f2ec49a6d40af60e9d8` | `eeb6ca22b57ff834deb231297e5d8a123de5ae926b0c3bedd2dd14154837565b` | Placeholder; canonical bodies `UNRESOLVED`. |
| `docs/governance/U-08-ORIGINAL.md` | `5f72b9c924a7ac09598893ed55a34268de3a426e` | `5097620a6ea4be13997fb1b5e8c5f8a3757134d2054d6d838350bbbadae26a1f` | Original U-8 placeholder. |
| `docs/governance/U-08R-REPLACEMENT.md` | `422a0c53e72a7b0044e48e5beb0c494c0599a0de` | `6441777ef42f80820b20a512fbbdad9fcfd22a1a76ab95f0df6f6ce3f5ee92ff` | Replacement U-8R placeholder. |
| `docs/governance/ADR-AUTH-001.md` | `aa7e8a511ff8e9ee84b10017ca8def6925a9b461` | `4d9a1391d87f0c535d1cf7459a46aad46782aa33e9e0ef78a0b31d831bb1a983` | Confirmed mandate / unresolved verbatim text. |
| `docs/governance/ADR-AUTH-002.md` | `f568793d402e9c9f3a77de759e1d21378a0af437` | `bc9402bb648764373204dac0a708ab58041d15f47b4a1f780fef3016e9426200` | Confirmed rules / unresolved verbatim text. |
| `docs/governance/ADR-AUTH-003.md` | `0fc6102e32bfc5b7e77713e829fbdf900fe61dc0` | `8434cca72cb3dab7bd45a87b11a175fed431f0e74a285036814e93b0063cbcca` | Confirmed rules / unresolved verbatim text. |
| `docs/architecture/BLUEPRINT.md` | `ae37f50170faa6d44030363e702e0a6f0c1e082f` | `2b65557b486afb0a838763abbcbb77610723f04dbfe7ba03286a6d7b6fdfd6ca` | M2 Security Kernel roadmap; M3 OmniRoute dependency. |
| `docs/milestones/M1-FOUNDATION.md` | `d38b26f9a36d7db7382c5bd05803d98aa1476bb5` | `0b914e03cb60c6923eebb77e78dc358436f8642a0f28e6c6818b9c6aaf64a1ed` | Gate separation; no M2 approval/vault engine. |
| `docs/milestones/M2-PREPARATION.md` | `8bcb52e200051ca851a88abff40e9aae7e7c4807` | `e2b84ac8db9fd73fad94d410ccb543621c2808d0c3305bc8b4b44171506f087d` | Prior preparation report; preserved, not modified in this task. |
| `README.md` | `cb854786d10a0e471343099b7add174e121527ce` | `1b0c278df15dbbbc4b4bd7976e7ba4a786cde07e01bf39de79cf5b3a0b3d4637` | M1 architecture/budget constraints. |

## 3. GitHub remote refs

Direct mirror/API inspection in the prior and current tasks found seven unique `aeron-core` refs. All six canonical-doc blobs were identical across:

- `refs/heads/arena/14c8988d-aeron-core` → `1a32b5b399839ca61d102ff33fbccae67a35b76d`
- `refs/heads/arena/8ed7a07b-aeron-core` / `refs/pull/1/head` → `ffcabbb2c5dc9beb675211b02412b17fa7f8c973`
- `refs/heads/arena/9c2318d0-aeron-core` → `5ea7e410f70579893be99c66d04b4e01902e5b9d`
- `refs/heads/fix/m1-tray-lifecycle` / `refs/pull/3/head` → `857b3203fea13239c397113298e8b41b1dc67f93`
- `refs/heads/main` → `24d0804b445b429909dd16068da7e00cccc603a0`
- `refs/pull/1/merge` → `90651bdab415b20a8cab3e65327868b9837be105`
- `refs/pull/3/merge` → `19e7892d0bd709972b6234b9a7af9e35e4c6b910`

Accessible owner repositories scanned: `aeron-core`, `avenox-website`, `brainrot-arena`, `museum-of-unfinished-days-`, `shadow-protocol`, `the-last-signal`, `veilbreak-`, `ZOE`. Exact U/ADR/U-8R option-source phrases matched only existing `aeron-core` docs/tests/scripts; other repositories had no relevant matches. `ZOE` had no refs in the mirror scan.

## 4. Connected knowledge sources

| Source | Search result | Recovered source/version |
|---|---|---|
| Google Drive / Google Docs | Exact `ADR-AUTH-001/002/003`, `OwnerAuthorityGuard`, `U-08R-REPLACEMENT`, and `U-8R` document-type full-text searches returned no canonical AERON specs. Broad full-text queries returned unrelated JS/JSON/OmniRoute source artifacts. | `AVENOX Ecosystem Master Blueprint (Working Copy)` Google Doc ID `1HYoxZsQoMISa8ZrxQUWGMh3FwPdnuJBdSWchghw1gow`, version `8`, created `2026-11-18T17:19:51.996Z`, modified `2026-11-18T17:20:18.661Z`; document version `1.0 | 18 November 2026`. |
| Google Drive DOCX | Same blueprint in Word form. | `AVENOX_Ecosystem_Master_Blueprint_v1.0.docx` ID `1AbTEXsWCaD3N23Se2bYswcvnUNfIYrrG`, version `4`, created/modified `2026-11-18T17:18:34.410Z`; document version `1.0 | 18 November 2026`; size `41684` bytes; text extraction complete. |
| Gmail | Zero messages for U-8R / option / canonical / attachment queries; spam/trash included. | No source/version to record. |
| Notion | Zero relevant results for `U-8R`, `Authority policy`, `Backup scope`, `Backup storage`, `Controlled Evolution`, `ADR-AUTH-001`; generic `Option C` returned default onboarding pages only. | No source/version to record. |
| Google Calendar | Zero events for `U-8R`, `Authority policy`, `Backup storage`, `AERON Season 2`. | No source/version to record. |
| GitHub issues/PRs/comments/reviews | Zero exact matches for `Authority policy`, `Backup scope`, `Backup storage Google Drive`, `U-8R`, and canonical U/ADR phrases. PR #1 body only lists governance files added. | No source/version to record. |

## 5. Exact recovered AVENOX/AOS wording (`RECOVERED` context only)

From the Google Docs working copy and matching DOCX, document version `1.0 | 18 November 2026`:

> Founder: Aditya Kumar Nayak

> Status: Private Development Phase

> Document Version: 1.0 | 18 November 2026

> AOS is the proposed operational intelligence layer for coordinating AI-assisted work across AVENOX. It is an agreed architecture and is not yet fully implemented.

> Founder (You) — Founder and final authority

> OpenHuman — Personal AI assistant; proposed AI Co-Founder / COO role

> Hermes — Marketing Director

> Arena Agent — Main Builder

> GPT-6 — Strategy and Technology Advisor

> Gemini — Shared AI resource

> Operating principle: each AI system has a distinct responsibility and works within defined permissions. The Founder retains final authority. External actions, spending, publishing, account changes, legal commitments and other consequential decisions require explicit human approval.

> Keep AI roles distinct; avoid duplicate authority and uncontrolled agent-to-agent actions.

> Require human approval for external commitments, spending, publishing, account access and legal matters.

Boundary: this is broader AVENOX ecosystem/AOS context, not AERON historical U-8R option definitions and not ADR-AUTH canonical clauses. The native Google Doc contains no `U-8R`, `Authority policy`, `Backup scope`, `Option C`, `Option D`, or `Option E` matches.

## 6. Unrecovered material

- Exact canonical wording for U-1…U-7, original U-8, replacement U-8R, and ADR-AUTH-001…003: `UNRESOLVED`.
- Full original U-8R questions and alternative descriptions, except the Owner-reported U-8R.6 list: `UNRESOLVED`. Owner-reported selected labels and specified descriptions are recorded in `U-08R.md`.
- `Options.png` and OmniRoute `decisions`/`options` folder artifacts were not treated as AERON canonical evidence; filename/folder name alone is not a recovered specification. No private filenames or `Documents` listings are reproduced.
