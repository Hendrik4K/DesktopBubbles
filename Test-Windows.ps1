param([string]$Executable = "$PSScriptRoot\src\DesktopBubbles\bin\Release\net8.0-windows\DesktopBubbles.exe")
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Executable)) { throw 'Zuerst Build.ps1 ausführen.' }
$smokeData = Join-Path $PSScriptRoot ('artifacts\windows-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$process = Start-Process -FilePath $Executable -ArgumentList @('--smoke-test', '--data-dir', ('"' + $smokeData + '"')) -PassThru -WindowStyle Hidden
if (!$process.WaitForExit(120000)) { throw "Der Windows-Test benötigt länger als 120 Sekunden. Testprozess $($process.Id) und Log in $smokeData prüfen." }
if ($process.ExitCode -ne 0) { throw "Windows-Test fehlgeschlagen (Exit $($process.ExitCode)): $smokeData" }
Get-Content -LiteralPath (Join-Path $smokeData 'smoke-results.json')
Write-Output "Testbericht und UI-Bilder: $smokeData"
