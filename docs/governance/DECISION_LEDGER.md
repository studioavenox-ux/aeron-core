# AERON — Master Governance & Decision Ledger

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

Status vocabulary used strictly across all AERON records:
- **`VERIFIED`:** Directly verified from repository files, automated checks, or inspected non-secret environment artifacts.
- **`CONFIRMED`:** Explicitly directed or approved by the Project Owner.
- **`PROPOSED`:** Recommended architecture/design awaiting milestone-specific Owner authorization.
- **`UNRESOLVED`:** Canonical specification text or technical choice not yet available/decided; **never invented**.

---

## 1. Historical Governance Decisions

| ID | Title / Scope | Status | Record |
|---|---|---|---|
| **U-1** | Controlled evolution | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-2** | Intelligent adaptive modes | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-3** | Dynamic prioritization | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-4** | Adaptive scheduling | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-5** | Adaptive notifications | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-6** | Human-like agent team, realistic avatars, controlled autonomy, intelligent team communication, adaptive personalities | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-7** | Complete contextual memory | `CONFIRMED` (principle) / `UNRESOLVED` (canonical text) | [`U-01-TO-U-07.md`](U-01-TO-U-07.md) |
| **U-8** *(Original)* | Original Decision U-8 *(strictly separate from `U-8R`)* | `UNRESOLVED` (canonical text not yet supplied) | [`U-08-ORIGINAL.md`](U-08-ORIGINAL.md) |
| **U-8R** *(Replacement)* | Replacement Decision U-8R *(strictly separate from `U-8`)* | `UNRESOLVED` (canonical text not yet supplied) | [`U-08R-REPLACEMENT.md`](U-08R-REPLACEMENT.md) |
| **ADR-AUTH-001** | Core governance & ultimate Project Owner authority (Part 1) | `CONFIRMED` (intent) / `UNRESOLVED` (verbatim clauses) | [`ADR-AUTH-001.md`](ADR-AUTH-001.md) |
| **ADR-AUTH-002** | Permission, delegation & `G1–G4` / `L0–L3` authorization boundaries (Part 2) | `CONFIRMED` (intent) / `UNRESOLVED` (verbatim clauses) | [`ADR-AUTH-002.md`](ADR-AUTH-002.md) |
| **ADR-AUTH-003** | Consequential approval, override preemption & audit governance (Part 3) | `CONFIRMED` (intent) / `UNRESOLVED` (verbatim clauses) | [`ADR-AUTH-003.md`](ADR-AUTH-003.md) |

---

## 2. Approved Milestone 1 Decisions (`CONFIRMED` — 2026-11-17 and 2026-11-19)

| ID | Decision | Status | Record |
|---|---|---|---|
| **D-M1-01** | C# 14 + .NET 10 LTS + WPF native Windows desktop executable, modular architecture, stable non-experimental WPF `ResourceDictionary` dark styling under `TreatWarningsAsErrors=true` | `CONFIRMED` | [`ADR-TECH-001-DOTNET10-WPF.md`](ADR-TECH-001-DOTNET10-WPF.md) |
| **D-M1-02** | Minimum supported OS: Windows 11 Version 25H2 (Build 26200, `x64`) or newer (distinct from `APEX` dev host: Windows 11, Intel Core i5 13th Gen, 16 GB RAM, Intel integrated graphics) | `CONFIRMED` | [`ADR-TECH-001-DOTNET10-WPF.md`](ADR-TECH-001-DOTNET10-WPF.md) |
| **D-M1-03** | Closing the main window (`X`) minimizes AERON to the system tray by default (`Window.CloseToTray = true`) | `CONFIRMED` | [`ADR-TECH-001-DOTNET10-WPF.md`](ADR-TECH-001-DOTNET10-WPF.md) |
| **D-M1-04** | Private repository policy with Proprietary — All Rights Reserved, Avenox Studio designation | `CONFIRMED` | [`LICENSE`](../../LICENSE) |
| **D-M1-05** | Explicit Milestone 1 `BUILD` authorization on branch `arena/8ed7a07b-aeron-core` submitted via reviewable PR to `main` | `CONFIRMED` | [`../milestones/M1-FOUNDATION.md`](../milestones/M1-FOUNDATION.md) |
| **D-M1-06** | Owner accepts the approximately 280 MB Working Set deviation for M1 closure only; the original M1 target of ≤120 MB is preserved; the performance target is not achieved and must not be claimed as achieved; memory profiling and optimization remain future work; this acceptance does not extend to M2 or later milestones | `CONFIRMED` | Owner approval (source; decision date 2026-11-19) |

> **D-M1-06 source:** Owner approval, decision date 2026-11-19. No signature or external record reference was supplied.

---

## 3. Deferred Decisions for Future Milestones (`UNRESOLVED`)

- **Pre-M2:** Supply canonical verbatim texts for `U-1`–`U-7`, original `U-8`, replacement `U-8R`, and `ADR-AUTH-001`–`003`.
- **Pre-M3:** Confirm exact OmniRoute `CLIENT_API` endpoint, approved ₹0 providers/models/combos for standing `G1` inference, and baseline `G2` disclosure rules.
- **Pre-M5:** Confirm SQLite FTS5-only retrieval vs. hybrid FTS5 + approved ₹0 remote embeddings via OmniRoute.
- **Pre-M6:** Confirm founding v1 specialist agent roster, delegation boundaries, and adaptive personality constraints (`U-6`).
- **Pre-M7:** Confirm compiled v1 built-in tool catalog, external connectors, and detailed `U-1`/`U-3`/`U-4`/`U-5` operational parameters.
- **Pre-M8:** Confirm approved ₹0 voice STT/TTS pipeline.
- **Pre-M10:** Confirm realistic avatar asset workflow and Intel-iGPU-targeted Virtual Headquarters rendering format.
