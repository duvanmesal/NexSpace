# Run with Windows PowerShell 5.1 and its built-in System.Drawing.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

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
            $bounds = [Drawing.Rectangle]::new(0, 0, $size, $size)
            $graphics.DrawImage($premultiplied, $bounds, 0, 0, $image.Width, $image.Height, [Drawing.GraphicsUnit]::Pixel, $options)
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
