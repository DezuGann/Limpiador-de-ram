Add-Type -AssemblyName System.Drawing

$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# Clear background (transparent)
$g.Clear([System.Drawing.Color]::Transparent)

# Background rounded rect or circle with electric blue / cyan gradient
$rect = New-Object System.Drawing.Rectangle 2, 2, 60, 60
$gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(26, 115, 232),
    [System.Drawing.Color]::FromArgb(13, 71, 161),
    45.0
)

# Draw circle background
$g.FillEllipse($gradBrush, $rect)

# Draw subtle outer border
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 255, 255, 255), 2)
$g.DrawEllipse($borderPen, 3, 3, 58, 58)

# Draw RAM Chip body (horizontal bar with chip pins)
$chipBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240, 244, 248))
$g.FillRectangle($chipBrush, 14, 24, 36, 16)

# Pins on the bottom
$pinBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 215, 0))
for ($x = 16; $x -le 46; $x += 5) {
    $g.FillRectangle($pinBrush, $x, 40, 3, 4)
}

# Chips on the RAM module
$icBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(44, 62, 80))
$g.FillRectangle($icBrush, 17, 27, 8, 10)
$g.FillRectangle($icBrush, 28, 27, 8, 10)
$g.FillRectangle($icBrush, 39, 27, 8, 10)

# Draw Electric Lightning Bolt in center-right
$boltPoints = @(
    (New-Object System.Drawing.Point 35, 10),
    (New-Object System.Drawing.Point 25, 32),
    (New-Object System.Drawing.Point 34, 32),
    (New-Object System.Drawing.Point 28, 54),
    (New-Object System.Drawing.Point 44, 28),
    (New-Object System.Drawing.Point 35, 28),
    (New-Object System.Drawing.Point 41, 10)
)
$boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 220, 0))
$boltPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 81, 0), 1.5)
$g.FillPolygon($boltBrush, $boltPoints)
$g.DrawPolygon($boltPen, $boltPoints)

$g.Dispose()

# Convert bitmap to icon
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)

$icoPath = Join-Path $PSScriptRoot "app.ico"
$fs = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()

Write-Host "Icon created successfully at: $icoPath"
