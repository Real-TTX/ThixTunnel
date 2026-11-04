<#
.SYNOPSIS
    Renders the ThixTunnel icon in all detail levels and writes the Windows .ico plus PNG files.

.DESCRIPTION
    Same geometry as branding/logo-*.svg (256 grid):
      16-32 px  : small design (logo-small.svg) - thick portal, one node
      48+ px    : full design (logo-icon.svg)   - portal with a route of three hops
    Output: src/ThixTunnel/Assets/thixtunnel.ico and branding/png/thixtunnel-<size>.png
    Runs in Windows PowerShell 5.1 or PowerShell 7 on Windows (System.Drawing).
#>
param(
    [string] $Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Color([string] $hex, [int] $alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}

function New-RoundedRectangle([float] $x, [float] $y, [float] $w, [float] $h, [float] $r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Portal([float] $left, [float] $right, [float] $top, [float] $bottom) {
    # Two legs and a half circle: M left bottom V top A r r 0 0 1 right top V bottom
    $radius = ($right - $left) / 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddLine($left, $bottom, $left, $top)
    $path.AddArc($left, $top - $radius, 2 * $radius, 2 * $radius, 180, 180)
    $path.AddLine($right, $top, $right, $bottom)
    return $path
}

function Fill-Circle($g, $brush, [float] $cx, [float] $cy, [float] $r) {
    $g.FillEllipse($brush, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
}

function Draw-Icon([int] $size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    $round = [System.Drawing.Drawing2D.LineCap]::Round

    if ($size -le 32) {
        $tile = New-RoundedRectangle 0 0 256 256 60
        $g.FillPath((New-Object System.Drawing.SolidBrush (New-Color '#16294A')), $tile)
        $pen = New-Object System.Drawing.Pen (New-Color '#4DA3FF'), 40
        $pen.StartCap = $round; $pen.EndCap = $round
        $g.DrawPath($pen, (New-Portal 52 204 124 216))
        Fill-Circle $g (New-Object System.Drawing.SolidBrush (New-Color '#FFFFFF')) 128 160 30
    }
    else {
        $tile = New-RoundedRectangle 16 16 224 224 56
        $tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 16, 16), (New-Object System.Drawing.PointF 240, 240), (New-Color '#1F3E6B'), (New-Color '#0D1828')
        $g.FillPath($tileBrush, $tile)

        $archBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 49, 0), (New-Object System.Drawing.PointF 207, 0), (New-Color '#3B8CF0'), (New-Color '#6CB8FF')
        $pen = New-Object System.Drawing.Pen $archBrush, 22
        $pen.StartCap = $round; $pen.EndCap = $round
        $g.DrawPath($pen, (New-Portal 60 196 124 200))

        $routePen = New-Object System.Drawing.Pen (New-Color '#FFFFFF' 217), 8
        $routePen.StartCap = $round; $routePen.EndCap = $round
        $routePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = [System.Drawing.PointF[]] @(
            (New-Object System.Drawing.PointF 96, 178),
            (New-Object System.Drawing.PointF 128, 138),
            (New-Object System.Drawing.PointF 160, 178))
        $g.DrawLines($routePen, $points)

        $white = New-Object System.Drawing.SolidBrush (New-Color '#FFFFFF')
        Fill-Circle $g $white 96 178 13
        Fill-Circle $g $white 128 138 13
        Fill-Circle $g (New-Object System.Drawing.SolidBrush (New-Color '#4ADE80')) 160 178 13
    }

    $g.Dispose()
    return $bitmap
}

$pngDirectory = Join-Path $Root 'branding\png'
$icoPath = Join-Path $Root 'src\ThixTunnel\Assets\thixtunnel.ico'
New-Item -ItemType Directory -Force $pngDirectory | Out-Null
New-Item -ItemType Directory -Force (Split-Path $icoPath) | Out-Null

$icoSizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($size in $icoSizes) {
    $bitmap = Draw-Icon $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += , @($size, $stream.ToArray())
    $bitmap.Dispose()
}

foreach ($size in 16, 32, 48, 256, 512) {
    $bitmap = Draw-Icon $size
    $bitmap.Save((Join-Path $pngDirectory "thixtunnel-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

# ICO with PNG entries (Windows Vista and later).
$file = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([UInt16] 0)
$writer.Write([UInt16] 1)
$writer.Write([UInt16] $images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $size = $image[0]; $bytes = $image[1]
    $dimension = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([Byte] $dimension)
    $writer.Write([Byte] $dimension)
    $writer.Write([Byte] 0)
    $writer.Write([Byte] 0)
    $writer.Write([UInt16] 1)
    $writer.Write([UInt16] 32)
    $writer.Write([UInt32] $bytes.Length)
    $writer.Write([UInt32] $offset)
    $offset += $bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]] $image[1]) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $file.ToArray())

Write-Host "Icon written: $icoPath"
Write-Host "PNGs written: $pngDirectory"
