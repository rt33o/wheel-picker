param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$master = Join-Path $PSScriptRoot 'assets\branding\wheel-launch.png'
$source = [Drawing.Bitmap]::FromFile($master)
$sizes = @(16,20,24,32,40,48,64,96,128,256)
$frames = @()
try {
 foreach ($size in $sizes) {
  $bitmap = New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [Drawing.Graphics]::FromImage($bitmap)
  $attributes = New-Object Drawing.Imaging.ImageAttributes
  try {
   $graphics.Clear([Drawing.Color]::Transparent)
   $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
   $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
   $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
   $graphics.DrawImage($source,(New-Object Drawing.Rectangle(0,0,$size,$size)),0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
   $stream = New-Object IO.MemoryStream
   try {
    if ($size -eq 256) {
     $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
     [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'assets\app-icon.png'),$stream.ToArray())
     [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'PowerToysRun\wheel.png'),$stream.ToArray())
    } else {
     # Legacy DIB frames keep the tray and .NET Framework icon APIs compatible.
     $writer = New-Object IO.BinaryWriter($stream)
     $stride = $size*4; $maskStride = [int]([Math]::Ceiling($size/32.0)*4)
     $writer.Write([uint32]40); $writer.Write([int]$size); $writer.Write([int]($size*2)); $writer.Write([uint16]1); $writer.Write([uint16]32)
     $writer.Write([uint32]0); $writer.Write([uint32]($stride*$size+$maskStride*$size)); for($i=0;$i -lt 4;$i++){$writer.Write([uint32]0)}
     $bits = $bitmap.LockBits((New-Object Drawing.Rectangle(0,0,$size,$size)),[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
     try {
      $row = New-Object byte[] $stride
      for($y=$size-1;$y -ge 0;$y--){[Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($bits.Scan0,$y*$bits.Stride),$row,0,$stride);$writer.Write($row)}
     } finally {$bitmap.UnlockBits($bits)}
     for($y=$size-1;$y -ge 0;$y--){$mask=New-Object byte[] $maskStride;for($x=0;$x -lt $size;$x++){if($bitmap.GetPixel($x,$y).A -eq 0){$mask[[int][Math]::Floor($x/8)] = $mask[[int][Math]::Floor($x/8)] -bor (128 -shr ($x%8))}};$writer.Write($mask)}
     $writer.Flush()
    }
    $frames += [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}
   } finally {$stream.Dispose()}
  } finally {$attributes.Dispose();$graphics.Dispose();$bitmap.Dispose()}
 }
 $output = [IO.File]::Create((Join-Path $PSScriptRoot 'wheel.ico'))
 $writer = New-Object IO.BinaryWriter($output)
 try {
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count);$offset=6+16*$frames.Count
  foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
  foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
 } finally {$writer.Dispose();$output.Dispose()}
} finally {$source.Dispose()}
Write-Host 'Built wheel.ico (16–256 px), assets/app-icon.png and PowerToysRun/wheel.png.'
