# Milestone 1 — Part B Interactive Windows Verification (`APEX`)

> Copyright (c) 2026 Avenox Studio. All Rights Reserved.

Automated CI on headless `windows-latest` runners **cannot** prove visible system tray rendering, foreground window restoration on a live desktop, or actual hardware resource behavior. Those criteria must be verified interactively on the Project Owner's Windows workstation (`APEX`: Windows 11, Intel Core i5 13th Gen, 16 GB RAM, Intel Integrated Graphics; minimum supported OS: **Windows 11 Version 25H2, Build 26200+, `x64`**).

---

## Preparation

Build and launch the published native executable from a local checkout (or download the `aeron-desktop-win-x64` CI artifact):

```powershell
dotnet publish src/Aeron.Desktop/Aeron.Desktop.csproj -c Release -r win-x64 --self-contained false -warnaserror -o artifacts/publish/win-x64
Start-Process -FilePath ".\artifacts\publish\win-x64\Aeron.Desktop.exe"
```

---

## Interactive Verification Checklist

| ID | Test Procedure on `APEX` | Expected Result | Measured / Observed Result |
|---|---|---|---|
| **AT-M1-APEX-01** | Launch `Aeron.Desktop.exe` on Windows 11 25H2+. Inspect rendering, DPI clarity, OS baseline banner, and `%LOCALAPPDATA%\Aeron\{config,data,logs}`. | Crisp native dark window opens with zero console/browser window or firewall prompt; OS banner reports compliant (`Build >= 26200`); `aeron.config.json`, `aeron_core.db`, and `aeron.jsonl` exist under `%LOCALAPPDATA%\Aeron`. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-02** | Click window close (`X`), verify icon in Windows 11 system tray/overflow + tooltip, right-click menu (*Open AERON*, *Minimize to Tray*, *Exit AERON*), double-click restore, then click *Exit AERON*. | Closing (`X`) hides window from taskbar and keeps AERON running in tray; double-click and *Open AERON* restore window; *Exit AERON* removes tray icon immediately with zero ghost icon and zero lingering `Aeron.Desktop` process in Task Manager. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-03** | While AERON is (a) minimized to taskbar and (b) hidden in the system tray, execute `.\artifacts\publish\win-x64\Aeron.Desktop.exe` a second time. | Second process exits immediately (`ExitCode 0`) and the existing AERON window restores visibly to the foreground in both cases. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-04** | Run `pwsh -NoProfile -File scripts/measure-apex-m1.ps1` after 60s steady-state visible idle, and repeat after 60s minimized to system tray; cross-check Intel iGPU column in Task Manager. | Empirically measure against performance targets: working set **≤ 120 MB**, idle CPU **< 1.0%**, Intel iGPU **~0% static / 0% minimized to tray**. Log actual numbers; remediate if any target is exceeded. | `PENDING OWNER APEX RUN` |
| **AT-M1-APEX-05** | Verify `Documents` folder and existing OmniRoute installation/runtime before, during, and after running AERON. | Zero files written to `Documents` or OmniRoute directory; active OmniRoute service operates completely unaffected. | `PENDING OWNER APEX RUN` |
