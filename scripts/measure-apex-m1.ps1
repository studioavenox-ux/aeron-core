# AERON Milestone 1 — Read-Only Interactive Resource Measurement Helper (APEX)
# Copyright (c) 2026 Avenox Studio. All Rights Reserved.
#
# Prerequisite:
#   Framework-dependent Aeron.Desktop.exe requires the .NET 10 LTS Windows Desktop Runtime (x64)
#   (Microsoft.WindowsDesktop.App 10.0.x), included with the .NET 10 LTS SDK (x64).
#
# Usage on APEX (Windows 11 25H2+):
#   1. Launch Release win-x64 Aeron.Desktop.exe and wait 60 seconds with no input.
#   2. Run: pwsh -NoProfile -File scripts/measure-apex-m1.ps1 -SampleSeconds 10
#   3. Minimize AERON to the system tray, wait 60 seconds, and run again.
#   4. If IntegratedGpuUsage reports "N/A — verify manually in Task Manager",
#      do NOT record 0%; inspect and record the GPU column manually in Task Manager.

param(
    [ValidateRange(3, 60)]
    [int]$SampleSeconds = 10,

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:InvariantCulture = [System.Globalization.CultureInfo]::InvariantCulture
$script:GpuUnavailableText = "N/A $([char]0x2014) verify manually in Task Manager"
$script:EmDash = [string][char]0x2014

function Resolve-GpuUtilizationReading {
    param(
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]]$CounterSamples
    )

    if ($null -eq $CounterSamples) {
        return $script:GpuUnavailableText
    }

    $items = @($CounterSamples)
    if ($items.Count -eq 0) {
        return $script:GpuUnavailableText
    }

    $validValues = [System.Collections.Generic.List[double]]::new()

    foreach ($sample in $items) {
        if ($null -eq $sample) {
            continue
        }

        $statusProp = $sample.PSObject.Properties['Status']
        if ($null -ne $statusProp -and $null -ne $statusProp.Value) {
            $statusCode = [uint32]$statusProp.Value
            # PDH_CSTATUS_VALID_DATA (0) and PDH_CSTATUS_NEW_DATA (1) are the only valid PDH statuses.
            if ($statusCode -ne [uint32]0 -and $statusCode -ne [uint32]1) {
                continue
            }
        }

        $cookedProp = $sample.PSObject.Properties['CookedValue']
        if ($null -eq $cookedProp -or $null -eq $cookedProp.Value) {
            continue
        }

        $numericValue = 0.0
        try {
            $numericValue = [double]$cookedProp.Value
        }
        catch {
            continue
        }

        if ([double]::IsNaN($numericValue) -or [double]::IsInfinity($numericValue) -or $numericValue -lt 0.0) {
            continue
        }

        $validValues.Add($numericValue)
    }

    if ($validValues.Count -eq 0) {
        return $script:GpuUnavailableText
    }

    $totalGpu = 0.0
    foreach ($val in $validValues) {
        $totalGpu += $val
    }

    $instanceCount = $validValues.Count
    $rounded = [Math]::Round($totalGpu, 3)

    if ($totalGpu -eq 0.0) {
        return "0.000 % (measured across $instanceCount GPU engine counter instance(s))"
    }

    if ($rounded -eq 0.0) {
        return "< 0.001 % (measured across $instanceCount GPU engine counter instance(s))"
    }

    $formatted = $rounded.ToString('F3', $script:InvariantCulture)
    return "$formatted % (measured across $instanceCount GPU engine counter instance(s))"
}

