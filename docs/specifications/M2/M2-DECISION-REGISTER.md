# AERON M2 — Owner Decision Register

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.
>
> Status: `OPEN — OWNER ACTION REQUIRED` for `D2-02`–`D2-10`, `D2-01-T1`, and `D2-01-T2` (`D2-01` recorded as `RESOLVED (CONDITIONAL)` on `2026-11-28`; implementation not authorized).
>
> This register records only decisions that are **genuinely unresolved** and that the M2
> implementation depends on. It does not restate, reselect, or reinterpret any confirmed
> decision: the 15 U-8R selections, ADR-AUTH-001/002/003 selections, the confirmed
> governance rules in `docs/governance/CONSTRAINTS.md`, and `D-M1-01`–`D-M1-06` stand as
> recorded. No entry below overrides them.

## How to read this register

Each entry states:

1. **What is unresolved** — the exact decision, not a general topic.
2. **Why implementation depends on it** — the concrete code path that is blocked or has
   a conservative fail-closed default.
3. **Technically viable alternatives** — options that exist within the ₹0 budget, .NET 10
   LTS, native Windows, no third-party agent framework, and remote-only inference.
4. **Security implications** — what each option does and does not protect against.
5. **Recommended option** — with justification.

Until an entry is resolved, the code behaves as documented in the "Current fail-closed
behavior" line. **No default in this register is an approval.** Recording "current
behavior" is not selecting an option.

---

## D2-01 — Owner MFA factor, enrollment, and recovery

- **Status:** `RESOLVED (CONDITIONAL)`
- **Resolution:** `Option A` — Windows Hello / TPM-bound platform authenticator (`DIRECT` attestation, `UV` `REQUIRED`, both enrollment and authentication ceremonies).
- **Approved decision document:** `D2-01 Revision 3.10` (`APPROVED AS RECORDED` by Project Owner).
- **Approval date:** `2026-11-28`
- **Governance dependency:** U-8R.3 `C — Multi-Factor Authentication` is `CONFIRMED`; the
  factor combination, technology, enrollment, fallback and recovery were recorded as
  `UNRESOLVED` in `docs/specifications/M2/U-08R.md`. Recovery remains governed separately
  under `D2-02`.
- **Historical scope at time of entry (preserved):** which factor(s) satisfy U-8R.3; how the Owner enrolls; what
  happens when the Owner loses the factor; whether any fallback exists at all.
- **Why implementation depends on it:** `IdentityAuthority.AuthenticateOwner` requires an
  `IOwnerFactorPolicy` that returns `IsApproved = true`. Production composition wires
  `UnapprovedOwnerFactorPolicy`. Therefore **no Owner session can be minted in
  production** and every Owner-only operation (permission provisioning, emergency-stop
  clear, approval decision, agent revocation) is denied. This is the intended
  fail-closed state, not a defect.
- **Viable alternatives (preserved unaltered):**
  - **A. Windows Hello / TPM-bound credential** via WebAuthn-style platform authenticator.
    Built-in, ₹0, hardware-backed when a TPM 2.0 is present (Windows 11 25H2 baseline
    requires TPM 2.0). Strongest phishing resistance.
  - **B. TOTP secret stored in the Windows vault** (RFC 6238, HMAC-SHA-1/256, 6 digits,
    30 s). ₹0, no external service, but the second factor lives on the same device as the
    first, which degrades it from "second factor" to "second check".
  - **C. FIDO2 hardware security key.** Strongest overall; requires the Owner to own a
    key, which is a purchase (budget impact must be confirmed by the Owner).
  - **D. No MFA; SID + explicit Owner confirmation dialog.** Contradicts U-8R.3 and is
    listed only to be rejected explicitly.
- **Security implications (preserved unaltered):** A is the only option that is both ₹0 and hardware-bound while
  keeping the trust anchor on the device. B is defeated by any local process running as
  the same user (which is the same threat model the rest of M2 already accepts) but it
  still blocks "another person at the unlocked machine". C is the strongest but may cost
  money. D removes the second factor entirely.
- **Historical recommendation (preserved unaltered):** **A — Windows Hello / TPM-bound platform authenticator**, with a
  documented recovery path decided under D2-02. It satisfies U-8R.3 without new spending
  and without moving the trust anchor off-device.
