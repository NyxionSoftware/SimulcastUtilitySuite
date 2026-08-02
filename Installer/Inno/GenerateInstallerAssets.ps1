param(
    [Parameter(Mandatory = $true)]
    [string]$IconPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$resolvedIconPath = (Resolve-Path -LiteralPath $IconPath).Path
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$iconBytes = [System.IO.File]::ReadAllBytes($resolvedIconPath)

if ($iconBytes.Length -lt 22 -or [BitConverter]::ToUInt16($iconBytes, 2) -ne 1)
{
    throw "The application icon is not a valid ICO file."
}

$imageLength = [BitConverter]::ToInt32($iconBytes, 14)
$imageOffset = [BitConverter]::ToInt32($iconBytes, 18)

if ($imageLength -le 0 -or $imageOffset -lt 22 -or $imageOffset + $imageLength -gt $iconBytes.Length)
{
    throw "The application icon does not contain a readable image frame."
}

$imageBytes = $iconBytes[$imageOffset..($imageOffset + $imageLength - 1)]
$imageStream = New-Object System.IO.MemoryStream (,$imageBytes)

try
{
    $iconImage = [System.Drawing.Image]::FromStream($imageStream)

    try
    {
        $smallImage = New-Object System.Drawing.Bitmap 55, 55

        try
        {
            $smallGraphics = [System.Drawing.Graphics]::FromImage($smallImage)

            try
            {
                $smallGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $smallGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $smallGraphics.Clear([System.Drawing.Color]::FromArgb(25, 29, 37))
                $smallGraphics.DrawImage($iconImage, 3, 3, 49, 49)
            }
            finally
            {
                $smallGraphics.Dispose()
            }

            $smallImage.Save((Join-Path $resolvedOutputDirectory "App-Icon.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
        }
        finally
        {
            $smallImage.Dispose()
        }

        $wizardImage = New-Object System.Drawing.Bitmap 164, 314

        try
        {
            $wizardGraphics = [System.Drawing.Graphics]::FromImage($wizardImage)

            try
            {
                $wizardGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $wizardGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $backgroundRectangle = New-Object System.Drawing.Rectangle 0, 0, 164, 314
                $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $backgroundRectangle, ([System.Drawing.Color]::FromArgb(16, 18, 23)), ([System.Drawing.Color]::FromArgb(24, 19, 43)), 90

                try
                {
                    $wizardGraphics.FillRectangle($backgroundBrush, $backgroundRectangle)
                }
                finally
                {
                    $backgroundBrush.Dispose()
                }

                $glowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 124, 92, 252))

                try
                {
                    $wizardGraphics.FillEllipse($glowBrush, -46, 28, 256, 256)
                }
                finally
                {
                    $glowBrush.Dispose()
                }

                $wizardGraphics.DrawImage($iconImage, 12, 64, 140, 140)
                $accentPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 124, 92, 252)), 2

                try
                {
                    $wizardGraphics.DrawArc($accentPen, -58, 194, 220, 150, 204, 112)
                    $wizardGraphics.DrawArc($accentPen, -42, 208, 220, 150, 204, 112)
                }
                finally
                {
                    $accentPen.Dispose()
                }
            }
            finally
            {
                $wizardGraphics.Dispose()
            }

            $wizardImage.Save((Join-Path $resolvedOutputDirectory "WizardImage.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
        }
        finally
        {
            $wizardImage.Dispose()
        }
    }
    finally
    {
        $iconImage.Dispose()
    }
}
finally
{
    $imageStream.Dispose()
}
