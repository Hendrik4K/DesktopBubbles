param([string]$Build = (Join-Path $PSScriptRoot '..\artifacts\installer\build.json'))
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildInfo = Get-Content -LiteralPath $Build -Raw | ConvertFrom-Json
$root = [IO.Path]::GetFullPath($buildInfo.FixtureRoot).TrimEnd('\')
if (!$root.StartsWith($project + '\artifacts\', [StringComparison]::OrdinalIgnoreCase) -or
    $buildInfo.Fixture -notlike '*-Fixture.exe' -or $buildInfo.Preview -notlike '*-Preview.exe') { throw 'Keine gültige isolierte Installer-Fixture.' }
if (Test-Path -LiteralPath $root) { throw 'Für den Test einen frischen FixtureRoot beim Bauen wählen.' }
New-Item -ItemType Directory -Path $root | Out-Null
$checks = [Collections.Generic.List[object]]::new()
function Check([string]$name, [bool]$condition) {
    $checks.Add(@{Name=$name;Passed=$condition})
    if (!$condition) { throw "Installer-Test fehlgeschlagen: $name" }
    Write-Host "PASS $name"
}
function Run-Installer([string]$exe, [string]$name, [string[]]$extra = @()) {
    $log = Join-Path $root ($name + '.log')
    $process = Start-Process -FilePath $exe -ArgumentList (@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+$log+'"')) + $extra) -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { throw "Installer-Fixture läuft länger als erwartet: $($process.Id)" }
    return $process.ExitCode
}
function Tree-Hashes([string]$directory) {
    $result = @{}
    foreach ($f in @(Get-ChildItem -LiteralPath $directory -File -Recurse)) { $result[$f.FullName.Substring($directory.Length + 1)] = (Get-FileHash -LiteralPath $f.FullName).Hash }
    return $result
}
function Same-Tree($expected, [string]$directory) {
    if (!(Test-Path -LiteralPath $directory)) { return $false }
    $current = Tree-Hashes $directory
    if ($current.Count -ne $expected.Count) { return $false }
    foreach ($key in $expected.Keys) { if ($expected[$key] -ne $current[$key]) { return $false } }
    return $true
}
$realApp = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'Programs\DesktopBubbles'
$realState = Join-Path $realApp 'data\groups.json'
$stateBefore = if (Test-Path -LiteralPath $realState) { (Get-FileHash -LiteralPath $realState).Hash } else { '' }
$realExeBefore = if (Test-Path -LiteralPath (Join-Path $realApp 'DesktopBubbles.exe')) { (Get-FileHash -LiteralPath (Join-Path $realApp 'DesktopBubbles.exe')).Hash } else { '' }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupBefore = Get-ItemPropertyValue -LiteralPath $runKey -Name DesktopBubbles -ErrorAction SilentlyContinue
$reg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A47AA47E-3E32-4A89-A51B-407DFA0A0902}_is1'
$registrationBefore = Test-Path -LiteralPath $reg
try {
    $exit = Run-Installer $buildInfo.Preview 'preview'
    Check 'Vorschau stoppt vor der Installation, auch im unbeaufsichtigten Modus' ($exit -ne 0 -and (Get-Content (Join-Path $root 'preview.log') -Raw).Contains('PREVIEW_INSTALL_BLOCKED'))
    Check 'Vorschau enthält kein App-Paket' ((Get-Item -LiteralPath $buildInfo.Preview).Length -lt 8MB -and (Get-Item -LiteralPath $buildInfo.Setup).Length -gt 30MB)
    Check 'Vorschau registriert keine Deinstallation' ((Test-Path -LiteralPath $reg) -eq $registrationBefore)
    Check 'Frische Installation im isolierten Testordner' ((Run-Installer $buildInfo.Fixture 'install') -eq 0)
    $program = Join-Path $root 'Program'
    Check 'Installierte Programmdatei entspricht dem Payload' ((Get-FileHash (Join-Path $program 'DesktopBubbles.exe')).Hash -eq (Get-FileHash (Join-Path $buildInfo.Payload 'DesktopBubbles.exe')).Hash)
    $lnk = Join-Path $root 'StartMenu\DesktopBubbles.lnk'
    $ws = New-Object -ComObject WScript.Shell
    $shortcut = $ws.CreateShortcut($lnk)
    Check 'Startmenü-Verknüpfung verwendet den richtigen Programm- und Datenort' ((Test-Path -LiteralPath $lnk) -and $shortcut.TargetPath -eq (Join-Path $program 'DesktopBubbles.exe') -and $shortcut.Arguments -eq '' -and (Test-Path (Join-Path $program 'portable.flag')))
    $data = Join-Path $program 'data'
    New-Item -ItemType Directory -Path (Join-Path $data 'shortcuts\journal') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $data 'groups.json') -Value '{"Version":6,"Settings":{"BubbleColor":"#123456"},"Groups":[{"Id":"d30f7731-f8e9-4059-a916-3fa5d1c6f49a","Name":"Eigene Testgruppe","Items":[]}]}' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $data 'shortcuts\fixture.lnk') -Value 'synthetic shortcut bytes'
    Set-Content -LiteralPath (Join-Path $data 'shortcuts\journal\ownership.json') -Value '{"Source":"fixture-only"}'
    $expected = Tree-Hashes $data
    $programLock = [IO.File]::Open((Join-Path $program 'DesktopBubbles.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try { Check 'Gesperrte Programmdatei blockiert Update auch ohne Datenordner-Mutex' ((Run-Installer $buildInfo.Fixture 'program-locked') -ne 0) }
    finally { $programLock.Dispose() }
    Check 'Programmdatei-Sperre greift vor der Datensicherung' (!(Test-Path -LiteralPath (Join-Path $root 'Backups')))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($data.ToUpperInvariant()))).Substring(0,16)
    $mutex = [Threading.Mutex]::new($false, ('Local\DesktopBubbles-' + $hash))
    try { Check 'Laufende Ziel-App blockiert Update ohne Beenden zu erzwingen' ((Run-Installer $buildInfo.Fixture 'running-blocked') -ne 0) }
    finally { $mutex.Dispose() }
    Check 'Blockiertes Update verändert keine Gruppen oder Verknüpfungen' (Same-Tree $expected $data)
    Check 'Update mit vorhandenen Gruppen erfolgreich' ((Run-Installer $buildInfo.Fixture 'update') -eq 0)
    Check 'Update erhält vollständige Daten bytegenau' (Same-Tree $expected $data)
    $backup = Get-ChildItem -LiteralPath (Join-Path $root 'Backups') -Directory | Select-Object -First 1
    Check 'Geprüfte Sicherung enthält auch Verknüpfungen und Journale' ($null -ne $backup -and (Same-Tree $expected $backup.FullName))
    $locked = [IO.File]::Open((Join-Path $data 'groups.json'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try { Check 'Nicht lesbare Datensicherung verhindert Installation' ((Run-Installer $buildInfo.Fixture 'backup-blocked') -ne 0) }
    finally { $locked.Dispose() }
    Check 'Fehlgeschlagene Sicherung lässt Originaldaten unverändert' (Same-Tree $expected $data)
    Check 'Deinstallation der isolierten Testinstallation erfolgreich' ((Run-Installer (Join-Path $program 'unins000.exe') 'uninstall') -eq 0)
    Check 'Deinstallation entfernt das Programm und erhält persönliche Daten' (!(Test-Path (Join-Path $program 'DesktopBubbles.exe')) -and (Same-Tree $expected $data))
    Check 'Deinstallation erhält die externe Datensicherung' (Same-Tree $expected $backup.FullName)
    Check 'Echte Nutzergruppen und Programmdatei unverändert' (($stateBefore -eq '' -or (Get-FileHash -LiteralPath $realState).Hash -eq $stateBefore) -and ($realExeBefore -eq '' -or (Get-FileHash (Join-Path $realApp 'DesktopBubbles.exe')).Hash -eq $realExeBefore))
    Check 'Echter Autostart und Installationsregistrierung unverändert' ((Get-ItemPropertyValue -LiteralPath $runKey -Name DesktopBubbles -ErrorAction SilentlyContinue) -eq $startupBefore -and (Test-Path -LiteralPath $reg) -eq $registrationBefore)
}
finally {
    [ordered]@{Checks=$checks;FixtureRoot=$root;RealInstallChanged=$false;ProductionInstallExecuted=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding utf8
}
Write-Host "$($checks.Count) Installer-Prüfungen bestanden."