function Get-ProcessGpuUtilization {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId,

        [scriptblock]$CounterReader
    )

    try {
        if ($null -ne $CounterReader) {
            $sampleSets = @(& $CounterReader $ProcessId)
        }
        else {
            $gpuCounterSet = Get-Counter -ListSet 'GPU Engine' -ErrorAction Stop
            if ($null -eq $gpuCounterSet) {
                return $script:GpuUnavailableText
            }

            $pathsProp = $gpuCounterSet.PSObject.Properties['PathsWithInstances']
            if ($null -eq $pathsProp -or $null -eq $pathsProp.Value) {
                return $script:GpuUnavailableText
            }

            $pidToken = "(pid_$($ProcessId)_"
            $matchingPaths = @(
                $pathsProp.Value | Where-Object {
                    $_ -is [string] -and
                    $_.IndexOf($pidToken, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
                    $_.EndsWith('\Utilization Percentage', [System.StringComparison]::OrdinalIgnoreCase)
                }
            )

            # Never fabricate 0.000% when no per-process GPU counter instances are exposed.
            if ($matchingPaths.Count -eq 0) {
                return $script:GpuUnavailableText
            }

            # Take two samples 1 second apart and read the second sample set for a genuine delta reading.
            $counterQuery = "\GPU Engine(pid_$($ProcessId)_*)\Utilization Percentage"
            $sampleSets = @(Get-Counter -Counter $counterQuery -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop)
        }

        if ($sampleSets.Count -eq 0) {
            return $script:GpuUnavailableText
        }

        $finalSet = $sampleSets[$sampleSets.Count - 1]
        if ($null -eq $finalSet) {
            return $script:GpuUnavailableText
        }

        $samplesProp = $finalSet.PSObject.Properties['CounterSamples']
        if ($null -eq $samplesProp -or $null -eq $samplesProp.Value) {
            return $script:GpuUnavailableText
        }

        return Resolve-GpuUtilizationReading -CounterSamples @($samplesProp.Value)
    }
    catch {
        # Unavailable, inaccessible, localized, or missing GPU counters must never be reported as 0%.
        return $script:GpuUnavailableText
    }
}

function Invoke-MeasurementSelfTest {
    $expectedUnavailable = "N/A — verify manually in Task Manager"
    if ($script:GpuUnavailableText -cne $expectedUnavailable) {
        throw "Unavailable sentinel mismatch: got '$($script:GpuUnavailableText)'."
    }

    # 1. Null and empty counter collections must report unavailable (never 0%).
    $nullResult = Resolve-GpuUtilizationReading -CounterSamples $null
    if ($nullResult -cne $expectedUnavailable) {
        throw "Expected unavailable for null samples, got '$nullResult'."
    }

    $emptyResult = Resolve-GpuUtilizationReading -CounterSamples @()
    if ($emptyResult -cne $expectedUnavailable) {
        throw "Expected unavailable for empty samples, got '$emptyResult'."
    }

    # 2. Invalid PDH status codes (e.g., PDH_INVALID_DATA 0xC0000BC6) and NaN must report unavailable.
    $invalidSamples = @(
        [PSCustomObject]@{ Status = [uint32]0xC0000BC6; CookedValue = 0.0 },
        [PSCustomObject]@{ Status = [uint32]0; CookedValue = [double]::NaN }
    )
    $invalidResult = Resolve-GpuUtilizationReading -CounterSamples $invalidSamples
    if ($invalidResult -cne $expectedUnavailable) {
        throw "Expected unavailable for invalid PDH samples, got '$invalidResult'."
    }

    # 3. Counter reader exception must report unavailable (never 0%).
    $throwingResult = Get-ProcessGpuUtilization -ProcessId 1234 -CounterReader {
        param($TargetPid)
        throw "Simulated unavailable GPU performance counter for PID $TargetPid"
    }
    if ($throwingResult -cne $expectedUnavailable) {
        throw "Expected unavailable when Get-Counter throws, got '$throwingResult'."
    }

    # 4. Live lookup against a non-existent PID (or host without GPU counters) must report unavailable.
    $liveMissingResult = Get-ProcessGpuUtilization -ProcessId ([int]::MaxValue)
    if ($liveMissingResult -cne $expectedUnavailable) {
        throw "Expected unavailable for unexposed/unavailable live GPU counter, got '$liveMissingResult'."
    }

    # 5. Valid PDH samples equal to 0.0 must report a verified measured zero reading.
    $zeroSamples = @(
        [PSCustomObject]@{ Status = [uint32]0; CookedValue = 0.0 },
        [PSCustomObject]@{ Status = [uint32]1; CookedValue = 0.0 }
    )
    $zeroResult = Resolve-GpuUtilizationReading -CounterSamples $zeroSamples
    if ($zeroResult -cne '0.000 % (measured across 2 GPU engine counter instance(s))') {
        throw "Unexpected measured-zero output: '$zeroResult'."
    }

    # 6. Small non-zero values below 0.0005 must not be mislabeled as exact 0.000%.
    $subMilliSamples = @(
        [PSCustomObject]@{ Status = [uint32]0; CookedValue = 0.0002 }
    )
    $subMilliResult = Resolve-GpuUtilizationReading -CounterSamples $subMilliSamples
    if ($subMilliResult -cne '< 0.001 % (measured across 1 GPU engine counter instance(s))') {
        throw "Unexpected sub-millipoint GPU output: '$subMilliResult'."
    }

    # 7. Positive valid PDH samples must sum accurately using the final delta sample set.
    $positiveResult = Get-ProcessGpuUtilization -ProcessId 1234 -CounterReader {
        @(
            [PSCustomObject]@{
                CounterSamples = @(
                    [PSCustomObject]@{ Status = [uint32]0; CookedValue = 0.0 }
                )
            },
            [PSCustomObject]@{
                CounterSamples = @(
                    [PSCustomObject]@{ Status = [uint32]0; CookedValue = 0.125 },
                    [PSCustomObject]@{ Status = [uint32]0; CookedValue = 0.25 }
                )
            }
        )
    }
    if ($positiveResult -cne '0.375 % (measured across 2 GPU engine counter instance(s))') {
        throw "Unexpected positive GPU output: '$positiveResult'."
    }

    Write-Host "measure-apex-m1.ps1 self-test PASSED (measured zero, non-zero, and unavailable counter paths verified)."
}

