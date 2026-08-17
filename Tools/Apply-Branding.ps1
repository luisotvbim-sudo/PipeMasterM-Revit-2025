[CmdletBinding()]
param(
    [string]$IconDirectory = (Join-Path $PSScriptRoot "..\Assets\Icones")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$accent = [System.Drawing.Color]::FromArgb(245, 124, 0)
$graphite = [System.Drawing.Color]::FromArgb(51, 51, 51)
$white = [System.Drawing.Color]::White
$background = [System.Drawing.Color]::FromArgb(230, 230, 230)
$border = [System.Drawing.Color]::FromArgb(200, 200, 200)

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Bounds,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Bounds.Left, $Bounds.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Bounds.Left, $Bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Export-SharpSmallIcon {
    param(
        [System.Drawing.Bitmap]$Source,
        [string]$DestinationPath
    )

    $scaled = [System.Drawing.Bitmap]::new(16, 16, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($scaled)
        try {
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, 16, 16))
        }
        finally {
            $graphics.Dispose()
        }

        $reference = $scaled.Clone()
        try {
            $amount = 0.55
            for ($y = 1; $y -lt 15; $y++) {
                for ($x = 1; $x -lt 15; $x++) {
                    $center = $reference.GetPixel($x, $y)
                    $neighbors = @(
                        $reference.GetPixel($x - 1, $y),
                        $reference.GetPixel($x + 1, $y),
                        $reference.GetPixel($x, $y - 1),
                        $reference.GetPixel($x, $y + 1)
                    )
                    $averageA = ($neighbors | Measure-Object -Property A -Average).Average
                    $averageR = ($neighbors | Measure-Object -Property R -Average).Average
                    $averageG = ($neighbors | Measure-Object -Property G -Average).Average
                    $averageB = ($neighbors | Measure-Object -Property B -Average).Average
                    $a = [Math]::Clamp([int][Math]::Round($center.A + $amount * ($center.A - $averageA)), 0, 255)
                    $r = [Math]::Clamp([int][Math]::Round($center.R + $amount * ($center.R - $averageR)), 0, 255)
                    $g = [Math]::Clamp([int][Math]::Round($center.G + $amount * ($center.G - $averageG)), 0, 255)
                    $b = [Math]::Clamp([int][Math]::Round($center.B + $amount * ($center.B - $averageB)), 0, 255)
                    $scaled.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $r, $g, $b))
                }
            }
        }
        finally {
            $reference.Dispose()
        }

        $temporaryPath = "$DestinationPath.branding.tmp.png"
        $scaled.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Move-Item -LiteralPath $temporaryPath -Destination $DestinationPath -Force
    }
    finally {
        $scaled.Dispose()
    }
}

$resolvedDirectory = (Resolve-Path -LiteralPath $IconDirectory).Path
$icons = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter "*.png" -File |
    Where-Object { $_.BaseName -notlike "*_16" })
if ($icons.Count -eq 0) {
    throw "Nenhum ícone PNG encontrado em $resolvedDirectory"
}

