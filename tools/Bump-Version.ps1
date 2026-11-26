# Sets the DesktopBubbles version to the release date: YY.MM.DD (26.12.01 = 1 December 2026).
# Year first, so every update check (Version.Parse, GitHub tag, installer) sees newer as higher.
# A second release on the same day gets a fourth number (26.12.01.2, .3, ...).
# <Version> keeps the plain number form (26.12.1) because .NET rejects leading zeros there;
# <InformationalVersion> carries the padded date, which the app, the installer and the tag show.
param([datetime]$Date = (Get-Date), [string]$Project = '')
$ErrorActionPreference = 'Stop'
if (-not $Project) { $Project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\DesktopBubbles\DesktopBubbles.csproj' }
$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [IO.File]::ReadAllText($Project, $utf8)
if ($text -notmatch '<Version>([^<]+)</Version>') { throw "Keine <Version> in $Project gefunden." }
$current = [version]$Matches[1]

$year = $Date.Year % 100
$day = [version]::new($year, $Date.Month, $Date.Day)
if ($current.Major -eq $day.Major -and $current.Minor -eq $day.Minor -and $current.Build -eq $day.Build) {
    $next = [version]::new($year, $Date.Month, $Date.Day, [math]::Max($current.Revision, 1) + 1)
} elseif ($current -gt $day) {
    throw "Die aktuelle Version $current ist neuer als das Datum $($Date.ToString('dd.MM.yyyy'))."
} else {
    $next = $day
}
$number = if ($next.Revision -gt 0) { "$($next.Major).$($next.Minor).$($next.Build).$($next.Revision)" } else { "$($next.Major).$($next.Minor).$($next.Build)" }
$display = '{0}.{1:00}.{2:00}' -f $next.Major, $next.Minor, $next.Build
if ($next.Revision -gt 0) { $display += ".$($next.Revision)" }

$text = $text -replace '<Version>[^<]+</Version>', "<Version>$number</Version>"
if ($text -match '<InformationalVersion>') {
    $text = $text -replace '<InformationalVersion>[^<]*</InformationalVersion>', "<InformationalVersion>$display</InformationalVersion>"
} else {
    $text = $text -replace '(\s*)(<Version>[^<]+</Version>)', "`$1`$2`$1<InformationalVersion>$display</InformationalVersion>"
}
[IO.File]::WriteAllText($Project, $text, $utf8)
Write-Host "DesktopBubbles $display (Tag v$display)" -ForegroundColor Cyan
$display