- **Approved D2-01 Revision 3.10 summary and mandatory conditions (`2026-11-28`):**
  - **Binding factor & ceremony rules:** Native Windows WebAuthn API (`webauthn.h`, `WEBAUTHN_API_VERSION_9` floor) + cryptographic assertions only (`UserConsentVerifier` alone never acceptable; no silent fallback to TOTP/software factors; Option C FIDO2 security key is never an automatic fallback; native origin validation is never claimed as browser-equivalent phishing resistance; recovery is separate under `D2-02`).
  - **Hardware AAGUID allowlist:** Fixed allowlist of `08987058-cadc-4b81-b6e1-30de50dcbe96` (Windows Hello hardware, TPM-backed) and `9ddd1817-af5a-4672-a2b9-3e3dd95000a9` (Windows Hello VBS hardware, TPM-backed); software AAGUIDs (`6028b017-b1d4-4c02-b4b3-afcdafc96bb2`, `6e96969e-a5cf-4aad-9b56-305fe6c82795`) and all others rejected; AAGUID is a selector only, never proof of TPM backing.
  - **TPM 2.0 attestation & `TPMA_OBJECT` / `TPMT_PUBLIC` verification (pinned to TCG TPM 2.0 Library Version 185, 12 March 2026):** `fmt = "tpm"` mandatory (`none` or non-`tpm` refused); full `certInfo` (`TPM_GENERATED_VALUE`, `TPM_ST_ATTEST_CERTIFY`, `extraData` under `attStmt.alg` hash, `attested.name`), `pubArea` ↔ COSE `credentialPublicKey`, AIK certificate (`2.23.133.8.3`, `CA = FALSE`), and `attStmt.alg` allowlist (`RS256` / `PS256` / `ES256`). Full 32-bit `TPMA_OBJECT` verification per Part 2 v1.85 §8.3.2 Table 37 & §8.3.3: Group A (`fixedTPM = 0x00000002`, `fixedParent = 0x00000010`, `sensitiveDataOrigin = 0x00000020`) all **SET**; Group B (`sign = 0x00040000` **SET**, `restricted = 0x00010000` **CLEAR**, `decrypt = 0x00020000` **CLEAR**, `x509sign = 0x00080000` **CLEAR** per Part 2 Table 37 / §8.3.3.15 & Part 3 §20.5.1 / §20.6.1); mandatory TPM consistency check `encryptedDuplication = 0x00000800` **CLEAR** when `fixedTPM` is SET (Part 2 §8.3.3.11, §8.3.3.1); reserved-bit mask `0xFFF0F009` (`must be 0`); bits 8/9 (`firmwareLimited` / `svnLimited`, Part 1 §41.3 / §41.4) parsed, reported, and consistency-checked without invented prohibitions; type-specific `TPMS_RSA_PARMS` (Table 228) vs `TPMS_ECC_PARMS` (Table 229) and `TPM_ALG_NULL` deprecation handling per Part 0 §3.1.4.2 (`TPM_ALG_NULL` rejected for ECC signing keys and never silently treated as a baseline `PASS` for RSA signing keys; final credential-key algorithm policy deferred to implementation design).
  - **Gates & governance boundary:** Gate 1 (attestation verification) ∧ Gate 2 (`D2-01-T1` trust anchor) ∧ Gate 3 (approved hardware AAGUID ∧ `TPMA_OBJECT` / `TPMT_PUBLIC` checks). Approval of `D2-01` fixes the policy text only: **no `MakeCredential` operation is authorized, no `GetAssertion` probe is authorized, production Owner MFA remains disabled, `UnapprovedOwnerFactorPolicy` remains active, no implementation authorization is granted, and `D2-02` through `D2-10` remain `OPEN`.**
