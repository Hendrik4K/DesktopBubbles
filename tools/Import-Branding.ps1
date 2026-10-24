param(
    [string]$IconPath,
    [Parameter(Mandatory)][string]$PngPath
)
$ErrorActionPreference = 'Stop'
$pngSource = (Resolve-Path -LiteralPath $PngPath).Path
$pngBytes = [IO.File]::ReadAllBytes($pngSource)
if ($pngBytes.Length -lt 8 -or [BitConverter]::ToString($pngBytes, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') { throw 'Keine gültige PNG-Kopfzeile.' }
$assetRoot = Join-Path $PSScriptRoot '..\src\DesktopBubbles\Assets'
if ($IconPath) {
    $iconSource = (Resolve-Path -LiteralPath $IconPath).Path
    $iconBytes = [IO.File]::ReadAllBytes($iconSource)
    if ($iconBytes.Length -lt 22 -or [BitConverter]::ToUInt16($iconBytes, 0) -ne 0 -or [BitConverter]::ToUInt16($iconBytes, 2) -ne 1 -or [BitConverter]::ToUInt16($iconBytes, 4) -eq 0) { throw 'Keine gültige ICO-Kopfzeile.' }
} else {
    # Format conversion only: preserve the supplied artwork and alpha, fit each square frame.
    Add-Type -AssemblyName System.Drawing
    $source = [System.Drawing.Image]::FromFile($pngSource)
    $frames = @()
    try {
        foreach ($size in @(256, 128, 64, 48, 40, 32, 24, 20, 16)) {
            $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $stream = [IO.MemoryStream]::new()
            $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $scale = [Math]::Min($size / $source.Width, $size / $source.Height)
                $width = [Math]::Max(1, [int][Math]::Round($source.Width * $scale))
                $height = [Math]::Max(1, [int][Math]::Round($source.Height * $scale))
                $rect = [System.Drawing.Rectangle]::new([int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height)
                $graphics.DrawImage($source, $rect, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $frames += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
            } finally { $attributes.Dispose(); $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $source.Dispose() }
    $output = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $writer.Write([byte]($frame.Size % 256)); $writer.Write([byte]($frame.Size % 256))
            $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
        $writer.Flush(); $iconBytes = $output.ToArray()
    } finally { $writer.Dispose(); $output.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $assetRoot 'DesktopBubbles.ico'), $iconBytes)
[IO.File]::WriteAllBytes((Join-Path $assetRoot 'DesktopBubbles.png'), $pngBytes)
Write-Output 'Branding importiert; PNG unverändert, ICO übernommen oder in neun Auflösungen konvertiert. Danach Build.ps1 ausführen.'
