[CmdletBinding()]
param(
    [string]$IconDirectory = (Join-Path $PSScriptRoot "..\Assets\Icones")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$accent = [System.Drawing.Color]::FromArgb(245, 124, 0)

function Convert-HslToColor {
    param(
        [byte]$Alpha,
        [double]$Hue,
        [double]$Saturation,
        [double]$Lightness
    )

    if ($Saturation -le 0) {
        $channel = [Math]::Clamp([int][Math]::Round($Lightness * 255), 0, 255)
        return [System.Drawing.Color]::FromArgb($Alpha, $channel, $channel, $channel)
    }

    $h = (($Hue % 360) + 360) % 360 / 360
    $q = if ($Lightness -lt 0.5) {
        $Lightness * (1 + $Saturation)
    }
    else {
        $Lightness + $Saturation - ($Lightness * $Saturation)
    }
    $p = 2 * $Lightness - $q

    function Get-HueChannel {
        param(
            [double]$P,
            [double]$Q,
            [double]$T
        )

        if ($T -lt 0) { $T += 1 }
        if ($T -gt 1) { $T -= 1 }
        if ($T -lt (1 / 6)) { return $P + ($Q - $P) * 6 * $T }
        if ($T -lt (1 / 2)) { return $Q }
        if ($T -lt (2 / 3)) { return $P + ($Q - $P) * ((2 / 3) - $T) * 6 }
        return $P
    }

    $red = Get-HueChannel -P $p -Q $q -T ($h + 1 / 3)
    $green = Get-HueChannel -P $p -Q $q -T $h
    $blue = Get-HueChannel -P $p -Q $q -T ($h - 1 / 3)

    return [System.Drawing.Color]::FromArgb(
        $Alpha,
        [Math]::Clamp([int][Math]::Round($red * 255), 0, 255),
        [Math]::Clamp([int][Math]::Round($green * 255), 0, 255),
        [Math]::Clamp([int][Math]::Round($blue * 255), 0, 255)
    )
}

function Export-SmallIcon {
    param(
        [System.Drawing.Bitmap]$Source,
        [string]$DestinationPath
    )

    $scaled = [System.Drawing.Bitmap]::new(16, 16, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($scaled)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, 16, 16))
        }
        finally {
            $graphics.Dispose()
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

$convertedPixels = 0
foreach ($icon in $icons) {
    $sourceBytes = [System.IO.File]::ReadAllBytes($icon.FullName)
    $sourceStream = [System.IO.MemoryStream]::new($sourceBytes, $false)
    $source = [System.Drawing.Bitmap]::new($sourceStream)
    try {
        $output = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            for ($y = 0; $y -lt $source.Height; $y++) {
                for ($x = 0; $x -lt $source.Width; $x++) {
                    $pixel = $source.GetPixel($x, $y)
                    $replacement = $pixel

                    if ($pixel.A -gt 0) {
                        $hue = $pixel.GetHue()
                        $saturation = $pixel.GetSaturation()
                        $isPurple = $saturation -ge 0.04 -and $hue -ge 235 -and $hue -le 320
                        if ($isPurple) {
                            $replacement = Convert-HslToColor `
                                -Alpha $pixel.A `
                                -Hue $accent.GetHue() `
                                -Saturation $saturation `
                                -Lightness $pixel.GetBrightness()
                            $convertedPixels++
                        }
                    }

                    $output.SetPixel($x, $y, $replacement)
                }
            }

            $temporaryPath = "$($icon.FullName).branding.tmp.png"
            $output.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Move-Item -LiteralPath $temporaryPath -Destination $icon.FullName -Force

            $smallPath = Join-Path $resolvedDirectory "$($icon.BaseName)_16.png"
            Export-SmallIcon -Source $output -DestinationPath $smallPath
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $source.Dispose()
        $sourceStream.Dispose()
    }
}

Write-Host "Matiz roxo convertido para laranja em $convertedPixels pixels de $($icons.Count) ícones. Demais pixels preservados. Diretório: $resolvedDirectory"