- **Separate unresolved sub-dependencies (implementation-blocking):**
  - **`D2-01-T1` — TPM attestation trust anchors:**
    - **Status:** `OPEN / UNRESOLVED` (Gate 2 implementation-blocking; no alternative selected, no implicit default).
    - **Alternatives:** **A.** Pinned TPM vendor root certificates shipped and governed by AERON; **B.** `LocalMachine` certificate stores with strict root constraints (noting `LocalMachine\Root` is not a dedicated trusted TPM attestation root store); **C.** Signed FIDO Metadata Service (MDS) snapshots.
  - **`D2-01-T2` — Cleanup applicability for plugin-managed credentials:**
    - **Status:** `OPEN / UNRESOLVED` (implementation-blocking for credential-creation probing; Gate 1 evidence unobtainable while `OPEN`).
    - **Alternatives & standing:** **A.** Matching approved hardware AAGUID treated as proof of cleanup applicability (`excluded` by Owner direction `2026-11-26`); **B.** Require no plugin authenticator installed before running (`deferred` — reconsiderable only via a reliable independent mechanism; WebAuthn authenticator inventory alone insufficient); **C.** Post-creation empirical determination with unconditional exact-ID cleanup (`available, not selected`); **D.** Defer credential creation until cleanup applicability of `WebAuthNDeletePlatformCredential` / `WebAuthNGetPlatformCredentialList` to plugin-managed credentials is established through documented, supported platform behaviour (**recommended Owner direction, not a resolved decision**).
    - **Seven binding restrictions while `D2-01-T2` remains `OPEN`:** (1) read-only WebAuthn API capability preflight remains permitted; (2) no `MakeCredential` operation is permitted; (3) an AAGUID match must not be used as proof of cleanup applicability; (4) no alternative deletion mechanism may be introduced without separate Owner authorization; (5) exact-ID deletion and verification requirements remain intact in full; (6) Alternative B may be reconsidered only if a reliable independent mechanism establishes that no plugin authenticator is installed; (7) empirical deletion success in a probe does not independently resolve `D2-01-T2`.
- **Current fail-closed behavior:** `UnapprovedOwnerFactorPolicy` remains active and never authenticates;
  all Owner operations remain denied; the desktop surface reports the reason verbatim until `D2-01-T1`, `D2-01-T2`, all gates, `D2-02`, and separate explicit Owner implementation authorization are satisfied.

## D2-02 — Owner secure recovery procedure

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.10 `E — Custom Recovery Architecture` is `CONFIRMED`;
  the procedure itself is unrecorded.
- **What is unresolved:** how the Owner regains control after losing the authentication
  factor, reinstalling Windows, or moving to a new device — without creating a bypass that
  an attacker could use to seize Owner authority.
- **Why implementation depends on it:** `EnrollOwner` refuses re-enrollment while an Owner
  is enrolled. There is deliberately **no reset path** in M2. Losing the factor currently
  means deleting the AERON state directory by hand, which also destroys the audit chain.
- **Viable alternatives:**
  - **A. Printed one-time recovery code set** (e.g. 8 × 128-bit codes), generated at
    enrollment, stored off-device by the Owner, each single-use, hashed at rest with a
    memory-hard KDF. ₹0, standard, offline.
  - **B. Second Windows account designated as recovery Owner.** ₹0, but widens the
    authority surface to two OS accounts permanently.
  - **C. No recovery; documented manual reset that destroys state and requires a new
    enrollment plus a signed Owner statement.** Cheapest; irreversible data loss.
- **Security implications:** A keeps recovery offline and out of the machine's trust
  boundary; the risk is physical loss of the printed codes. B creates a second permanent
  Owner-equivalent identity, which conflicts with single-Owner authority. C is
  fail-safe but unrecoverable.
- **Recommendation:** **A**, with codes stored off-device, each usable once, hashed with
  PBKDF2-SHA-256 (≥ 600 000 iterations) or Argon2id if a reviewed implementation is
  approved, and every recovery attempt durably audited.
- **Current fail-closed behavior:** no reset path; lost factor = no Owner authority.

## D2-03 — Device identity and trust policy

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.9 `D — Comprehensive Device Security` is `CONFIRMED`;
  per-device permissions, risk-based authorization and remote revocation are unrecorded.
- **What is unresolved:** whether AERON trusts only the enrolled device, how a second
  device is admitted, and whether remote revocation (U-8R.9) is required in M2 or later.
- **Why implementation depends on it:** M2 binds Owner identity to the enrolled Windows
  SID on the current machine. There is no device identity record, no second-device flow,
  and no remote revocation channel. Any device identity work beyond "this machine" is
  blocked.
- **Viable alternatives:**
  - **A. Single-device, machine-bound trust** via TPM-backed key (DPAPI-CurrentUser +
    TPM). ₹0, simplest, matches a native single-user desktop assistant.
  - **B. TPM-backed per-device key with an Owner-approved device list** stored locally;
    adding a device requires the existing device to approve it.
  - **C. Full remote revocation service.** Requires outbound infrastructure, which is a
    ₹0-budget conflict unless it uses an existing free channel; also expands the attack
    surface.
