<#
.SYNOPSIS
  Builds a self-contained single-file PixelAniMaker for one platform (no .NET install needed).
.EXAMPLE
  ./scripts/publish.ps1                  # Windows x64 -> publish/win-x64/PixelAniMaker.exe
  ./scripts/publish.ps1 -Runtime osx-arm64
  ./scripts/publish.ps1 -Runtime linux-x64
#>
param(
    [ValidateSet("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64")]
    [string] $Runtime = "win-x64"
)

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "publish/$Runtime"

dotnet publish (Join-Path $root "src/PixelAniMaker.App/PixelAniMaker.App.csproj") `
    -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# native debug symbols shipped by SkiaSharp/HarfBuzz are not needed to run
Get-ChildItem $out -Filter *.pdb | Remove-Item

Get-ChildItem $out | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
