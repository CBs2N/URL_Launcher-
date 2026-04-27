$pngPath = Join-Path $PSScriptRoot 'app.png'
$icoPath = Join-Path $PSScriptRoot 'app.ico'
if (-not (Test-Path $pngPath)) {
    Write-Error "PNG not found: $pngPath"
    exit 1
}

$pngBytes = [IO.File]::ReadAllBytes($pngPath)
$imageOffset = 6 + 16 * 1 # ICONDIR + one ICONDIRENTRY

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR
$bw.Write([UInt16]0)   # reserved
$bw.Write([UInt16]1)   # type = 1 for icon
$bw.Write([UInt16]1)   # count

# ICONDIRENTRY (single, using PNG data)
$bw.Write([byte]0)     # width (0 means 256)
$bw.Write([byte]0)     # height (0 means 256)
$bw.Write([byte]0)     # color count
$bw.Write([byte]0)     # reserved
$bw.Write([UInt16]1)   # planes
$bw.Write([UInt16]32)  # bitcount
$bw.Write([UInt32]($pngBytes.Length)) # bytes in resource
$bw.Write([UInt32]($imageOffset))    # image offset

# image data (raw PNG)
$bw.Write($pngBytes)
$bw.Flush()

[IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
Write-Host "Wrote ico: $icoPath (size: $([IO.File]::GetLength($icoPath)))"