param([string]$src, [string]$dst, [int]$frameH, [int]$scale = 4, [int]$perRow = 13)
# Lays a vertical sprite sheet out in rows of frames, scaled up, on a sky-blue background, with frame numbers.
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($src)
$fw = $img.Width; $n = [int]($img.Height / $frameH)
$rows = [math]::Ceiling($n / $perRow)
$cw = $fw * $scale + 6; $ch = $frameH * $scale + 18
$out = New-Object System.Drawing.Bitmap ($cw * $perRow), ($ch * $rows)
$g = [System.Drawing.Graphics]::FromImage($out)
$g.Clear([System.Drawing.Color]::FromArgb(255, 120, 170, 220))
$g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
$font = New-Object System.Drawing.Font 'Arial', 9
for ($i = 0; $i -lt $n; $i++) {
	$cx = ($i % $perRow) * $cw; $cy = [math]::Floor($i / $perRow) * $ch
	$g.DrawString("$i", $font, [System.Drawing.Brushes]::Black, $cx + 2, $cy + 1)
	$g.DrawImage($img, (New-Object System.Drawing.Rectangle $cx, ($cy + 16), ($fw * $scale), ($frameH * $scale)), (New-Object System.Drawing.Rectangle 0, ($i * $frameH), $fw, $frameH), 'Pixel')
}
$g.Dispose(); $out.Save($dst); $out.Dispose(); $img.Dispose()
