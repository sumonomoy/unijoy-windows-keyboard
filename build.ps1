$ErrorActionPreference = "Stop"

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    throw "C# compiler not found at $cscPath"
}

# 1. Generate app.ico with simple text "বাং"
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(64, 64)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$sf.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap

$font = New-Object System.Drawing.Font("Nirmala UI", 31, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$rectF = New-Object System.Drawing.RectangleF(-4, 0, 72, 64)
$banglaText = [string][char]0x09AC + [char]0x09BE + [char]0x0982 # "বাং"
$g.DrawString($banglaText, $font, $textBrush, $rectF, $sf)

$pngStream = New-Object System.IO.MemoryStream
$bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()

$icoPath = Join-Path $PSScriptRoot "src\app.ico"
$icoStream = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($icoStream)
# ICONDIR
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
# ICONDIRENTRY
$bw.Write([byte]64)
$bw.Write([byte]64)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$pngBytes.Length)
$bw.Write([UInt32]22)
$bw.Write($pngBytes)
$bw.Flush()
$icoStream.Close()
$g.Dispose()
$bmp.Dispose()

# 2. Stop running instance if any before overwriting exe
Get-Process -Name "UnijoyKeyboard" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Compile UnijoyKeyboard.exe
$outExe = Join-Path $PSScriptRoot "UnijoyKeyboard.exe"
$setupExe = Join-Path $PSScriptRoot "UnijoyKeyboardSetup.exe"
$layoutFile = Join-Path $PSScriptRoot "Unijoy.avrolayout"
$srcFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "src\*.cs") | Select-Object -ExpandProperty FullName

$compileArgs = @(
    "/nologo",
    "/optimize+",
    "/target:winexe",
    "/out:$outExe",
    "/win32icon:$icoPath",
    "/resource:$layoutFile,UnijoyKeyboard.Unijoy.avrolayout",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll"
) + $srcFiles

Write-Host "Compiling UnijoyKeyboard.exe..."
& $cscPath $compileArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Copy-Item -Path $outExe -Destination $setupExe -Force

Write-Host "Build succeeded:"
Write-Host "  App:       $outExe"
Write-Host "  Installer: $setupExe"
