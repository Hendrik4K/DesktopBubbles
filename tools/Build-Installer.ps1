param(
    [Parameter(Mandatory)][string]$Payload,
    [string]$Compiler = (Join-Path $PSScriptRoot '..\.tools\inno\ISCC.exe'),
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\installer'),
    [string]$FixtureRoot
)
$ErrorActionPreference = 'Stop'
$payloadRoot = (Resolve-Path -LiteralPath $Payload).Path.TrimEnd('\')
$compilerPath = (Resolve-Path -LiteralPath $Compiler).Path
$outputRoot = [IO.Path]::GetFullPath($Output)
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exePath = Join-Path $payloadRoot 'DesktopBubbles.exe'
$dllPath = Join-Path $payloadRoot 'DesktopBubbles.dll'
if (!(Test-Path -LiteralPath $exePath) -or !(Test-Path -LiteralPath $dllPath) -or !(Test-Path -LiteralPath (Join-Path $payloadRoot 'portable.flag'))) {
    throw 'Ein vollständiger portabler Build mit portable.flag ist erforderlich.'
}
if (Test-Path -LiteralPath (Join-Path $payloadRoot 'data')) { throw 'Der Installer darf keine Benutzerdaten enthalten. Einen frischen Build verwenden.' }
$files = @(Get-ChildItem -LiteralPath $payloadRoot -Recurse -File)
if ((Get-Item -LiteralPath $payloadRoot).Attributes -band [IO.FileAttributes]::ReparsePoint -or
    (Get-ChildItem -LiteralPath $payloadRoot -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) -or
    ($files | Where-Object { $_.Name -like 'groups.json*' -or $_.Extension -in @('.lnk','.url','.bubbles','.log') })) {
    throw 'Der Payload enthält Verknüpfungen, Nutzerdaten, Logs oder Dateisystem-Links.'
}
$version = (Get-Item -LiteralPath $dllPath).VersionInfo.ProductVersion.Split('+')[0]
if ($version -notmatch '^\d+\.\d+\.\d+([-\.a-zA-Z0-9]*)$') { throw 'Ungültige Produktversion.' }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$scriptPath = Join-Path $PSScriptRoot 'installer\DesktopBubbles.iss'
function Compile-Setup([bool]$preview, [bool]$fixture) {
    $name = 'DesktopBubbles-Setup-' + $version
    if ($preview) { $name += '-Preview' }
    if ($fixture) { $name += '-Fixture' }
    $compileOutput = if ($fixture) { Join-Path $outputRoot 'tests' } else { $outputRoot }
    New-Item -ItemType Directory -Path $compileOutput -Force | Out-Null
    $parameters = @('/Qp', ('/DAppVersion=' + $version), ('/DPayloadDir=' + $payloadRoot),
        ('/DOutputPath=' + $compileOutput), ('/DOutputName=' + $name), ('/DPreviewOnly=' + [int]$preview), ('/DTestBuild=' + [int]$fixture))
    if ($fixture) {
        $fixturePath = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\')
        if (!$fixturePath.StartsWith($projectRoot + '\artifacts\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Installer-Fixtures müssen innerhalb des Projektordners artifacts liegen.'
        }
        $parameters += '/DTestRoot=' + $fixturePath
    }
    & $compilerPath @parameters $scriptPath | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "Installer-Kompilierung fehlgeschlagen: $name" }
    $built = Join-Path $compileOutput ($name + '.exe')
    (Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash | Set-Content -LiteralPath ($built + '.sha256')
    return $built
}
$setup = Compile-Setup $false $false
$preview = Compile-Setup $true $false
$fixture = if ($FixtureRoot) { Compile-Setup $false $true } else { $null }
[ordered]@{Version=$version;Setup=$setup;Preview=$preview;Fixture=$fixture;FixtureRoot=$FixtureRoot;Payload=$payloadRoot;
    PayloadFiles=$files.Count;PayloadBytes=($files | Measure-Object Length -Sum).Sum;
    PreviewContainsAppPayload=$false;Published=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'build.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $outputRoot 'build.json')
