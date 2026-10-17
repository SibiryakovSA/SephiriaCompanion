$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$size = 192
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$graphics.ScaleTransform($size / 108.0, $size / 108.0)
function Fill-Rect([string]$color, [float]$x, [float]$y, [float]$width, [float]$height) {
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($color))
    try { $graphics.FillRectangle($brush, $x, $y, $width, $height) } finally { $brush.Dispose() }
}
try {
    Fill-Rect '#272234' 0 0 108 108
    Fill-Rect '#D5AD6B' 19 13 70 82
    Fill-Rect '#272234' 23 17 62 74
    Fill-Rect '#49405A' 27 21 54 29
    Fill-Rect '#49405A' 27 58 54 29
    Fill-Rect '#D5AD6B' 23 52 62 4
    Fill-Rect '#A9D5BA' 35 27 13 13
    Fill-Rect '#A9D5BA' 58 33 13 13
    Fill-Rect '#A9D5BA' 45 35 16 4
    Fill-Rect '#F0DEB1' 46 61 16 5
    Fill-Rect '#F0DEB1' 49 65 10 5
    Fill-Rect '#F0DEB1' 43 69 22 13
    Fill-Rect '#A9D5BA' 46 75 16 5
    $destination = Join-Path (Split-Path -Parent $PSScriptRoot) 'android\res\drawable-nodpi'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $bitmap.Save((Join-Path $destination 'ic_launcher_legacy.png'), [System.Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $bitmap.Dispose() }
