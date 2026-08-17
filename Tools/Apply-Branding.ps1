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

$resolvedDirectory = (Resolve-Path -LiteralPath $IconDirectory).Path
$icons = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter "*.png" -File)
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

                    $overlay.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($pixel.A, $target.R, $target.G, $target.B))
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
