# Milestone 1 — Part B Interactive Windows Verification (`APEX`)

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

Automated CI on headless `windows-latest` runners **cannot** prove visible system tray rendering, foreground window restoration on a live desktop, or actual hardware resource behavior. Those criteria must be verified interactively on the Project Owner's Windows workstation (`APEX`: Windows 11, Intel Core i5 13th Gen, 16 GB RAM, Intel Integrated Graphics; minimum supported OS: **Windows 11 Version 25H2, Build 26200+, `x64`**).

---

## Runtime Prerequisite (.NET 10 Windows Desktop Runtime)

`Aeron.Desktop.exe` is published as a **framework-dependent** native `win-x64` WPF executable (`--self-contained false`) targeting `net10.0-windows`. Before launching it on `APEX` (or any supported Windows 11 25H2+ `x64` system), ensure the **.NET 10 LTS Windows Desktop Runtime (`Microsoft.WindowsDesktop.App 10.0.x`, `x64`)** is installed:

- **Running from a local source checkout:** Installing the **.NET 10 LTS SDK (`x64`)** (`10.0.100` or newer `10.0.x` feature band) already includes `Microsoft.WindowsDesktop.App 10.0.x`.
- **Running the downloaded `aeron-desktop-win-x64` CI artifact without the SDK:** Install the free official **.NET 10 LTS Windows Desktop Runtime (`x64`)** (for example via `winget install --id Microsoft.DotNet.DesktopRuntime.10 --exact --architecture x64` or the official Microsoft .NET 10 download page).
  - *Important:* The base `.NET Runtime` (`Microsoft.NETCore.App` alone) is **not sufficient** to launch a WPF desktop executable; `Microsoft.WindowsDesktop.App 10.0.x` (`x64`) is specifically required.

Verify the runtime prerequisite (read-only):

```powershell
dotnet --list-runtimes | Select-String '^Microsoft\.WindowsDesktop\.App 10\.0\.'
```

Expected output: at least one `Microsoft.WindowsDesktop.App 10.0.x` line under the `x64` `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App` path.

---

## Preparation

**Option A — Publish and launch locally from repository root (requires .NET 10 LTS SDK `x64`):**

```powershell
dotnet publish src/Aeron.Desktop/Aeron.Desktop.csproj -c Release -r win-x64 --self-contained false -warnaserror -o artifacts/publish/win-x64
Start-Process -FilePath ".\artifacts\publish\win-x64\Aeron.Desktop.exe"
```

**Option B — Launch downloaded CI artifact (requires .NET 10 LTS Windows Desktop Runtime `x64`):**

1. Extract `aeron-desktop-win-x64.zip` to a local non-synced directory (outside `Documents` and outside any OmniRoute folder).
2. Launch `Aeron.Desktop.exe` directly or via `Start-Process -FilePath ".\Aeron.Desktop.exe"`.

---

## Interactive Verification Checklist

| ID | Test Procedure on `APEX` | Expected Result | Measured / Observed Result |
|---|---|---|---|
| **AT-M1-APEX-01** | Confirm `.NET 10 Windows Desktop Runtime (x64)` prerequisite, then launch `Aeron.Desktop.exe` on Windows 11 25H2+. Inspect rendering, DPI clarity, OS baseline banner, and `%LOCALAPPDATA%\Aeron\{config,data,logs}`. | Crisp native dark window opens with zero console/browser window, missing-runtime prompt, or firewall prompt; OS banner reports compliant (`Build >= 26200`); `aeron.config.json`, `aeron_core.db`, and `aeron.jsonl` exist under `%LOCALAPPDATA%\Aeron`. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-02** | Click window close (`X`), verify icon in Windows 11 system tray/overflow + tooltip, right-click menu (*Open AERON*, *Minimize to Tray*, *Exit AERON*), double-click restore, then click *Exit AERON*. | Closing (`X`) hides window from taskbar and keeps AERON running in tray; double-click and *Open AERON* restore window; *Exit AERON* removes tray icon immediately with zero ghost icon and zero lingering `Aeron.Desktop` process in Task Manager. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-03** | While AERON is (a) minimized to taskbar and (b) hidden in the system tray, execute `Aeron.Desktop.exe` a second time. | Second process exits immediately (`ExitCode 0`) and the existing AERON window restores visibly to the foreground in both cases. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-04** | Run `pwsh -NoProfile -File scripts/measure-apex-m1.ps1 -SampleSeconds 10` after 60s steady-state visible idle, and repeat after 60s minimized to system tray; verify Intel integrated GPU usage in Task Manager. | Empirically measure against performance targets: working set **≤ 120 MB**, idle CPU **< 1.0%**, Intel iGPU **~0% static / 0% minimized to tray**. See **GPU Counter Integrity Rule** below; log exact observed figures and remediate if any target is exceeded. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-05** | Verify `Documents` folder and existing OmniRoute installation/runtime before, during, and after running AERON. | Zero files written to `Documents` or OmniRoute directory; active OmniRoute service operates completely unaffected. | `PENDING OWNER APEX RUN` |

### GPU Counter Integrity Rule (`AT-M1-APEX-04`)

`scripts/measure-apex-m1.ps1` strictly separates a verified performance-counter measurement from unavailable counters:

- **Verified counter reading:** Reported only when valid per-process `\GPU Engine(pid_<PID>_*)\Utilization Percentage` PDH samples are returned—displayed as `0.000 % (measured across N GPU engine counter instance(s))` for an actual zero sum, `< 0.001 % (measured across ...)` for sub-millipoint non-zero activity, or `<value> % (measured across ...)` for higher activity.
- **Unavailable counters:** When the `GPU Engine` counter set or per-process instances are absent, inaccessible, localized, or return no valid PDH samples, the script reports `N/A — verify manually in Task Manager`. This output is **not** a verified `0%` measurement; open Windows 11 Task Manager, enable the **GPU** and **GPU Engine** columns for `Aeron.Desktop.exe`, and record the manually observed GPU percentage (noting that PDH counters reported `N/A`).
