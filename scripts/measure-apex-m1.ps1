# AERON Milestone 1 — Read-Only Interactive Resource Measurement Helper (APEX)
# Copyright (c) 2026 Avenox Studio. All Rights Reserved.
#
# Usage on APEX (Windows 11 25H2+):
#   1. Launch Release build of Aeron.Desktop.exe and wait 60 seconds with no input.
#   2. Run: pwsh -NoProfile -File scripts/measure-apex-m1.ps1 -SampleSeconds 10
#   3. Minimize AERON to the system tray, wait 60 seconds, and run again.

param(
    [ValidateRange(3, 60)]
    [int]$SampleSeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$proc = Get-Process -Name 'Aeron.Desktop' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $proc) {
    Write-Error "Aeron.Desktop is not currently running. Launch the Release win-x64 executable first."
    exit 1
}

$osBuild = [System.Environment]::OSVersion.Version.Build
$logicalProcessors = [System.Environment]::ProcessorCount

$cpuStart = $proc.TotalProcessorTime.TotalSeconds
$sw = [System.Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds $SampleSeconds
$sw.Stop()

$proc.Refresh()
$cpuEnd = $proc.TotalProcessorTime.TotalSeconds

$cpuPercent = [Math]::Round((($cpuEnd - $cpuStart) / ($sw.Elapsed.TotalSeconds * $logicalProcessors)) * 100.0, 3)
$workingSetMb = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
$privateMb = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)

$gpuPercentText = 'Inspect in Task Manager (GPU counter unavailable)'
try {
    $counterPath = "\GPU Engine(pid_$($proc.Id)_*)\Utilization Percentage"
    $sample = Get-Counter -Counter $counterPath -ErrorAction Stop
    $totalGpu = ($sample.CounterSamples | Measure-Object -Property CookedValue -Sum).Sum
    $gpuPercentText = "$([Math]::Round($totalGpu, 3)) %"
}
catch {
    # Zero active GPU engine instances typically reports no counter instances (~0% utilization).
    $gpuPercentText = '0.000 % (no active GPU engine load; confirm in Task Manager)'
}

$wsStatus = if ($workingSetMb -le 120.0) { 'PASS (target <= 120 MB)' } else { 'EXCEEDS TARGET (> 120 MB)' }
$cpuStatus = if ($cpuPercent -lt 1.0) { 'PASS (target < 1.0%)' } else { 'EXCEEDS TARGET (>= 1.0%)' }
$osStatus = if ($osBuild -ge 26200) { 'COMPLIANT (Windows 11 25H2+)' } else { "BELOW MINIMUM ($osBuild < 26200)" }

[PSCustomObject]@{
    CapturedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    ProcessId           = $proc.Id
    WindowsBuild        = "$osBuild — $osStatus"
    SampleWindowSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
    WorkingSetMB        = "$workingSetMb MB — $wsStatus"
    PrivateMemoryMB     = "$privateMb MB"
    IdleCpuPercent      = "$cpuPercent % — $cpuStatus"
    IntegratedGpuUsage  = $gpuPercentText
    ThreadCount         = $proc.Threads.Count
    HandleCount         = $proc.HandleCount
} | Format-List
