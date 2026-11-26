# Run with Windows PowerShell 5.1 and its built-in System.Drawing.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;

public static class IconOpticalBounds {
    public static Rectangle Trim(Bitmap bitmap, int alphaFloor) {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++) {
            for (int x = 0; x < bitmap.Width; x++) {
                int alpha = bitmap.GetPixel(x, y).A;
                if (alpha == 0)
                    continue;
                if (alpha < alphaFloor) {
                    bitmap.SetPixel(x, y, Color.Transparent);
                    continue;
                }
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
'@

$source = Join-Path $PSScriptRoot '../../assets/icons/nexspace-jade-symbol.png'
$destination = Join-Path $PSScriptRoot 'src-tauri/icons'
$sizes = 16, 24, 32, 48, 64, 128, 256
$image = [Drawing.Image]::FromFile($source, $true)
$premultiplied = $null
$frames = @()
try {
    if ($image.Width -ne $image.Height -or $image.Width -lt 256 -or
        ![Drawing.Image]::IsAlphaPixelFormat($image.PixelFormat)) {
        throw 'Se requiere un original cuadrado con alfa y al menos 256 px por lado.'
    }
    # Resample associated colors so pixels outside the symbol do not tint its edges.
    $format = [Drawing.Imaging.PixelFormat]::Format32bppPArgb
    $premultiplied = [Drawing.Bitmap]::new($image.Width, $image.Height, $format)
    $inputGraphics = [Drawing.Graphics]::FromImage($premultiplied)
    try {
        $inputGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $inputGraphics.DrawImageUnscaled($image, 0, 0)
    } finally {
        $inputGraphics.Dispose()
    }
    # Trace alpha outside the symbol must not dictate its scale.
    $content = [IconOpticalBounds]::Trim($premultiplied, 4)
    if ($content.IsEmpty) { throw 'El icono no contiene una silueta visible.' }
    $sample = $content
    $sample.Inflate(2, 2) # Preserve the resampling margin around the silhouette.
    $sample = [Drawing.Rectangle]::Intersect($sample, [Drawing.Rectangle]::new(0, 0, $image.Width, $image.Height))
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, $format)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $options = [Drawing.Imaging.ImageAttributes]::new()
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $options.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $scale = $size * 0.90 / [Math]::Max($content.Width, $content.Height)
            $left = ($size - $content.Width * $scale) / 2 - ($content.Left - $sample.Left) * $scale
            $top = ($size - $content.Height * $scale) / 2 - ($content.Top - $sample.Top) * $scale
            $points = [Drawing.PointF[]]@(
                [Drawing.PointF]::new($left, $top),
                [Drawing.PointF]::new($left + $sample.Width * $scale, $top),
                [Drawing.PointF]::new($left, $top + $sample.Height * $scale)
            )
            $sourceRect = [Drawing.RectangleF]::new($sample.X, $sample.Y, $sample.Width, $sample.Height)
            $graphics.DrawImage($premultiplied, $points, $sourceRect, [Drawing.GraphicsUnit]::Pixel, $options)
            $last = $size - 1
            if ($bitmap.GetPixel(0, 0).A -or $bitmap.GetPixel(0, $last).A -or
                $bitmap.GetPixel($last, 0).A -or $bitmap.GetPixel($last, $last).A) {
                throw "La variante de $size px debe tener las cuatro esquinas transparentes."
            }
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $bytes = $stream.ToArray()
            [IO.File]::WriteAllBytes((Join-Path $destination "${size}x${size}.png"), $bytes)
            $frames += @{size = $size; bytes = $bytes}
        } finally {
            $stream.Dispose()
            $options.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
} finally {
    if ($premultiplied) { $premultiplied.Dispose() }
    $image.Dispose()
}

$ico = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $destination 'icon.ico')))
try {
    $ico.Write([uint16]0)
    $ico.Write([uint16]1)
    $ico.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        # ICO encodes a 256-pixel dimension as zero.
        $dimension = [byte]($frame.size % 256)
        $ico.Write($dimension)
        $ico.Write($dimension)
        $ico.Write([uint16]0)
        $ico.Write([uint16]1)
        $ico.Write([uint16]32)
        $ico.Write([uint32]$frame.bytes.Length)
        $ico.Write([uint32]$offset)
        $offset += $frame.bytes.Length
    }
    foreach ($frame in $frames) { $ico.Write([byte[]]$frame.bytes) }
} finally {
    $ico.Dispose()
}
