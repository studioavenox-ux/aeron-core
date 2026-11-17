# ADR-AUTH-002 — Permission Boundaries & Separated External Authorization (Part 2)

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Identifier:** `ADR-AUTH-002`
- **Status:** `CONFIRMED` (permission & gateway separation mandate) / `UNRESOLVED` (canonical verbatim historical text)
- **Confirmed Governance Rules:**
  1. AERON enforces an independent, default-deny permission engine separate from AERON Core and agent reasoning.
  2. External governance strictly separates four distinct domains:
     - **`G1` Approved Remote Model Inference:** Routine requests over an Owner-approved, zero-cost OmniRoute gateway configuration execute without per-message approval prompts while enforcing approved provider, model, zero-cost, remote-only, redaction, and data-disclosure policies, with **zero paid or local inference fallback**.
     - **`G2` External Data Disclosure:** Governed by data classification and explicit Owner approval before disclosing local/restricted data off-device; secrets are strictly blocked and redacted.
     - **`G3` External Communications:** Mandatory explicit per-action Owner approval before sending any external communication.
     - **`G4` External Account Modifications:** Mandatory explicit per-action Owner approval before altering external accounts/services; mutating OmniRoute or enabling paid charges is permanently prohibited.
  3. v1 strictly prohibits dynamic skill-script execution.
- **Unrecovered Historical Text Note (`UNRESOLVED`):** Exact historical clauses from the original external `ADR-AUTH-002` document will be inserted verbatim when provided by the Project Owner prior to Milestone 2.
