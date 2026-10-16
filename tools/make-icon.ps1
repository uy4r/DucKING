# DucKING uygulama simgesini (koyu zemin üzerinde altın taç) çok boyutlu .ico olarak üretir.
# Kullanım: powershell -File tools\make-icon.ps1 -Out src\DucKING\Assets\ducking.ico
param([Parameter(Mandatory)][string]$Out)

Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Yuvarlatılmış koyu kare
    $r = [single]($size * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $s = [single]($size - 1)
    $path.AddArc(0, 0, $r, $r, 180, 90)
    $path.AddArc($s - $r, 0, $r, $r, 270, 90)
    $path.AddArc($s - $r, $s - $r, $r, $r, 0, 90)
    $path.AddArc(0, $s - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size), ([System.Drawing.Color]::FromArgb(255, 30, 35, 44)), ([System.Drawing.Color]::FromArgb(255, 14, 16, 20))
    $g.FillPath($bg, $path)

    # Taç: üç sivri uçlu gövde + taban bandı
    $u = $size / 16.0
    $pts = @(
        @(3.0, 11.0), @(2.6, 5.2), @(5.6, 7.8), @(8.0, 3.6), @(10.4, 7.8), @(13.4, 5.2), @(13.0, 11.0)
    ) | ForEach-Object { New-Object System.Drawing.PointF ([single]($_[0] * $u)), ([single]($_[1] * $u)) }
    $gold = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, ([single](3 * $u))), (New-Object System.Drawing.PointF 0, ([single](13 * $u))), ([System.Drawing.Color]::FromArgb(255, 255, 214, 102)), ([System.Drawing.Color]::FromArgb(255, 245, 164, 0))
    $g.FillPolygon($gold, [System.Drawing.PointF[]]$pts)
    $g.FillRectangle($gold, [single](3.0 * $u), [single](11.6 * $u), [single](10.0 * $u), [single](1.6 * $u))

    # Uçlardaki toplar (küçük boyutlarda okunmadığı için çizilmez)
    if ($size -ge 32) {
        foreach ($p in @(@(2.6, 5.2), @(8.0, 3.6), @(13.4, 5.2))) {
            $d = [single](1.5 * $u)
            $g.FillEllipse($gold, [single]($p[0] * $u - $d / 2), [single]($p[1] * $u - $d / 2), $d, $d)
        }
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 128, 64, 48, 32, 24, 16
$images = $sizes | ForEach-Object { , (New-IconPng $_) }

# ICO: 6 bayt başlık + her görüntü için 16 bayt dizin girdisi + PNG verileri
$buffer = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($buffer)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()

$full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
New-Item -ItemType Directory -Force (Split-Path $full) | Out-Null
[System.IO.File]::WriteAllBytes($full, $buffer.ToArray())
"Icon written: $Out ($($buffer.Length) bytes)"
