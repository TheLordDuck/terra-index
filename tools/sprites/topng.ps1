param([string]$dir)
Add-Type -AssemblyName System.Drawing
foreach ($f in Get-ChildItem "$dir\*.rgba") {
	$bytes = [System.IO.File]::ReadAllBytes($f.FullName)
	$w = [BitConverter]::ToInt32($bytes, 0); $h = [BitConverter]::ToInt32($bytes, 4)
	$bmp = New-Object System.Drawing.Bitmap $w, $h
	for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
		$i = 8 + ($y * $w + $x) * 4
		$bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($bytes[$i + 3], $bytes[$i], $bytes[$i + 1], $bytes[$i + 2]))
	} }
	$bmp.Save(($f.FullName -replace '\.rgba$', '.png'))
	$bmp.Dispose()
}
