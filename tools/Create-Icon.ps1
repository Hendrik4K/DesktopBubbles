$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconRoot = Join-Path $PSScriptRoot '..\src\DesktopBubbles\Assets'
New-Item -ItemType Directory -Path $iconRoot -Force | Out-Null
$images = @()
foreach ($size in @(16, 32, 48, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $colors = @('#1185A6', '#79CBD8', '#ADDDE7', '#30637D')
    for ($index = 0; $index -lt 4; $index++) {
        $x = $size * (0.08 + ($index % 2) * 0.47)
        $y = $size * (0.08 + [Math]::Floor($index / 2) * 0.47)
        $width = $size * 0.37
        $radius = $size * 0.12
        $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $shape.AddArc($x, $y, $radius, $radius, 180, 90)
        $shape.AddArc($x + $width - $radius, $y, $radius, $radius, 270, 90)
        $shape.AddArc($x + $width - $radius, $y + $width - $radius, $radius, $radius, 0, 90)
        $shape.AddArc($x, $y + $width - $radius, $radius, $radius, 90, 90)
        $shape.CloseFigure()
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($colors[$index]))
        $graphics.FillPath($brush, $shape)
        $brush.Dispose()
        $shape.Dispose()
    }
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@($size, $stream.ToArray())
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
$file = [System.IO.File]::Create((Join-Path $iconRoot 'DesktopBubbles.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $writer.Write([byte]($image[0] % 256)); $writer.Write([byte]($image[0] % 256))
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$image[1].Length); $writer.Write([uint32]$offset)
        $offset += $image[1].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image[1]) }
} finally { $writer.Dispose(); $file.Dispose() }
