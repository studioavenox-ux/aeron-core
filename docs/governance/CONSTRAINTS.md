# AERON — Non-Negotiable Constraints & Authorization Boundaries

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

## 1. Core Constraints (`CONFIRMED`)

| ID | Constraint | Enforcement |
|---|---|---|
| **C-01** | **₹0 development and operating budget** | Free/OSS tooling and libraries only; strict zero-cost model/service enforcement. |
| **C-02** | **Native Windows desktop executable** | Compiled `.NET 10 LTS` + `WPF` `win-x64` executable; never a web app or localhost UI. |
| **C-03** | **Remote-only AI inference** | `Security.RemoteOnlyInference = true` locked fail-closed; all inference executes remotely. |
| **C-04** | **No local LLM runtimes or model weight downloads** | `Security.AllowLocalInference = false`; CI and architecture tests ban local LLM libraries and model weight formats (`.gguf`, `.safetensors`, `.onnx`, `.pt`, `.pth`, `.tflite`). |
| **C-05** | **Preserve existing OmniRoute installation, config, and data** | OmniRoute is never modified, reconfigured, or queried for raw secrets; M3+ connects solely via read/inference `CLIENT_API`. |
| **C-06** | **No paid or local inference fallback** | `Security.AllowPaidFallback = false`; fail-closed pause/queue + Owner notice when approved ₹0 routes are unavailable or exhausted. |
| **C-07** | **AERON owns its agent, permission, and execution engines** | Designed and implemented natively within `aeron-core`. |
| **C-08** | **No mandatory third-party agent framework** | Architecture tests forbid LangChain, SemanticKernel, AutoGen, CrewAI, and equivalent runtime dependencies. |
| **C-09** | **No skill-script execution in v1** | `Security.AllowSkillScripts = false`; v1 tools (M7) are strictly compiled, statically registered modules. |
| **C-10** | **Ultimate Project Owner authority** | Owner commands, policy locks, cancellations, and emergency stops preempt AERON Core and every agent immediately. |
| **C-11** | **Mandatory Owner approval for consequential actions** | Enforced deterministically across `G2`, `G3`, `G4`, and `L2` before any consequential side effect executes. |
| **C-12** | **Zero secret exposure in configs, logs, prompts, or memory** | Config files reject inline credentials; `RedactionFilter` scrubs every log entry and journal write; runtime storage stays in user-ACL-protected `%LOCALAPPDATA%\Aeron` outside cloud-synced `Documents`. |

---

## 2. Separated External Authorization Domains (`G1`–`G4`)

1. **`G1` — Approved Remote Model Inference:**
   - Normal inference requests through an **explicitly Owner-approved, zero-cost gateway configuration** do **not** require repeated per-message approval prompts.
   - Non-bypassable automated guards enforce approved provider, approved model/combo, verified `$0.00` cost, remote-only routing, pre-wire secret redaction, data-disclosure policy compliance, and zero paid or local fallback.
   - Adding or modifying approved gateway endpoints, providers, models, cost rules, or disclosure policies requires explicit Owner approval.
2. **`G2` — External Data Disclosure:**
   - Governs transmitting local files, private documents, restricted memory items, or host artifacts off-device (including embedding `LocalPrivate` data into `G1` prompts or sending data to external APIs).
   - `Secret` data is hard-blocked and redacted; `LocalPrivate` / restricted data requires policy clearance and explicit Owner approval specifying exact source and destination; `RoutineContext` data flows only within the approved `G1` disclosure policy.
3. **`G3` — External Communications:**
   - Governs any outbound email, message, chat, invite, comment, webhook, or externally visible post.
   - Agents may stage local drafts only; transmission always requires explicit, per-action, parameter-hashed Owner approval.
4. **`G4` — External Account Modifications:**
   - Governs any write, edit, delete, share, permission change, repository mutation, or setting modification on external accounts or cloud services.
   - Permitted integrations require explicit, per-action, parameter-hashed Owner approval; mutating OmniRoute or enabling paid/billed services is permanently forbidden.

---

## 3. Local Machine Authorization Tiers (`L0`–`L3`)

- **`L0` — Isolated AERON Internal State:** Operations inside `%LOCALAPPDATA%\Aeron` (`config`, `data`, `backups`, `logs`) protected by current-user Windows ACLs.
- **`L1` — Bounded Safe Local Reads / Drafts:** Policy-permitted non-destructive reads and local staging with audit logging.
- **`L2` — Consequential Local Actions:** Writing/deleting/moving Owner files, launching external processes, altering OS state, persistent schedule changes (`U-4`), or applying controlled evolution proposals (`U-1`) — **mandatory explicit Owner approval**.
- **`L3` — Forbidden Local Actions:** v1 skill-script execution, local LLM execution or model downloads, reading external `.env`/secret files, disabling redaction or audit, or bypassing Owner authority — **hard-denied in code**.