if ($SelfTest) {
    Invoke-MeasurementSelfTest
    exit 0
}

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
if ($proc.HasExited) {
    Write-Error "Aeron.Desktop (PID $($proc.Id)) exited during the sampling window."
    exit 1
}

$cpuEnd = $proc.TotalProcessorTime.TotalSeconds

$cpuPercent = [Math]::Round((($cpuEnd - $cpuStart) / ($sw.Elapsed.TotalSeconds * $logicalProcessors)) * 100.0, 3)
$workingSetMb = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
$privateMb = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
$sampleWindowText = [Math]::Round($sw.Elapsed.TotalSeconds, 1).ToString('F1', $script:InvariantCulture)
$workingSetText = $workingSetMb.ToString('F2', $script:InvariantCulture)
$privateMbText = $privateMb.ToString('F2', $script:InvariantCulture)
$cpuPercentText = $cpuPercent.ToString('F3', $script:InvariantCulture)

$gpuUsageText = Get-ProcessGpuUtilization -ProcessId $proc.Id

$wsStatus = if ($workingSetMb -le 120.0) { 'PASS (target <= 120 MB)' } else { 'EXCEEDS TARGET (> 120 MB)' }
$cpuStatus = if ($cpuPercent -lt 1.0) { 'PASS (target < 1.0%)' } else { 'EXCEEDS TARGET (>= 1.0%)' }
$osStatus = if ($osBuild -ge 26200) { 'COMPLIANT (Windows 11 25H2+)' } else { "BELOW MINIMUM ($osBuild < 26200)" }

[PSCustomObject]@{
    CapturedAtUtc       = (Get-Date).ToUniversalTime().ToString('o', $script:InvariantCulture)
    ProcessId           = $proc.Id
    WindowsBuild        = "$osBuild $($script:EmDash) $osStatus"
    SampleWindowSeconds = $sampleWindowText
    WorkingSetMB        = "$workingSetText MB $($script:EmDash) $wsStatus"
    PrivateMemoryMB     = "$privateMbText MB"
    IdleCpuPercent      = "$cpuPercentText % $($script:EmDash) $cpuStatus"
    IntegratedGpuUsage  = $gpuUsageText
    ThreadCount         = $proc.Threads.Count
    HandleCount         = $proc.HandleCount
} | Format-List
