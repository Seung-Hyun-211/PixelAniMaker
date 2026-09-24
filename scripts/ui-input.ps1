<#
.SYNOPSIS
  Mouse/keyboard helpers for driving PixelAniMaker in patch-note demos. Dot-source this file.
.DESCRIPTION
  Screen positions are given in "shot" coordinates: pixels of a capture taken with
  capture-window.ps1 -Scale $ShotScale. Canvas positions use canvas pixels and need the canvas
  origin/pixel size measured on such a capture (Set-CanvasOrigin).
.EXAMPLE
  . ./scripts/ui-input.ps1
  Connect-App; Set-CanvasOrigin 484 216 3.6
  Send-Key 0x50; Invoke-CanvasRotate 21.5 40 19.7 52.6 -30
#>

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class UiNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, IntPtr e);
    [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, IntPtr e);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    public struct RECT { public int L, T, R, B; }
}
"@
[UiNative]::SetProcessDPIAware() | Out-Null

$script:ShotScale = 0.6
$script:Window = $null
$script:Canvas = @{ X = 0.0; Y = 0.0; Pixel = 1.0 }

function Connect-App([string] $TitlePrefix = "PixelAniMaker", [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $proc = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($TitlePrefix) } | Select-Object -First 1
        if (-not $proc) { Start-Sleep -Milliseconds 500 }
    } while (-not $proc -and (Get-Date) -lt $deadline)
    if (-not $proc) { throw "No window titled '$TitlePrefix*'." }
    [UiNative]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    $rect = New-Object UiNative+RECT
    [UiNative]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $script:Window = $rect
    Start-Sleep -Milliseconds 300
}

function Set-CanvasOrigin([double] $X, [double] $Y, [double] $Pixel) {
    $script:Canvas = @{ X = $X; Y = $Y; Pixel = $Pixel }
}

function Move-To([double] $X, [double] $Y) {
    [UiNative]::SetCursorPos([int]($script:Window.L + $X / $script:ShotScale), [int]($script:Window.T + $Y / $script:ShotScale)) | Out-Null
}

function Move-ToCanvas([double] $X, [double] $Y) {
    Move-To ($script:Canvas.X + $X * $script:Canvas.Pixel) ($script:Canvas.Y + $Y * $script:Canvas.Pixel)
}

function Send-MouseDown { [UiNative]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero) }
function Send-MouseUp { [UiNative]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero) }

function Send-Key([byte] $VirtualKey) {
    [UiNative]::keybd_event($VirtualKey, 0, 0, [IntPtr]::Zero)
    [UiNative]::keybd_event($VirtualKey, 0, 2, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}

function Invoke-Click([double] $X, [double] $Y) {
    Move-To $X $Y; Start-Sleep -Milliseconds 100
    Send-MouseDown; Send-MouseUp; Start-Sleep -Milliseconds 250
}

<# Drags along canvas points; each segment is walked pixel by pixel so no move events are lost. #>
function Invoke-CanvasDrag([double[][]] $Points) {
    Move-ToCanvas $Points[0][0] $Points[0][1]; Start-Sleep -Milliseconds 100
    Send-MouseDown; Start-Sleep -Milliseconds 40
    for ($k = 1; $k -lt $Points.Count; $k++) {
        $a = $Points[$k - 1]; $b = $Points[$k]
        $n = [Math]::Max(1, [int][Math]::Ceiling([Math]::Max([Math]::Abs($b[0] - $a[0]), [Math]::Abs($b[1] - $a[1]))))
        for ($i = 1; $i -le $n; $i++) {
            Move-ToCanvas ($a[0] + ($b[0] - $a[0]) * $i / $n) ($a[1] + ($b[1] - $a[1]) * $i / $n)
            Start-Sleep -Milliseconds 15
        }
    }
    Start-Sleep -Milliseconds 40; Send-MouseUp; Start-Sleep -Milliseconds 250
}

<# Pose mode: drags point (Ax,Ay) around the joint (Px,Py) by Degrees (clockwise on screen). #>
function Invoke-CanvasRotate([double] $Px, [double] $Py, [double] $Ax, [double] $Ay, [double] $Degrees) {
    $vx = $Ax - $Px; $vy = $Ay - $Py
    Move-ToCanvas $Ax $Ay; Start-Sleep -Milliseconds 120
    Send-MouseDown; Start-Sleep -Milliseconds 50
    $n = [Math]::Max(8, [int]([Math]::Abs($Degrees) / 4))
    for ($k = 1; $k -le $n; $k++) {
        $t = $Degrees * $k / $n * [Math]::PI / 180
        $c = [Math]::Cos($t); $s = [Math]::Sin($t)
        Move-ToCanvas ($Px + $c * $vx - $s * $vy) ($Py + $s * $vx + $c * $vy)
        Start-Sleep -Milliseconds 15
    }
    Start-Sleep -Milliseconds 50; Send-MouseUp; Start-Sleep -Milliseconds 250
}
