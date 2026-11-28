# Просмотр кадров .ico: собирает контрольный лист (реальные кадры, увеличение 4x и вид на тёмной панели).
# Нужен, чтобы проверять иконки глазами, не запуская приложение:
#   .\tools\show-icon.ps1 -Ico res\app.ico -Out build\icon-full.png
param(
    [string]$Ico = 'res\app.ico',
    [string]$Out = 'build\icon-preview.png'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Read-IcoFrames([string]$path) {
    $b = [IO.File]::ReadAllBytes($path)
    $count = [BitConverter]::ToUInt16($b, 4)
    $frames = @()
    for ($i = 0; $i -lt $count; $i++) {
        $o = 6 + 16 * $i
        $w = [int]$b[$o]; if ($w -eq 0) { $w = 256 }
        $size = [BitConverter]::ToInt32($b, $o + 8)
        $off = [BitConverter]::ToInt32($b, $o + 12)
        $bmp = New-Object System.Drawing.Bitmap($w, $w, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt $w; $y++) {
            for ($x = 0; $x -lt $w; $x++) {
                $p = $off + 40 + (($w - 1 - $y) * $w + $x) * 4
                $bb = $b[$p]; $gg = $b[$p + 1]; $rr = $b[$p + 2]; $aa = $b[$p + 3]
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb))
            }
        }
        $frames += , @{ Size = $w; Bitmap = $bmp }
    }
    return $frames
}

$frames = Read-IcoFrames $Ico
$pad = 12
$labelH = 18
$zoom = 4
$totalW = $pad
foreach ($f in $frames) { $totalW += 268 }
$zoomW = $pad
foreach ($f in $frames) { if ($f.Size -le 48) { $zoomW += ($f.Size * $zoom) + $pad } }
$W = [Math]::Max($totalW, $zoomW)
$H = $pad + 260 + $pad + $labelH + 64 + $pad + $labelH + 260 + $pad

$sheet = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(245, 246, 248))
$font = New-Object System.Drawing.Font('Consolas', 9, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 45, 55))

# Ряд 1: кадры в натуральную величину, каждый на своей подложке 256px.
$x = $pad
$y = $pad
foreach ($f in $frames) {
    $tile = New-Object System.Drawing.Rectangle($x, $y, 256, 256)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)), $tile)
    $sx = [int](($256 - $f.Size) / 2)
    $g.DrawImage($f.Bitmap, ($x + $sx), ($y + $sx), $f.Size, $f.Size)
    $g.DrawString(($f.Size.ToString() + ' px'), $font, $brush, ($x + 6), ($y + 4))
    $x += 268
}

# Ряд 2: увеличение 4x (без сглаживания) — видно, что нарисовано в мелких кадрах.
$x = $pad
$y = $pad + 260 + $pad + $labelH
$g.DrawString('увеличение 4x (nearest): 16, 24, 32, 48', $font, $brush, $pad, ($y - 16))
foreach ($f in $frames) {
    if ($f.Size -gt 48) { continue }
    $big = New-Object System.Drawing.Bitmap(($f.Size * $zoom), ($f.Size * $zoom))
    $gb = [System.Drawing.Graphics]::FromImage($big)
    $gb.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gb.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $gb.Clear([System.Drawing.Color]::FromArgb(230, 232, 236))
    $gb.DrawImage($f.Bitmap, 0, 0, ($f.Size * $zoom), ($f.Size * $zoom))
    $gb.Dispose()
    $g.DrawImage($big, $x, $y, ($f.Size * $zoom), ($f.Size * $zoom))
    $g.DrawString(($f.Size.ToString() + ' px x4'), $font, $brush, $x, ($y + $f.Size * $zoom + 2))
    $x += ($f.Size * $zoom) + $pad
}

# Ряд 3: как иконка смотрится в панели задач (16 и 32 на тёмной плашке).
$y3 = $y + 48 * $zoom + 30
$g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(32, 40, 52))), $pad, $y3, 300, 52)
foreach ($f in $frames) {
    if ($f.Size -ne 16 -and $f.Size -ne 32) { continue }
    $off = if ($f.Size -eq 16) { 16 } else { 60 }
    $g.DrawImage($f.Bitmap, ($pad + $off), ($y3 + 10), $f.Size, $f.Size)
}
$g.DrawString('на тёмной панели: 16 и 32', $font, (New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 225, 232))), ($pad + 110), ($y3 + 20))

$g.Dispose()
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory $dir -Force | Out-Null }
$sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
foreach ($f in $frames) { $f.Bitmap.Dispose() }
Write-Host ("Контрольный лист: " + $Out)
