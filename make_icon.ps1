param([string]$OutPath = "app.ico")
Add-Type -AssemblyName System.Drawing

function New-RoundPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min([Math]::Min($radius * 2, $r.Width), $r.Height)
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $bg = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $path = New-RoundPath $bg ([float]($s * 0.22))
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 95, 63))
    $g.FillPath($brush, $path)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 60, 36), [Math]::Max(1, $s * 0.015))
    $g.DrawPath($pen, $path)
    $brush.Dispose(); $pen.Dispose(); $path.Dispose()

    $cx = $s * 0.50
    $top = $s * 0.28
    $bottom = $s * 0.74
    $left = $s * 0.23
    $right = $s * 0.77
    $lift = $s * 0.045
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(248, 246, 240))
    $lp = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([single]$cx, [single]($top + $lift)),
        [System.Drawing.PointF]::new([single]$left, [single]$top),
        [System.Drawing.PointF]::new([single]$left, [single]($bottom - $lift)),
        [System.Drawing.PointF]::new([single]$cx, [single]$bottom)
    )
    $rp = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([single]$cx, [single]($top + $lift)),
        [System.Drawing.PointF]::new([single]$right, [single]$top),
        [System.Drawing.PointF]::new([single]$right, [single]($bottom - $lift)),
        [System.Drawing.PointF]::new([single]$cx, [single]$bottom)
    )
    $g.FillPolygon($white, $lp)
    $g.FillPolygon($white, $rp)
    $white.Dispose()

    $spine = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 95, 63), [Math]::Max(1, $s * 0.035))
    $spine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spine.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($spine, $cx, $top + $lift, $cx, $bottom)
    $spine.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    $ms.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $OutPath), $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Output ("icon written: " + $OutPath + " bytes=" + (Get-Item $OutPath).Length)
