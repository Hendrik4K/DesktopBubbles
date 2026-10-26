param([string]$Destination = (Join-Path $PSScriptRoot '..\.tools\inno'))
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Destination)
$compiler = Join-Path $target 'ISCC.exe'
$compilerHash = '0A8757031B33777E4C9CBFFEE40F11A5062B36D25CBE144C1DB73B6102B80AD7'
if ((Test-Path -LiteralPath $compiler) -and (Get-FileHash -LiteralPath $compiler).Hash -eq $compilerHash) {
    Write-Output $compiler
    return
}
$downloadDir = Join-Path (Split-Path $target -Parent) 'inno-download'
New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
$package = Join-Path $downloadDir 'innosetup-6.7.3.exe'
$packageHash = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'
if (!(Test-Path -LiteralPath $package) -or (Get-FileHash -LiteralPath $package).Hash -ne $packageHash) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $package
}
if ((Get-FileHash -LiteralPath $package).Hash -ne $packageHash) { throw 'Inno-Setup-Download: SHA-256 stimmt nicht.' }
$signature = Get-AuthenticodeSignature -LiteralPath $package
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B\.V\.') {
    throw 'Die Inno-Setup-Herausgebersignatur ist nicht gültig.'
}
$process = Start-Process -FilePath $package -ArgumentList @('/PORTABLE=1','/VERYSILENT','/SUPPRESSMSGBOXES',
    '/NORESTART','/NOICONS','/CURRENTUSER',('/DIR="'+$target+'"'),'/TASKS=') -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(60000) -or $process.ExitCode -ne 0) { throw 'Der portable Compiler konnte nicht bereitgestellt werden.' }
if (!(Test-Path -LiteralPath $compiler) -or (Get-FileHash -LiteralPath $compiler).Hash -ne $compilerHash) {
    throw 'Der bereitgestellte Compiler entspricht nicht Inno Setup 6.7.3.'
}
Write-Output $compiler
