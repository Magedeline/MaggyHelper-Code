Add-Type -AssemblyName System.Drawing

$file = Join-Path $PSScriptRoot '..\Graphics\Atlases\Gui\logo\logo.png'
$file = (Resolve-Path $file).Path
$backup = Join-Path (Split-Path $file) 'logo_before_soft_edge_backup.png'

Copy-Item $file $backup -Force

$bmp = [System.Drawing.Bitmap]::FromFile($file)
$w = $bmp.Width
$h = $bmp.Height

$edge = New-Object 'bool[,]' $w, $h

for ($y = 1; $y -lt ($h - 1); $y++) {
    for ($x = 1; $x -lt ($w - 1); $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.A -eq 0) { continue }

        $touchesTransparent = $false
        for ($oy = -1; $oy -le 1 -and -not $touchesTransparent; $oy++) {
            for ($ox = -1; $ox -le 1; $ox++) {
                if ($ox -eq 0 -and $oy -eq 0) { continue }
                if ($bmp.GetPixel($x + $ox, $y + $oy).A -eq 0) {
                    $touchesTransparent = $true
                    break
                }
            }
        }

        if ($touchesTransparent) {
            $edge[$x, $y] = $true
        }
    }
}

$changed = 0
$softA = 230
$softR = 18
$softG = 18
$softB = 18

for ($y = 1; $y -lt ($h - 1); $y++) {
    for ($x = 1; $x -lt ($w - 1); $x++) {
        if (-not $edge[$x, $y]) { continue }

        $c = $bmp.GetPixel($x, $y)
        if ($c.A -ne $softA -or $c.R -ne $softR -or $c.G -ne $softG -or $c.B -ne $softB) {
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($softA, $softR, $softG, $softB))
            $changed++
        }
    }
}

$tmp = Join-Path (Split-Path $file) 'logo_soft_edge_tmp.png'
$bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Move-Item $tmp $file -Force

$check = [System.Drawing.Bitmap]::FromFile($file)
$corner = $check.GetPixel(0, 0)
$center = $check.GetPixel([int]($w / 2), [int]($h / 2))
$check.Dispose()

Write-Output "Softened edge pixels changed: $changed"
Write-Output "Corner alpha: $($corner.A)"
Write-Output "Center alpha: $($center.A), RGB: $($center.R),$($center.G),$($center.B)"
Write-Output "Backup: $backup"
