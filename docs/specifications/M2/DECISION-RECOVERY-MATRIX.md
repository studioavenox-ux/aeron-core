# Decision Recovery Matrix

> Purpose: assign exactly one review state to every reconstructed decision.  
> States: `RECOVERED`, `CONFIRMED`, `PROPOSED`, `UNRESOLVED`, `SUPERSEDED`. GPT-6 reviewed H1–H4 text in ChatGPT Library per Owner clarification; Arena did not independently inspect or hash those files.

| ID | Decision / content | State | Source/version | Boundary |
|---|---|---|---|---|
| `U-8R.1` | Autonomy: `C` — Controlled Full Autonomy | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.2` | Authority: `D` — Shared Authority | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.3` | Authentication: `C` — Multi-Factor Authentication | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.4` | Encryption: `D` — Comprehensive Encryption | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.5` | Privacy: `E` — Custom Security Architecture | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.6` | Emergency protection: `D` — Comprehensive Emergency Protection | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.7` | Audit: `D` — Comprehensive Audit System | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.8` | Access: `D` — Controlled Intelligent Access | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.9` | Device security: `D` — Comprehensive Device Security | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.10` | Recovery: `E` — Custom Recovery Architecture | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.11` | Application backup: `C` — Complete Application Backup | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.12` | Isolation: `C` — Component Isolation and User Alert | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.13` | Permission revocation: `D` — Comprehensive Permission Revocation | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.14` | Notifications: `D` — Comprehensive Security Notifications | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U-8R.15` | Governance: `C` — Shared Governance | `CONFIRMED` | Owner v1.1 request Part 2; H1/H2 cited; GPT-6 review reported; not inspected by Arena | Exact original question/alternatives unresolved except reported U-8R.6 alternatives. |
| `U8R-DETAILS` | U-8R.4, .6, .9, .11, .15 reported descriptions | `RECOVERED` | Owner v1.1 request Part 2; originals not inspected by Arena | GPT-6 source-text comparison reported; Arena has not independently verified original bytes. |
| `U8R-CUSTOM` | U-8R.5 custom policy follow-up | `UNRESOLVED` | Owner v1.1 request Part 2 | Not invented. |
| `U8R-OLD` | v1.0 ten-item summary as complete decision set | `SUPERSEDED` | v1.0 pack; Owner v1.1 correction | No inferred mappings. |
| `ADR-HIST-001` | C — Core-Led Governance | `CONFIRMED` | Owner v1.1 request Part 4; H2 inspected by GPT-6 per Owner; not by Arena | Routine governance led by Core. |
| `ADR-HIST-002` | C — Restricted Dual Authorization | `CONFIRMED` | Owner v1.1 request Part 4; H2 inspected by GPT-6 per Owner; not by Arena | Core proposes amendments; Owner approval before effect. |
| `ADR-HIST-003` | B — Complete Owner Authority | `CONFIRMED` | Owner v1.1 request Part 4; H2 inspected by GPT-6 per Owner; not by Arena | Ultimate authority over strategy, identity, priorities, consequential decisions and amendments. |
| `BACKUP-SAFEGUARDS` | Pre-upload encryption, separate key management, integrity verification, controlled restoration | `PROPOSED` | Owner v1.1 request Part 3 | Not separate historical decisions. |
| `H1-H4-REVIEW` | GPT-6 reviewed the four historical source texts in the ChatGPT Library | `RECOVERED` | Owner v1.1.1 clarification | Reported review, not Arena inspection. |
| `H1-H4-INTEGRITY` | Original bytes, hashes and exact versions independently verified by Arena | `UNRESOLVED` | Arena workspace and prior exact-name Drive search | Original files not opened or hashed by Arena. |
| `REC-U1` | U-1 canonical body | `UNRESOLVED` | `U-01-TO-U-07.md`, blob `4598aa24…` | Principle confirmed; reconstruction `PROPOSED`. |
| `REC-U2` | U-2 canonical body / mode catalog | `UNRESOLVED` | same blob | Principle confirmed; reconstruction `PROPOSED`. |
| `REC-U3` | U-3 canonical body / scoring rules | `UNRESOLVED` | same blob | Principle confirmed; reconstruction `PROPOSED`. |
| `REC-U4` | U-4 canonical body / scheduling rules | `UNRESOLVED` | same blob | Principle confirmed; reconstruction `PROPOSED`. |
| `REC-U5` | U-5 canonical body / notification thresholds | `UNRESOLVED` | same blob | Principle confirmed; reconstruction `PROPOSED`. |
| `REC-U6` | U-6 canonical body / roster, personality, avatar spec | `UNRESOLVED` | same blob | Principle confirmed; reconstruction `PROPOSED`. |
| `U-7-A` | Complete Personal Knowledge System direction | `RECOVERED` | Owner v1.1.1 clarification of GPT-6 review, H1–H4 collectively | Letter, original question and alternatives not supplied; search/index design unresolved. |
| `U-7-B` | Automatically remember useful information; ask before storing sensitive or personal information | `CONFIRMED` | Same clarification/review | Letter, original question and alternatives not supplied; consent/classification unresolved. |
| `U-7-C` | `D — Hybrid Cognitive Memory` | `CONFIRMED` | Same clarification/review | Semantic, episodic, knowledge graph, long-term and contextual retrieval; original question/alternatives and implementation unresolved. |
| `U-7-D` | Permanent archive of all conversations | `CONFIRMED` | Same clarification/review | Letter, original question and alternatives not supplied; technical retention/deletion/backup/encryption unresolved. |
| `U-7-E` | `D — Complete Contextual Memory` identity | `CONFIRMED` | Same clarification/review | Question supplied; full alternatives unavailable; does not replace A–D or establish complete original U-7. |
| `REC-U7` | Complete original U-7 body / technical retention and retrieval | `UNRESOLVED` | Repository placeholder; Owner clarification | Separate recovered records do not establish the full original U-7. |
| `REC-U8` | Original U-8 canonical text | `UNRESOLVED` | `U-08-ORIGINAL.md`, blob `5f72b9c9…` | Kept separate from U-8R; no clauses invented. |
| `REC-U8R` | Replacement U-8R canonical text | `UNRESOLVED` | `U-08R-REPLACEMENT.md`, blob `422a0c53…` | Fifteen selections confirmed by Owner; GPT-6 reviewed historical text per Owner; full original text and byte integrity remain unresolved to Arena. |
| `REC-ADR1` | ADR-AUTH-001 verbatim clauses | `UNRESOLVED` | `ADR-AUTH-001.md`, blob `aa7e8a51…` | Mandate `CONFIRMED`; reconstruction `PROPOSED`. |
| `REC-ADR2` | ADR-AUTH-002 verbatim clauses | `UNRESOLVED` | `ADR-AUTH-002.md`, blob `f568793d…` | Rules `CONFIRMED`; reconstruction `PROPOSED`. |
| `REC-ADR3` | ADR-AUTH-003 verbatim clauses | `UNRESOLVED` | `ADR-AUTH-003.md`, blob `0fc6102e…` | Rules `CONFIRMED`; reconstruction `PROPOSED`. |
| `REC-AOS-01` | AVENOX/AOS Founder/final authority wording | `RECOVERED` | Google Doc version `8`, DOCX version `4`, document version `1.0 | 18 November 2026` | Context only, not AERON U/ADR canonical text. |
| `REC-AOS-02` | AVENOX/AOS explicit human approval wording | `RECOVERED` | Same blueprint | Context only. |
| `REC-CONSTRAINT-01` | Native Windows, .NET 10 LTS/WPF, ₹0, remote-only, no local models/weights, no paid fallback, OmniRoute untouched, Owner authority, default deny, permission-controlled tools, persistent memory, multi-agent, secure logging/audit/backups | `CONFIRMED` | Owner request v1 + `CONSTRAINTS.md` blob `57f9cb26…` | Preserved as boundary; not re-asked. |
| `REC-M2-COMP-01` | OwnerAuthorityGuard spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review. |
| `REC-M2-COMP-02` | PolicyDecisionEngine spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review. |
| `REC-M2-COMP-03` | OwnerApprovalService spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review. |
| `REC-M2-COMP-04` | Parameter-hashing infrastructure spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review. |
| `REC-M2-COMP-05` | AuditLedger spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review. |
| `REC-M2-COMP-06` | Windows Security Vault spec | `PROPOSED` | `SECURITY-ARCHITECTURE.md` v1.1 | Requires Owner review; no third-party vault dependency proposed. |
| `REC-M2-TEST-01` | Threat model, CI scope, acceptance criteria | `PROPOSED` | `SECURITY-THREAT-MODEL.md`, `M2-ACCEPTANCE-CRITERIA.md` | Requires Owner review. |
| `REC-M2-GRAPH-01` | Component/module dependency graph | `PROPOSED` | `M2-DEPENDENCY-GRAPH.md` | Preserves current project references; future module proposed only. |
| `REC-D-M1-06` | M1-closure-only RAM deviation | `CONFIRMED` | `DECISION_LEDGER.md`, local modified blob `be7efb3…` | Not extended to M2 or later; no signature invented. |
| `REC-M2-BUILD` | Explicit M2 implementation authorization | `UNRESOLVED` | Current request states specification development only | Implementation blocked. |
