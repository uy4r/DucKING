# DucKING uygulama simgesini (koyu yuvarlatılmış kare üzerinde kırmızı taçlı ördek) çok boyutlu .ico olarak üretir.
# Kullanım: powershell -File tools\make-icon.ps1 -Out src\DucKING\Assets\ducking.ico
# Çizim, src\DucKING\Assets\ducking.svg ile aynı 64 birimlik koordinatları kullanır; birini değiştirirseniz
# diğerini (ve MainWindow.xaml'deki üst şerit logosunu) da güncelleyin.
param([Parameter(Mandatory)][string]$Out)

Add-Type -AssemblyName System.Drawing

$crown = @(13, 21), @(13, 12), @(19, 16), @(26, 5), @(33, 16), @(39, 12), @(39, 21)
$head = @(7, 62), @(5, 44), @(9, 30), @(18, 23), @(34, 23), @(42, 29), @(44, 32), @(56, 33), @(59, 36), @(58, 40),
        @(53, 42), @(43, 42), @(40, 46), @(34, 49), @(39, 55)
$eye = @(28, 29), @(38, 32), @(36, 35), @(29, 34)

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Koyu yuvarlatılmış kare (uygulamanın zemin renkleri)
    $r = [single]($size * 0.44)
    $s = [single]($size - 1)
    $bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bgPath.AddArc(0, 0, $r, $r, 180, 90)
    $bgPath.AddArc($s - $r, 0, $r, $r, 270, 90)
    $bgPath.AddArc($s - $r, $s - $r, $r, $r, 0, 90)
    $bgPath.AddArc(0, $s - $r, $r, $r, 90, 90)
    $bgPath.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size), ([System.Drawing.Color]::FromArgb(255, 26, 26, 30)), ([System.Drawing.Color]::FromArgb(255, 7, 7, 8))
    $g.FillPath($bg, $bgPath)

    # Küçük boyutlarda logo kareyi daha çok doldurur, yoksa ayrıntılar kaybolur.
    $fill = if ($size -le 24) { 0.92 } else { 0.80 }
    $scale = $size * $fill / 64.0
    $offset = ($size - 64 * $scale) / 2.0
    $toPoints = { param($list) [System.Drawing.PointF[]]@($list | ForEach-Object { New-Object System.Drawing.PointF ([single]($offset + $_[0] * $scale)), ([single]($offset + $_[1] * $scale)) }) }

    $red = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 229, 35, 47))
    $mark = New-Object System.Drawing.Drawing2D.GraphicsPath ([System.Drawing.Drawing2D.FillMode]::Alternate)
    $mark.AddPolygon((& $toPoints $head))
    if ($size -ge 24) { $mark.AddPolygon((& $toPoints $eye)) } # 16-20 px'te göz okunmaz, leke gibi durur
    $g.FillPath($red, $mark)
    $g.FillPolygon($red, (& $toPoints $crown))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 256, 128, 64, 48, 40, 32, 24, 20, 16
$images = $sizes | ForEach-Object { , (New-IconPng $_) }

# ICO: 6 bayt başlık + her görüntü için 16 bayt dizin girdisi + PNG verileri
$buffer = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($buffer)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offsetBytes = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offsetBytes)
    $offsetBytes += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()

$full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
New-Item -ItemType Directory -Force (Split-Path $full) | Out-Null
[System.IO.File]::WriteAllBytes($full, $buffer.ToArray())
"Icon written: $Out ($($buffer.Length) bytes)"
