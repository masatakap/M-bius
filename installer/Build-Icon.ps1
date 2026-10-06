param([string]$ImagePath=(Join-Path $PSScriptRoot '..\source\Assets\original.png'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assetDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\source\Assets'))
[IO.Directory]::CreateDirectory($assetDir) | Out-Null
$source=[Drawing.Bitmap]::FromFile([IO.Path]::GetFullPath($ImagePath))
try {
    # Preserve the supplied artwork; remove only transparent margins for an icon-sized canvas.
    $left=$source.Width;$top=$source.Height;$right=-1;$bottom=-1
    for($y=0;$y -lt $source.Height;$y++){for($x=0;$x -lt $source.Width;$x++){
        if($source.GetPixel($x,$y).A -gt 0){$left=[Math]::Min($left,$x);$top=[Math]::Min($top,$y);$right=[Math]::Max($right,$x);$bottom=[Math]::Max($bottom,$y)}
    }}
    if($right -lt $left){throw 'The supplied image is transparent'}
    $srcRect=[Drawing.Rectangle]::new($left,$top,$right-$left+1,$bottom-$top+1)
    $frames=[Collections.Generic.List[byte[]]]::new()
    $sizes=@(16,20,24,32,40,48,64,96,128,256)
    foreach($size in $sizes){
        $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scale=($size*0.84)/[Math]::Max($srcRect.Width,$srcRect.Height)
            $w=[single]($srcRect.Width*$scale);$h=[single]($srcRect.Height*$scale)
            $destRect=[Drawing.RectangleF]::new(($size-$w)/2,($size-$h)/2,$w,$h)
            $graphics.DrawImage($source,$destRect,$srcRect,[Drawing.GraphicsUnit]::Pixel)
            $memory=[IO.MemoryStream]::new()
            try {$bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png);$frames.Add($memory.ToArray())} finally {$memory.Dispose()}
            if($size -eq 256){$bitmap.Save((Join-Path $assetDir 'Mobius.png'),[Drawing.Imaging.ImageFormat]::Png)}
        }finally{$graphics.Dispose();$bitmap.Dispose()}
    }
    $stream=[IO.File]::Create((Join-Path $assetDir 'Mobius.ico'))
    $writer=[IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
        $offset=6+16*$sizes.Count
        for($i=0;$i -lt $sizes.Count;$i++){
            $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
            $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
            $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset)
            $offset+=$frames[$i].Length
        }
        foreach($frame in $frames){$writer.Write($frame)}
    }finally{$writer.Dispose();$stream.Dispose()}
    Write-Output "Icon: $($sizes -join ', ') px; supplied artwork bounds $srcRect"
}finally{$source.Dispose()}