- **Security implications:** A removes the remote-revocation requirement, which U-8R.9
  may demand. B satisfies "device list" locally but "remote" revocation from a lost
  device would need a channel. C is the only true remote revocation and the most costly.
- **Recommendation:** **B for M2**, with the remote channel explicitly deferred and
  recorded as a partial satisfaction of U-8R.9 rather than claimed complete.
- **Current fail-closed behavior:** trust is single-machine; there is no device list and
  no remote revocation. The status surface does not claim remote revocation.

## D2-04 — Windows isolation mechanism between Owner and agents

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.12 `C — Component Isolation and User Alert` is
  `CONFIRMED`; the Windows isolation mechanism itself is unrecorded.
- **What is unresolved:** how agents are isolated from the Owner identity — separate OS
  account, separate process with restricted token, AppContainer, or in-process logical
  isolation only.
- **Why implementation depends on it:** M2 agent authentication uses ECDSA keys and
  per-agent sessions, but agents still run as the same Windows user. An agent process can
  therefore read the same files the Owner can, including the audit file and the approval
  store. The `ApproverTrustRegistry` keeps requesters from approving through the AERON
  API, but it is **not** OS isolation and must not be described as such.
- **Viable alternatives:**
  - **A. Separate Windows standard user account for agent host**, with AERON Core
    running as the Owner account and enforcing cross-account access. Real OS boundary;
    requires user provisioning and complicates packaging.
  - **B. AppContainer / restricted-token agent host.** Real OS boundary within one user;
    requires Win32 packaging work and Capability SIDs.
  - **C. In-process logical isolation only** (first-party services plus the trust
    registry). No OS boundary; relies on code correctness.
- **Security implications:** A and B give a genuine security boundary and should be the
  target because U-8R.12 is a confirmed decision. C is what M2 currently implements and
  is not isolation in the OS sense.
- **Recommendation:** **B — AppContainer/restricted-token agent host**, because it
  provides a real boundary without a second interactive account, with A as a fallback if
  AppContainer packaging proves impractical for the tray + WPF shell.
- **Current fail-closed behavior:** agents hold no capabilities until the Owner grants
  them; the approval decision path requires an approver marker only the Owner surface
  registers. The boundary is logical, and documentation states so plainly.

## D2-05 — Vault envelope encryption and algorithm policy

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.4 `D — Comprehensive Encryption` is `CONFIRMED`;
  algorithms and key recovery were recorded as `UNRESOLVED`.
- **What is unresolved:** whether the implemented envelope scheme is the approved one.
- **Why implementation depends on it:** `WindowsCredentialVault` implements AES-256-GCM
  envelope encryption with a per-machine data key protected by Windows DPAPI
  (CurrentUser). Production composition wires `UnapprovedVaultEncryptionPolicy`, so the
  vault **refuses to store secrets**. Implementing an algorithm is not the same as
  approving it.
- **Viable alternatives:**
  - **A. Approve the implemented scheme** (AES-256-GCM, 96-bit random nonce per record,
    128-bit tag, DPAPI-CurrentUser-protected data key, keys under current-user ACL).
  - **B. Require a stronger KDF/TPM-bound key** (e.g. key sealed to TPM with PIN), which
    adds a dependency on D2-01's authenticator.
  - **C. Require per-secret keys**, which improves blast radius but multiplies key
    management.
- **Security implications:** A is standard and appropriate for local secrets. The nonce
  is random per record; at 2^32 records the birthday bound becomes non-negligible, so a
  counter-based scheme or per-file key rotation is the stricter choice. B and C are
  stronger but add complexity and dependencies.
- **Recommendation:** **A**, with an explicit note that random 96-bit nonces are safe for
  the anticipated secret counts (bounded well below 2^32) and that rotation re-encrypts
  under a fresh nonce.
- **Current fail-closed behavior:** storage and rotation are refused; retrieval reports
  `Unavailable`; nothing is silently stored unencrypted.

## D2-06 — Vault key recovery and backup scope

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.4 and U-8R.11 `C — Complete Application Backup` are
  `CONFIRMED`; the treatment of secrets inside a "complete" backup is not recorded.
- **What is unresolved:** whether the vault data key is included in a Google Drive backup,
  excluded, or re-wrapped under a separate Owner-held key; and whether DPAPI-protected
  material is restorable after a device reset (it is not, by construction).
