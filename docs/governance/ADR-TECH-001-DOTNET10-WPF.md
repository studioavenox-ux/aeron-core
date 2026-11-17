# ADR-TECH-001 — Primary Native Windows Technology Stack & Platform Baseline

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

- **Identifier:** `ADR-TECH-001`
- **Status:** `CONFIRMED` (Authorized by Project Owner on `2026-11-17`)
- **Decisions Recorded:** `D-M1-01`, `D-M1-02`, `D-M1-03`, `D-M1-04`, `D-M1-05`

---

## Decision

1. **Primary Technology Stack:** **C# 14 + .NET 10 LTS + WPF**, packaged as a native Windows desktop executable (`WinExe`, `win-x64`) composed of modular, independently testable assemblies (`net10.0` headless libraries + `net10.0-windows` desktop shell).
   - End-of-support `.NET 8` and short-term `.NET 9` are prohibited.
2. **Warnings-as-Errors & Experimental WPF API Policy:** `TreatWarningsAsErrors=true` is enforced solution-wide with zero global `<NoWarn>` suppressions and zero `#pragma warning disable` bypasses in Milestone 1. Because WPF's built-in Fluent/theme-mode APIs remain marked `[Experimental("WPF0001")]`, Milestone 1 implements its dark UI using a stable standard WPF `ResourceDictionary` (`src/Aeron.Desktop/Themes/FoundationDark.xaml`).
3. **Platform Separation:**
   - **Minimum Supported OS Baseline:** **Windows 11 Version 25H2 (Build 26200, `x64`) or newer**.
   - **Recorded `APEX` Development Host:** Windows 11 (`x64`), Intel Core i5 (13th Generation), 16 GB RAM, Intel Integrated Graphics (shared memory, no discrete GPU).
4. **Window Close (`X`) Semantics:** Defaults to minimizing AERON to the Windows system tray (`Window.CloseToTray = true`, Owner-configurable), with explicit *Exit AERON* available in both the system tray menu and main window.
5. **Repository Designation:** Private repository policy under proprietary copyright: *Copyright (c) 2026 Avenox Studio. All Rights Reserved.*

---

## Supporting Research Summary (Retained)

- **C# 14 + .NET 10 LTS + WPF *(Approved)*:** Active LTS support, ₹0 cost, 100% native Windows executable without embedded browser or localhost server, clean `net10.0` headless module isolation, direct Win32/ACL/DPAPI access, and low-overhead DirectX composition appropriate for `APEX`'s 16 GB RAM and Intel integrated graphics.
- **C# 14 + .NET 10 LTS + WinUI 3 *(Evaluated, Not Selected)*:** Native Windows UI, but adds heavier Windows App SDK packaging and tray/unpackaged interop overhead.
- **Tauri v2 — Rust + WebView2/TypeScript *(Evaluated, Not Selected)*:** Strong Rust safety, but introduces dual-language complexity and relies on an embedded Chromium WebView2 runtime.
- **Python + PySide6 / Qt 6 *(Evaluated, Not Selected)*:** Runtime-typed with weaker compile-time module isolation and larger distribution footprint (initial repo `.gitignore` was a bootstrap default only, retained for safety).
