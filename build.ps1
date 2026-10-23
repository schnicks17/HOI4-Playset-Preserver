# Builds HOI4_PP.exe with the C# compiler that ships with Windows (.NET Framework 4.x) - no SDK needed.
# Also draws the shield icon (shield.ico). Run: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Add-Type -AssemblyName System.Drawing

# ---- icon: red/gold shield with a white snowflake ("frozen"), PNG-compressed ICO with several sizes
function Draw-Shield([int]$S) {
    $bmp = New-Object Drawing.Bitmap $S, $S
    $g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
    $k = $S / 256.0
    $sx = 28 * $k; $sy = 14 * $k; $sw = 200 * $k; $sh = 228 * $k
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddLine($sx, $sy, $sx + $sw, $sy); $p.AddLine($sx + $sw, $sy, $sx + $sw, $sy + $sh * 0.45)
    $p.AddBezier($sx + $sw, $sy + $sh * 0.45, $sx + $sw, $sy + $sh * 0.8, $sx + $sw * 0.62, $sy + $sh * 0.92, $sx + $sw / 2, $sy + $sh)
    $p.AddBezier($sx + $sw / 2, $sy + $sh, $sx + $sw * 0.38, $sy + $sh * 0.92, $sx, $sy + $sh * 0.8, $sx, $sy + $sh * 0.45); $p.CloseFigure()
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.PointF $sx, $sy), (New-Object Drawing.PointF $sx, ($sy + $sh)), ([Drawing.Color]::FromArgb(255, 160, 30, 30)), ([Drawing.Color]::FromArgb(255, 74, 13, 13))
    $g.FillPath($fill, $p)
    $g.DrawPath((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 224, 191, 74)), ([Math]::Max(1.5, 12 * $k))), $p)
    $cx = $sx + $sw / 2; $cy = $sy + $sh * 0.47
    $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 240, 248, 255)), ([Math]::Max(1.2, 13 * $k)); $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $r = 70 * $k
    for ($i = 0; $i -lt 6; $i++) {
        $a = [Math]::PI / 3 * $i
        $g.DrawLine($pen, $cx, $cy, $cx + $r * [Math]::Cos($a), $cy + $r * [Math]::Sin($a))
        if ($S -ge 32) { foreach ($s in -1, 1) { $mx = $cx + $r * 0.58 * [Math]::Cos($a); $my = $cy + $r * 0.58 * [Math]::Sin($a); $b = $a + $s * 0.7; $g.DrawLine($pen, $mx, $my, $mx + $r * 0.34 * [Math]::Cos($b), $my + $r * 0.34 * [Math]::Sin($b)) } }
    }
    $g.Dispose(); $bmp
}
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { $b = Draw-Shield $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); , $ms.ToArray() }
$ico = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset); $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $here 'shield.ico'), $ico.ToArray())

# ---- compile
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $here 'HOI4_PP.exe'
& $csc /nologo /target:exe /optimize+ /platform:anycpu "/out:$out" "/win32icon:$(Join-Path $here 'shield.ico')" /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $here 'Program.cs') (Join-Path $here 'PPSqlite.cs')
if ($LASTEXITCODE -ne 0) { throw "compile failed" }
"built $out ($((Get-Item $out).Length) bytes)"
