# Генерирует иконки приложений (BMP-кадры, без PNG-сжатия — совместимо с Windows XP/2003).
#
# С 1.7.1 рисунок — «просмотр лицензий 1С»: белый бланк лицензии с надписью «1C»
# и увеличительное стекло поверх него (общепринятый знак «посмотреть/проверить»).
#   full (по умолчанию) — янтарный фон, res\app.ico        (полное приложение)
#   lite              — бирюзовый фон, res\app-lite.ico    (краткое приложение)
# Рисунок одинаковый, различаются цветом: так обе иконки читаются как «лицензии 1С»,
# а в списке процессов и на панели задач их не путают.
#
# Размеры 16–256: мелкие кадры рисуются упрощённо (толще линии, без подписи «1C»),
# иначе в 16 px получается каша.
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
$paper = [System.Drawing.Color]::FromArgb(255, 252, 252, 250)
$glass = [System.Drawing.Color]::FromArgb(205, 255, 255, 255)

function New-RoundPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($radius -lt 0.6) {
        $p.AddRectangle($r)
        return $p
    }
    $d = $radius * 2
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # --- фон: скруглённый квадрат с градиентом ---
    $margin = [Math]::Max(1, [int]($size * 0.05))
    $rect = New-Object System.Drawing.RectangleF($margin, $margin, ($size - 2 * $margin), ($size - 2 * $margin))
    $bgPath = New-RoundPath $rect ([Math]::Max(2.0, $size * 0.22))
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, 90.0)
    $g.FillPath($bgBrush, $bgPath)
    $bgPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 26, 38, 50)), ([Math]::Max(1.0, $size / 48.0))
    $g.DrawPath($bgPen, $bgPath)

    $small = ($size -le 24)
    # --- бланк лицензии ---
    # Крупные кадры: бланк слева, стекло справа, между ними «воздух».
    # Мелкие: бланк чуть шире и левее, стекло крупнее — иначе не читается.
    $docX = if ($small) { $size * 0.115 } else { $size * 0.125 }
    $docY = if ($small) { $size * 0.17 } else { $size * 0.135 }
    $docW = if ($small) { $size * 0.47 } else { $size * 0.46 }
    $docH = if ($small) { $size * 0.63 } else { $size * 0.68 }
    $docRect = New-Object System.Drawing.RectangleF($docX, $docY, $docW, $docH)
    $docPath = New-RoundPath $docRect ([Math]::Max(1.0, $size * 0.045))
    $paperBrush = New-Object System.Drawing.SolidBrush($paper)
    $g.FillPath($paperBrush, $docPath)
    $paperPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 26, 38, 50)), ([Math]::Max(0.8, $size * 0.012))
    $g.DrawPath($paperPen, $docPath)

    # --- содержимое бланка: сверху надпись «1C», ниже строки ---
    $linePen = New-Object System.Drawing.Pen ($ink), ([Math]::Max(1.0, $size * 0.030))
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $lx1 = $docRect.X + $docRect.Width * 0.15
    $lx2 = $docRect.X + $docRect.Width * 0.85

    # Надпись «1C» — только там, где её реально видно (32 px и больше); в мелких кадрах
    # на её месте ещё одна строка, иначе выходит грязное пятно.
    if ($size -ge 32) {
        $fontSize = [float]($size * 0.185)
        $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = [System.Drawing.StringAlignment]::Center
        $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
        $textBrush = New-Object System.Drawing.SolidBrush($ink)
        $textRect = New-Object System.Drawing.RectangleF($docRect.X, ($docRect.Y + $docRect.Height * 0.05),
            $docRect.Width, ($docRect.Height * 0.26))
        $g.DrawString('1C', $font, $textBrush, $textRect, $fmt)
        $font.Dispose(); $textBrush.Dispose()
        $lineYs = @(0.47, 0.62, 0.77)
    } else {
        $lineYs = @(0.28, 0.48, 0.68)
    }
    for ($i = 0; $i -lt $lineYs.Count; $i++) {
        $ly = $docRect.Y + $docRect.Height * $lineYs[$i]
        $r = if ($i -eq $lineYs.Count - 1) { 0.62 } else { 0.85 }
        $g.DrawLine($linePen, $lx1, $ly, [float]($docRect.X + $docRect.Width * $r), $ly)
    }

    # --- печать/пломба лицензии (только на крупных кадрах, в левом нижнем углу бланка) ---
    if ($size -ge 48) {
        $sealR = $size * 0.052
        $sealX = $docRect.X + $docRect.Width * 0.17
        $sealY = $docRect.Y + $docRect.Height * 0.90
        $sealBrush = New-Object System.Drawing.SolidBrush($bottom)
        $g.FillEllipse($sealBrush, [float]($sealX - $sealR), [float]($sealY - $sealR), [float]($sealR * 2), [float]($sealR * 2))
        $sealBrush.Dispose()
    }

    # --- лупа: стекло, обод, ручка ---
    $lensR = if ($small) { $size * 0.205 } else { $size * 0.160 }
    $lensCX = if ($small) { $size * 0.655 } else { $size * 0.675 }
    $lensCY = if ($small) { $size * 0.585 } else { $size * 0.560 }
    $ringW = if ($small) { [Math]::Max(1.4, $size * 0.075) } else { [Math]::Max(1.6, $size * 0.058) }

    $glassBrush = New-Object System.Drawing.SolidBrush($glass)
    $g.FillEllipse($glassBrush, [float]($lensCX - $lensR), [float]($lensCY - $lensR), [float]($lensR * 2), [float]($lensR * 2))
    $glassBrush.Dispose()

    $ringPen = New-Object System.Drawing.Pen ($ink), ([float]$ringW)
    $g.DrawEllipse($ringPen, [float]($lensCX - $lensR), [float]($lensCY - $lensR), [float]($lensR * 2), [float]($lensR * 2))

    # Ручка лупы: 45°, от обода к правому нижнему углу, с круглыми концами.
    $handleW = if ($small) { [Math]::Max(1.6, $size * 0.085) } else { [Math]::Max(1.8, $size * 0.072) }
    $handlePen = New-Object System.Drawing.Pen ($ink), ([float]$handleW)
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $dx = 0.7071 * $lensR
    $hx1 = $lensCX + $dx * 1.05
    $hy1 = $lensCY + $dx * 1.05
    $hx2 = $lensCX + $size * 0.245
    $hy2 = $lensCY + $size * 0.265
    if ($hx2 -gt $size * 0.90) { $hx2 = $size * 0.90 }
    if ($hy2 -gt $size * 0.90) { $hy2 = $size * 0.90 }
    $g.DrawLine($handlePen, [float]$hx1, [float]$hy1, [float]$hx2, [float]$hy2)

    # Блик на стекле — тонкая светлая дуга сверху слева (на мелочи не рисуем: шум).
    if (-not $small) {
        $shinePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150, 255, 255, 255)), ([Math]::Max(0.8, $size * 0.022))
        $shinePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $shinePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $sr = $lensR * 0.62
        $g.DrawArc($shinePen, [float]($lensCX - $sr), [float]($lensCY - $sr), [float]($sr * 2), [float]($sr * 2), 200, 70)
        $shinePen.Dispose()
    }

    $linePen.Dispose(); $ringPen.Dispose(); $handlePen.Dispose(); $paperBrush.Dispose(); $paperPen.Dispose()
    $bgBrush.Dispose(); $bgPen.Dispose(); $bgPath.Dispose(); $docPath.Dispose(); $g.Dispose()
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