$appliedCount = 0
$skippedCount = 0
foreach ($icon in $icons) {
    $sourceBytes = [System.IO.File]::ReadAllBytes($icon.FullName)
    $sourceStream = [System.IO.MemoryStream]::new($sourceBytes, $false)
    $source = [System.Drawing.Bitmap]::new($sourceStream)
    try {
        $brandingPixelCount = 0
        $opaqueColors = @{}
        for ($y = 0; $y -lt $source.Height; $y++) {
            for ($x = 0; $x -lt $source.Width; $x++) {
                $probe = $source.GetPixel($x, $y)
                $isBackground = $probe.A -ge 240 -and
                    [Math]::Abs($probe.R - $background.R) -le 4 -and
                    [Math]::Abs($probe.G - $background.G) -le 4 -and
                    [Math]::Abs($probe.B - $background.B) -le 4
                if ($isBackground) {
                    $brandingPixelCount++
                }

                if ($probe.A -ge 240) {
                    $colorKey = "$($probe.R),$($probe.G),$($probe.B)"
                    if ($opaqueColors.ContainsKey($colorKey)) {
                        $opaqueColors[$colorKey]++
                    }
                    else {
                        $opaqueColors[$colorKey] = 1
                    }
                }
            }
        }

        if ($brandingPixelCount -ge 8) {
            $smallPath = Join-Path $resolvedDirectory "$($icon.BaseName)_16.png"
            Export-SharpSmallIcon -Source $source -DestinationPath $smallPath
            Write-Verbose "Identidade visual já aplicada: $($icon.Name)"
            $skippedCount++
            continue
        }

        $legacyBackground = $null
        $dominantOpaque = $opaqueColors.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
        if ($null -ne $dominantOpaque -and $dominantOpaque.Value -ge ($source.Width * $source.Height * 0.20)) {
            $channels = @($dominantOpaque.Key.Split(',') | ForEach-Object { [int]$_ })
            $dominantColor = [System.Drawing.Color]::FromArgb($channels[0], $channels[1], $channels[2])
            if ($dominantColor.GetBrightness() -le 0.35) {
                $legacyBackground = $dominantColor
            }
        }

        $overlay = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            for ($y = 0; $y -lt $source.Height; $y++) {
                for ($x = 0; $x -lt $source.Width; $x++) {
                    $pixel = $source.GetPixel($x, $y)
                    if ($pixel.A -eq 0) {
                        continue
                    }

                    if ($null -ne $legacyBackground) {
                        $distance = [Math]::Sqrt(
                            [Math]::Pow($pixel.R - $legacyBackground.R, 2) +
                            [Math]::Pow($pixel.G - $legacyBackground.G, 2) +
                            [Math]::Pow($pixel.B - $legacyBackground.B, 2)
                        )
                        if ($distance -le 28) {
                            continue
                        }
                    }

                    $hue = $pixel.GetHue()
                    $saturation = $pixel.GetSaturation()
                    $brightness = $pixel.GetBrightness()

                    if ($saturation -ge 0.18 -and ($hue -ge 205 -or $hue -le 60)) {
                        $target = $accent
                    }
                    elseif ($saturation -ge 0.18) {
                        $target = $graphite
                    }
                    elseif ($brightness -ge 0.78) {
                        $target = $white
                    }
                    else {
                        $target = $graphite
                    }

                    $sharpenedAlpha = [Math]::Clamp([int][Math]::Round(($pixel.A - 128) * 1.35 + 128), 0, 255)
                    if ($sharpenedAlpha -gt 0) {
                        $overlay.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($sharpenedAlpha, $target.R, $target.G, $target.B))
                    }
                }
            }

            $output = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($output)
                try {
                    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                    $tileBounds = [System.Drawing.RectangleF]::new(1.5, 1.5, $source.Width - 3, $source.Height - 3)
                    $tilePath = New-RoundedRectanglePath -Bounds $tileBounds -Radius 5
                    try {
                        $fillBrush = [System.Drawing.SolidBrush]::new($background)
                        $borderPen = [System.Drawing.Pen]::new($border, 1)
                        try {
                            $graphics.FillPath($fillBrush, $tilePath)
                            $graphics.DrawPath($borderPen, $tilePath)
                        }
                        finally {
                            $fillBrush.Dispose()
                            $borderPen.Dispose()
                        }
                    }
                    finally {
                        $tilePath.Dispose()
                    }

                    $graphics.DrawImageUnscaled($overlay, 0, 0)
                }
                finally {
                    $graphics.Dispose()
                }

                $temporaryPath = "$($icon.FullName).branding.tmp.png"
                $output.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
                Move-Item -LiteralPath $temporaryPath -Destination $icon.FullName -Force
                $smallPath = Join-Path $resolvedDirectory "$($icon.BaseName)_16.png"
                Export-SharpSmallIcon -Source $output -DestinationPath $smallPath
                $appliedCount++
            }
            finally {
                $output.Dispose()
            }
        }
        finally {
            $overlay.Dispose()
        }
    }
    finally {
        $source.Dispose()
        $sourceStream.Dispose()
    }
}

Write-Host "Identidade visual aplicada a $appliedCount ícones; $skippedCount já estavam atualizados. Diretório: $resolvedDirectory"
