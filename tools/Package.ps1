param([string]$PortableOutput = (Join-Path $PSScriptRoot '..\artifacts\DesktopBubbles-win-x64'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$portableRoot = [System.IO.Path]::GetFullPath($PortableOutput)
function Write-Zip([string]$destination, [string]$basePath, [System.IO.FileInfo[]]$files) {
    $tempZip = $destination + '.tmp'
    $stream = [System.IO.File]::Open($tempZip, [System.IO.FileMode]::Create)
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $files) {
            $relative = $item.FullName.Substring($basePath.TrimEnd('\').Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $item.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
    if ([System.IO.File]::Exists($destination)) { [System.IO.File]::Delete($destination) }
    [System.IO.File]::Move($tempZip, $destination)
    (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash | Set-Content -LiteralPath ($destination + '.sha256')
    Write-Output "ZIP: $destination"
}
# Never include an existing user's portable configuration in a release archive.
$portableFiles = Get-ChildItem -LiteralPath $portableRoot -Recurse -File | Where-Object {
    !$_.FullName.Substring($portableRoot.Length + 1).StartsWith('data\', [System.StringComparison]::OrdinalIgnoreCase)
}
Write-Zip ($portableRoot + '.zip') $portableRoot $portableFiles
$sourceFiles = @()
foreach ($folder in @('src', 'tests', 'tools', 'docs', '.github')) {
    $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $projectRoot $folder) -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
}
foreach ($file in @('DesktopBubbles.sln', 'Directory.Build.props', 'global.json', 'NuGet.Config', '.gitignore', 'Build.ps1', 'Test-Windows.ps1', 'README.md', 'design-qa.md')) {
    $sourceFiles += Get-Item -LiteralPath (Join-Path $projectRoot $file) -Force
}
foreach ($file in @('LICENSE', 'CHANGELOG.md')) {
    if (Test-Path -LiteralPath (Join-Path $projectRoot $file)) { $sourceFiles += Get-Item -LiteralPath (Join-Path $projectRoot $file) }
}
Write-Zip (Join-Path $projectRoot 'artifacts\DesktopBubbles-Source.zip') $projectRoot $sourceFiles
