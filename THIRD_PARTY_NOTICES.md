# Third-Party Notices — AERON

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

This file records third-party projects and packages that AERON references, has audited, or has incorporated. It is maintained alongside the adoption audit in [`docs/foundation/`](docs/foundation/SIR-THADDEUS-ADOPTION-AUDIT.md).

## 1. Upstream project: Sir Thaddeus (reference, not yet incorporated)

- **Project:** Sir Thaddeus
- **Repository:** https://github.com/raydeStar/sir-thaddeus
- **Audited revision:** `845b50c9b450d77cf7d634dd90bc72bee37844b5`
- **Licence:** Apache License, Version 2.0 (upstream `LICENSE`, verified via the GitHub licence API)
- **Copyright:** The upstream `LICENSE` does not name a copyright holder. Attribution is to the upstream project and its owner, `raydeStar`, and its contributors.
- **NOTICE file:** none exists upstream.
- **Current use in AERON:** **none.** No upstream source has been copied into this repository. The upstream was audited only. Module-level reuse, if approved later, will carry an attribution header and be recorded in `docs/foundation/MIGRATION-MAP.md`.
- **Trademark:** Apache-2.0 §6 grants no trademark rights. "Sir Thaddeus" is not AERON's product name.

## 2. Dependencies assessed by the audit

Licences were confirmed from GitHub repository metadata unless marked otherwise.

| Package | Licence | Status in AERON |
|---|---|---|
| Microsoft.Data.Sqlite | MIT | **In use** (AERON `Aeron.Infrastructure`, pinned in `Directory.Packages.props`) |
| Photino.NET | Apache-2.0 | Not used (excluded: browser-wrapper shell) |
| KokoroSharp.CPU | MIT | Not used (excluded: local model download) |
| Microsoft.Playwright (.NET) | MIT | Not used (excluded for now) |
| ModelContextProtocol (C# SDK) | Licence transitioning MIT → Apache-2.0; **pinned version not verified** | Not used (deferred to M7; verify first) |
| PdfPig | Apache-2.0 | Not used (deferred to M5) |
| DocumentFormat.OpenXml | MIT | Not used (deferred to M5) |
| Serilog | Apache-2.0 | Not used |
| AngleSharp | MIT | Not used (deferred) |
| HtmlAgilityPack | MIT | Not used (deferred) |
| YamlDotNet | MIT | Not used (excluded) |
| NCalc | MIT | Not used (deferred) |
| Cronos | MIT | Not used (deferred to M7) |
| NUlid | MIT | Not used |
| SQLitePCLRaw | Apache-2.0 | Not referenced directly. It may appear transitively through `Microsoft.Data.Sqlite`; the exact transitive set is **not verified** here (no NuGet restore is possible in the audit environment). |
| SmartReader | **Not verified** | Not used. Verify before any use. |
| SearXNG (bundled upstream in `apps/searxng`) | **AGPL-3.0-or-later** | **Excluded.** Not to be bundled or linked. |
| Kokoro / Piper / Whisper.cpp model weights | Model-specific | **Excluded.** Local model weights are forbidden (C-04). |

## 3. Attribution obligations

- Apache-2.0 requires that any redistribution of upstream source includes a copy of the licence and preserves copyright, patent, trademark and attribution notices. Because no upstream source is redistributed here, a copy of the licence is not yet bundled. This must be done before any port is merged.
- Any modified file ported from upstream must carry a prominent notice stating that it was changed.

## 4. Maintenance

This file is updated in the same pull request that adds, removes, or changes a third-party dependency or ported component.
