param(
    [switch]$InstallSdk,
    [switch]$FrameworkDependent,
    [switch]$NoZip,
    [string]$OutputName = 'DesktopBubbles-win-x64'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.tools\nuget-cache'
# Keep SDK caches and configuration entirely inside this checkout.
$env:APPDATA = Join-Path $PSScriptRoot '.tools\appdata'
$env:LOCALAPPDATA = Join-Path $PSScriptRoot '.tools\localappdata'
$sdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if ($InstallSdk -and !(Test-Path -LiteralPath $sdk)) {
    New-Item -ItemType Directory -Path '.tools' -Force | Out-Null
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile '.tools\dotnet-install.ps1'
    & '.\.tools\dotnet-install.ps1' -Version '8.0.426' -InstallDir "$PSScriptRoot\.tools\dotnet" -NoPath
}
if (!(Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
& $sdk --version
if ($LASTEXITCODE -ne 0) { throw 'Kein passendes .NET-8-SDK. Erneut mit -InstallSdk ausführen.' }
& $sdk build DesktopBubbles.sln -c Release --nologo -m:1 -nr:false -p:UseSharedCompilation=false "-p:RestoreConfigFile=$PSScriptRoot\NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
& $sdk 'tests\DesktopBubbles.Tests\bin\Release\net8.0\DesktopBubbles.Tests.dll' "$PSScriptRoot\artifacts" | Tee-Object -FilePath 'artifacts\core-tests.txt'
if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
$portableOutput = Join-Path $PSScriptRoot ('artifacts\' + $OutputName + $(if ($FrameworkDependent) { '-runtime-required' } else { '' }))
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& $sdk publish 'src\DesktopBubbles\DesktopBubbles.csproj' -c Release -r win-x64 --self-contained $selfContained -o $portableOutput --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:DebugType=None -p:PublishSingleFile=false "-p:RestoreConfigFile=$PSScriptRoot\NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Portable Veröffentlichung fehlgeschlagen.' }
# Include notices from exactly the runtime packages listed in this self-contained build.
$runtimeConfig = Get-Content -LiteralPath (Join-Path $portableOutput 'DesktopBubbles.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packageId = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $runtimePackage = Join-Path $env:NUGET_PACKAGES ($packageId + '\' + $framework.version)
    $licenseRoot = Join-Path $portableOutput 'licenses'
    New-Item -ItemType Directory -Path $licenseRoot -Force | Out-Null
    $foundLicense = $false
    foreach ($noticeName in @('LICENSE.TXT', 'LICENSE', 'THIRD-PARTY-NOTICES.TXT')) {
        $noticeSource = Join-Path $runtimePackage $noticeName
        if (Test-Path -LiteralPath $noticeSource) {
            Copy-Item -LiteralPath $noticeSource -Destination (Join-Path $licenseRoot ($framework.name + '-' + $noticeName))
            if ($noticeName.StartsWith('LICENSE')) { $foundLicense = $true }
        }
    }
    if (!$foundLicense) { throw "Runtime-Lizenzhinweise fehlen: $runtimePackage" }
}
Set-Content -LiteralPath (Join-Path $portableOutput 'portable.flag') -Value 'Store settings in the adjacent data folder.' -Encoding utf8
Copy-Item -LiteralPath 'README.md' -Destination $portableOutput
foreach ($projectNotice in @('LICENSE', 'CHANGELOG.md')) {
    if (Test-Path -LiteralPath $projectNotice) { Copy-Item -LiteralPath $projectNotice -Destination $portableOutput }
}
Copy-Item -LiteralPath 'design-qa.md' -Destination $portableOutput
Copy-Item -LiteralPath 'docs' -Destination $portableOutput -Recurse -Force
if (!$NoZip) {
    & (Join-Path $PSScriptRoot 'tools\Package.ps1') -PortableOutput $portableOutput
}
Write-Output "Starten: $(Join-Path $portableOutput 'DesktopBubbles.exe')"
