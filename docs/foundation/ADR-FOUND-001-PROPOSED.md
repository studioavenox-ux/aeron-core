# ADR-FOUND-001 — Sir Thaddeus as an upstream reference for AERON (Selective Adoption)

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Identifier:** `ADR-FOUND-001`
- **Status:** `PROPOSED` — awaiting Project Owner decision. It is **not** `CONFIRMED`, and it does not change the Decision Ledger, the M2 Decision Register, or any ADR-AUTH record.
- **Date:** 2026-11-28
- **Basis:** [`SIR-THADDEUS-ADOPTION-AUDIT.md`](SIR-THADDEUS-ADOPTION-AUDIT.md)

## Context

The Project Owner asked for AERON to be built on the open-source Sir Thaddeus project (Apache-2.0, upstream `raydeStar/sir-thaddeus`) rather than from scratch. The audit found:

1. Upstream is .NET 10 and shares AERON's runtime, SQLite library and licence family, so reuse is technically plausible.
2. Upstream's product topology conflicts with ratified AERON constraints: a Photino/WebView2 shell with a React UI, a Kestrel loopback server, default local LLM endpoints (LM Studio, Ollama, Codex CLI), and local model weights (Kokoro, Piper, Whisper.cpp).
3. Upstream's authority model (in-memory permission tokens, Off/Ask/Always groups, arbitrary system-command tools, regex-scrubbed JSONL audit) is weaker than AERON's security kernel, which AERON's Security Specifications require to take precedence.
4. AERON's architecture tests enforce the exact project list, ban the upstream UI and runtime stack, and ban the local-model packages.
5. C-07 requires AERON to own its agent, permission and execution engines.

## Options considered

| Option | Description | Assessment |
|---|---|---|
| **A. Whole-repository adoption** | Replace `main` with the upstream tree, or merge upstream history | **Rejected.** Destroys AERON's security kernel, fails the architecture tests, reintroduces local inference and a localhost UI. |
| **B. Reference-only** | Use upstream as design documentation and write everything new | Compatible, but duplicates the effort the Owner wants to avoid, and leaves useful permissive utilities (document parsing, memory schema, OpenAI request shapes) unused. |
| **C. Selective, module-by-module adoption** | Port individual permissive modules, adapt them to AERON interfaces, route every effect through AERON's kernel, and keep attribution | **Recommended.** Compatible with constraints when each module is gated. |
| **D. Git subtree / submodule of upstream** | Vendor the upstream tree into a subdirectory | **Rejected.** Imports unrelated UI, benchmark and local-model code, and the architecture tests would reject it. |

## Decision (proposed)

1. Adopt **Option C**. Sir Thaddeus is a **reference and source of permissively licensed modules**. It is not the AERON product base.
2. AERON's existing `main` remains the product baseline. Its security kernel, architecture tests, CI gates and M2 decisions are preserved.
3. Upstream **engine** code for permissions, tool execution, agent orchestration and audit is **not adopted**. This is the recommended reading of C-07. The Owner must confirm it.
4. Upstream **UI, runtime-host, local-inference, local-voice, SearXNG, and benchmark-harness** code is **excluded**.
5. Any module that is ported:
   - requires its own milestone, Owner authorization, and PR;
   - carries an attribution header (`Adapted from raydeStar/sir-thaddeus @ <sha>, Apache-2.0`);
   - is recorded in [`../../THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) and [`MIGRATION-MAP.md`](MIGRATION-MAP.md);
   - passes the full `ci-windows.yml` gate, including the security-suite minimum;
   - does not weaken any architecture test. A new project requires an explicit, Owner-approved change to the scope guard.
6. Inference remains **remote-only**, through OmniRoute. No local runtime, no model-weight download, no paid fallback (C-03, C-04, C-06).
7. Dependencies with unverified or restricted licences are not adopted until verified (see the audit §4).

## Consequences

- **Positive:** AERON reuses proven, permissively licensed engineering in document handling, memory schema and OpenAI-style client patterns, without inheriting the topology or authority model that conflicts with AERON.
- **Negative:** several upstream features (local-first voice, an embedded web UI, a benchmark method) are not available and must be built AERON-native or sourced from approved remote providers. This is more work than a fork would be.
- **Neutral:** the upstream project continues to change. Adopted code is pinned to a commit SHA and maintained in AERON on its own schedule.

## Conditions that would change this decision

- The Owner decides C-07 permits adopting upstream engine code. Then the permission and tool modules move from *Replace* to *Adapt*, subject to the security kernel.
- The Owner approves a different desktop UI technology. This would require a new ADR superseding ADR-TECH-001.

## Blocking decisions

| Decision | Status |
|---|---|
| Confirm Option C and the C-07 reading in §Decision 3 | `UNRESOLVED` |
| Pre-M3 OmniRoute `CLIENT_API` endpoint and ₹0 allowlist | `UNRESOLVED` |
| Pre-M5 FTS5-only vs hybrid retrieval | `UNRESOLVED` |
| Pre-M8 approved ₹0 STT/TTS | `UNRESOLVED` |
| MCP SDK and SmartReader licence verification | `UNRESOLVED` (verification task) |
| D2-01 T1/T2, D2-02, D2-05, D2-06 | `OPEN` (unchanged) |

## Explicit non-claims

- This ADR does not authorize any implementation.
- This ADR does not authorize any OmniRoute connection.
- This ADR does not authorize any Owner factor, credential, or production secret.
- This ADR does not change `DECISION_LEDGER.md`, the M2 Decision Register, or ADR-AUTH-001…003.
