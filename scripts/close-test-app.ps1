<#
.SYNOPSIS
  Closes PixelAniMaker instances started for testing and removes their autosave copies, so the next
  launch does not offer to recover test work.
#>
Get-Process | Where-Object { $_.MainWindowTitle -like 'PixelAniMaker*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$autosave = Join-Path $env:APPDATA 'PixelAniMaker\autosave'
if (Test-Path $autosave) { Get-ChildItem $autosave -File | Remove-Item -Force }
