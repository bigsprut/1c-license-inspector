# Генерирует иконку приложения (BMP-кадры, без PNG-сжатия — совместимо с Windows XP/2003).
#
#   full (по умолчанию) — янтарный квадрат с «1C»: полное приложение, res\app.ico
#   lite              — бирюзовый квадрат с «1C» и полоской из двух «вкладок»: краткое
#                       приложение, res\app-lite.ico (чтобы в списке процессов и на панели
#                       задач их не путали)
param(
    [string]$OutPath = "",
    [ValidateSet('full', 'lite')][string]$Variant = 'full'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrEmpty($OutPath)) {
    $root = Split-Path -Parent $PSScriptRoot
    $OutPath = if ($Variant -eq 'lite') { Join-Path $root 'res\app-lite.ico' } else { Join-Path $root 'res\app.ico' }
}

# Палитра под вариант: полное — янтарь, краткое — бирюза.
$lite = ($Variant -eq 'lite')
$top = if ($lite) { [System.Drawing.Color]::FromArgb(255, 96, 204, 170) } else { [System.Drawing.Color]::FromArgb(255, 235, 172, 70) }
$bottom = if ($lite) { [System.Drawing.Color]::FromArgb(255, 30, 143, 112) } else { [System.Drawing.Color]::FromArgb(255, 214, 141, 32) }
$ink = if ($lite) { [System.Drawing.Color]::FromArgb(255, 8, 48, 38) } else { [System.Drawing.Color]::FromArgb(255, 26, 38, 50) }

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $margin = [Math]::Max(1, [int]($size * 0.06))
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, ($size - 2 * $margin), ($size - 2 * $margin))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Max(2, [int]($size * 0.22))
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, 90.0)
    $g.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 26, 38, 50), [Math]::Max(1.0, $size / 48.0))
    $g.DrawPath($pen, $path)

    $fontSize = [float]($size * 0.42)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textBrush = New-Object System.Drawing.SolidBrush($ink)
    # У краткого варианта надпись чуть выше — внизу оставляем место под «вкладки».
    $textRect = if ($lite) {
        New-Object System.Drawing.RectangleF(0, (-$size * 0.06), $size, $size)
    } else {
        New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    }
    $g.DrawString('1C', $font, $textBrush, $textRect, $fmt)

    if ($lite) {
        # Полоска из двух «вкладок» — узнаваемый признак краткого приложения.
        $barW = [float]($size * 0.46)
        $barH = [Math]::Max(2.0, [float]($size * 0.10))
        $gap = [Math]::Max(1.0, [float]($size * 0.05))
        $half = ($barW - $gap) / 2.0
        $y = [float]($rect.Bottom - $barH - $size * 0.11)
        $x = [float]($rect.X + ($rect.Width - $barW) / 2.0)
        $barBrush = New-Object System.Drawing.SolidBrush($ink)
        $r1 = New-Object System.Drawing.RectangleF($x, $y, $half, $barH)
        $r2 = New-Object System.Drawing.RectangleF(($x + $half + $gap), $y, $half, $barH)
        $g.FillRectangle($barBrush, $r1)
        $g.FillRectangle($barBrush, $r2)
        $barBrush.Dispose()
    }

    $font.Dispose(); $textBrush.Dispose(); $brush.Dispose(); $pen.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

function Get-IconData([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # BITMAPINFOHEADER
    $bw.Write([int]40)
    $bw.Write([int]$w)
    $bw.Write([int]($h * 2))
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int]0)
    $bw.Write([int]($w * $h * 4))
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($w * $h * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)

    for ($y = $h - 1; $y -ge 0; $y--) {
        $bw.Write($bytes, ($y * $w * 4), ($w * 4))
    }

    # AND-маска: всё непрозрачно
    $maskRow = [int]([Math]::Floor(($w + 31) / 32) * 4)
    $mask = New-Object byte[] ($maskRow * $h)
    $bw.Write($mask, 0, $mask.Length)

    $bw.Flush()
    return $ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 256)
$images = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $images += , @{ Size = $s; Data = (Get-IconData $bmp) }
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$wri = New-Object System.IO.BinaryWriter($out)
$wri.Write([int16]0)
$wri.Write([int16]1)
$wri.Write([int16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
foreach ($img in $images) {
    $s = $img.Size
    $wri.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $wri.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $wri.Write([byte]0)
    $wri.Write([byte]0)
    $wri.Write([int16]1)
    $wri.Write([int16]32)
    $wri.Write([int]$img.Data.Length)
    $wri.Write([int]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $wri.Write($img.Data, 0, $img.Data.Length) }
$wri.Flush()

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())
$wri.Dispose(); $out.Dispose()
Write-Host ("Иконка создана (" + $Variant + "): " + $OutPath + " (" + (Get-Item $OutPath).Length + " байт)")
