<#
.SYNOPSIS
  Captures the PixelAniMaker main window to a PNG (used for patch-note screenshots).
.EXAMPLE
  ./scripts/capture-window.ps1 -Out doc/patchnotes/images/0001-layout.png
#>
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $TitlePrefix = "PixelAniMaker",
    [int] $TimeoutSeconds = 30,
    [double] $Scale = 0.75
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class CaptureNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    public struct RECT { public int L, T, R, B; }
}
"@
[CaptureNative]::SetProcessDPIAware() | Out-Null

# wait for the main window (the app may still be starting)
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    $proc = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($TitlePrefix) } | Select-Object -First 1
    if (-not $proc) { Start-Sleep -Milliseconds 500 }
} while (-not $proc -and (Get-Date) -lt $deadline)
if (-not $proc) { throw "No window titled '$TitlePrefix*' found within $TimeoutSeconds s." }
$rect = New-Object CaptureNative+RECT
[CaptureNative]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.R - $rect.L; $h = $rect.B - $rect.T

$full = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($full)
$hdc = $g.GetHdc()
[CaptureNative]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc); $g.Dispose()

$outW = [int]($w * $Scale); $outH = [int]($h * $Scale)
$small = New-Object System.Drawing.Bitmap $outW, $outH
$g = [System.Drawing.Graphics]::FromImage($small)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($full, 0, 0, $outW, $outH); $g.Dispose()

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$small.Save((Resolve-Path -LiteralPath (Split-Path $Out)).Path + "\" + (Split-Path $Out -Leaf), [System.Drawing.Imaging.ImageFormat]::Png)
$full.Dispose(); $small.Dispose()
"saved $Out ($outW x $outH)"