- **Why implementation depends on it:** `SecurityComposition` stores the vault key as
  `vault/operation-binding.key` protected by DPAPI. After a Windows reset that file cannot
  be decrypted. Any "complete backup" that includes it would restore material that
  silently fails. Backup scope must therefore be decided before backup is implemented.
- **Viable alternatives:**
  - **A. Exclude vault material from backups** and require re-entry of secrets after
    restore. Safest, least convenient.
  - **B. Include an Owner-passphrase-wrapped copy of the data key** in the backup, with
    the passphrase stored only in the Owner's memory/off-device. Restorable, and a leaked
    backup alone is insufficient.
  - **C. Include DPAPI blobs as-is.** Restores fail after reset and encourages bad
    operator assumptions; listed to be rejected.
- **Security implications:** B is the only option that makes a complete backup genuinely
  restorable without uploading a bare key. It requires the Owner to keep a strong
  passphrase, which interacts with D2-02.
- **Recommendation:** **B**, with A as the fallback if the Owner will not maintain a
  separate recovery passphrase.
- **Current fail-closed behavior:** no backup implementation exists; vault storage is
  blocked by D2-05; nothing vault-related is copied anywhere.

## D2-07 — Independently protected audit checkpoint

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.7 `D — Comprehensive Audit System` is `CONFIRMED`;
  the checkpoint protection mechanism is `PROPOSED` in the spec pack.
- **What is unresolved:** which mechanism is "independent" enough to claim rollback and
  truncation resistance.
- **Why implementation depends on it:** `DurableAuditLedger` detects modification,
  deletion of interior records, truncation, and checkpoint mismatch. Those detections are
  only meaningful if an attacker who can rewrite the ledger file **cannot** rewrite the
  head anchor. `FileAuditCheckpointStore` writes a sibling file under the **same
  current-user ACL**, so a process running as the Owner can rewrite both. M2 therefore
  reports the anchor as `WeakSameUser` and does **not** claim rollback resistance.
- **Viable alternatives:**
  - **A. TPM-sealed monotonic counter / NV index** holding the sequence and head hash.
    Strong; requires TPM 2.0 (present on the Windows 11 25H2 baseline) and Windows TBS
    API work.
  - **B. Separate Windows account that owns the checkpoint**, with the AERON process
    granted write-through only via an explicit helper. Real separation; more provisioning.
  - **C. Append-only remote anchor** (e.g. a free-tier object store with versioning).
    Conflicts with ₹0 and introduces network trust.
  - **D. Windows event log as a secondary witness** for the chain head. Cheap, but the
    event log is writable by administrators.
- **Security implications:** Only A and B provide a boundary that a same-user attacker
  cannot cross. A is the strongest and is hardware-backed.
- **Recommendation:** **A — TPM-sealed NV counter**, with the `FileAuditCheckpointStore`
  retained explicitly labelled as weak until A is implemented.
- **Current fail-closed behavior:** ledger integrity is verified and reported honestly;
  the status surface states that rollback resistance is not yet claimed.

## D2-08 — Approval role separation and cross-process exclusion

- **Status:** `UNRESOLVED`
- **Governance dependency:** ADR-AUTH-003 confirmed rules require explicit Owner approval;
  the mechanism is `PROPOSED`.
- **What is unresolved:** whether approval decisions must be separated from the requester
  at the OS level, and whether the durable approval store needs cross-process exclusion.
- **Why implementation depends on it:** `ApproverTrustRegistry` is an in-process marker
  registry. It prevents any code path that lacks the marker from approving, including
  background services and agents where they are separate DI instances. It does **not**
  prevent a same-user process from calling the store's file format directly. The approval
  store also serializes within one process only.
- **Viable alternatives:**
  - **A. Keep in-process marker separation** and rely on D2-04 for the real boundary.
  - **B. Authenticated local IPC between an Owner-only UI process and the kernel** with a
    per-session token, so approval decisions cross a real process boundary.
  - **C. Signed approval records** where the Owner UI signs the approval with a key from
    D2-01's authenticator, so a forged approval file fails verification.
- **Security implications:** C is the strongest and reuses the authenticator; B gives a
  process boundary but still relies on the token not leaking; A is what M2 does.
- **Recommendation:** **C**, which composes naturally with D2-01's platform
  authenticator and makes approval evidence independently verifiable.
- **Current fail-closed behavior:** approver marker required; agent sessions cannot
  approve; the status surface states the boundary is in-process.

## D2-09 — Emergency-stop reach and irreversibility statement

- **Status:** `UNRESOLVED`
- **Governance dependency:** U-8R.6 `D — Comprehensive Emergency Protection` and
  U-8R.12 `C — Component Isolation and User Alert`, plus ADR-AUTH-001 rule 4 (owner pause,
  cancel, revoke, reconfigure, emergency-stop preempts every subsystem).
- **What is unresolved:** what emergency stop must additionally do beyond what M2
  implements — kill running OS processes, block network egress at the firewall, wipe
  caches, require re-authentication to clear.
- **Why implementation depends on it:** M2 emergency stop latches a flag, revokes all
  agent sessions, bumps the preemption epoch (invalidating in-flight permits), and denies
  authentication, approvals, and dispatch. It does **not** terminate agent processes, and
  it cannot reverse an external side effect already started. Additional reach requires
  design and, for process termination, a decision about whether AERON may kill Owner
  processes.
- **Viable alternatives:**
  - **A. Keep the logical latch only** and document the limits; clearing requires the
    Owner session per D2-01.
  - **B. Add agent-host process termination** once D2-04 defines the host boundary.
  - **C. Add OS-level network suppression** (Windows Firewall rule injection), which is a
    powerful and risky capability.
- **Security implications:** B materially improves containment once a process boundary
  exists. C is powerful but can break unrelated Owner network activity, and it changes OS
  state — which would itself be an `L2` consequential action.
- **Recommendation:** **A for M2, B when D2-04 lands**; C should be explicitly rejected
  unless the Owner asks for it.
- **Current fail-closed behavior:** latch + session revocation + epoch bump; the UI and
  docs say an already-started external effect cannot be rolled back.

## D2-10 — Secret-shaped material in state files and diagnostics

- **Status:** `UNRESOLVED`
- **Governance dependency:** C-12 (zero secret exposure) is `CONFIRMED`; how
  secret-shaped material in *state files* is protected is not recorded.
- **What is unresolved:** whether DPAPI-protected vault material in the state directory
  needs ACL hardening beyond the M1 current-user ACL, and whether vault bytes must be
  structurally barred from every logging path.
- **Why implementation depends on it:** `RedactionFilter` scrubs known secret patterns, but
  DPAPI and AES-GCM blobs are opaque bytes that match no pattern, so a hexadecimal dump
  would not be redacted. M2 never logs those bytes and a test asserts plaintext never
  appears on disk; the invariant still rests on discipline rather than a filter.
- **Viable alternatives:**
  - **A. Structural ban**: the vault holds no logger reference at all, so it cannot log
    raw bytes even by mistake; diagnostics flow through `IRedactionFilter` only.
  - **B. Log only a SHA-256 fingerprint** when diagnostics must reference key material.
  - **C. Extend the redaction filter to recognize a DPAPI blob prefix.** Weak and
    format-coupled.
- **Security implications:** A removes the possibility at the type level, which is stronger
  than any runtime filter. B adds a useful reference without exposing material.
- **Recommendation:** **A + B**.
- **Current behavior:** A is implemented (`WindowsCredentialVault` takes only
  `IRedactionFilter` and no logger); B is not yet exercised because no diagnostics need a
  key fingerprint.

---

## Confirmed decisions preserved unchanged (not open)

The following are recorded here only to make clear they are **not** in scope for change:

- `U-8R.1`–`U-8R.15` selections as recorded in `docs/specifications/M2/U-08R.md`.
- `ADR-AUTH-001 C — Core-Led Governance`, `ADR-AUTH-002 C — Restricted Dual
  Authorization`, `ADR-AUTH-003 B — Complete Owner Authority`.
- `CONSTRAINTS.md` C-01–C-12, the `G1`–`G4` external domains and `L0`–`L3` local tiers.
- `D-M1-01`–`D-M1-06`, including the M1-only memory deviation and its non-extension.
- Original `U-8` remains separate and `UNRESOLVED`; this register does not touch it.

## Resolution procedure

1. The Project Owner records the selected option for an entry by amending this file with a
   dated decision line (`Resolution: <option>`), signed by Owner statement.
2. The implementation then replaces the fail-closed default with the recorded choice and
   adds the tests named in the entry.
3. The status surface and this register are updated together; neither is updated alone.
4. No entry is resolved by an agent, by the implementation, or by implication from
   existing code.
