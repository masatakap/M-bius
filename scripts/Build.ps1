param(
    [Parameter(Mandatory=$true)][string]$NativeRuntimeDirectory,
    [string]$Compiler
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtimeRoot = (Resolve-Path -LiteralPath $NativeRuntimeDirectory).Path
$appRoot = Join-Path $repoRoot 'app'
$nativeFiles = @('libmpv-2.dll','hap_demux.dll','avformat-62.dll','avcodec-62.dll','avutil-60.dll','swresample-6.dll')
foreach ($name in $nativeFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $runtimeRoot $name) -PathType Leaf)) { throw "Missing native dependency: $name" }
}
if ($Compiler) { $Compiler = (Resolve-Path -LiteralPath $Compiler).Path }
& dotnet publish (Join-Path $repoRoot 'source/VideoMosaic.csproj') -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -p:DebugType=None -o $appRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
foreach ($name in $nativeFiles) {
    $nativePath = Join-Path $runtimeRoot $name
    $targetPath = Join-Path $appRoot $name
    if ([IO.Path]::GetFullPath($nativePath) -ne [IO.Path]::GetFullPath($targetPath)) { Copy-Item -LiteralPath $nativePath -Destination $targetPath -Force }
}
if ($Compiler) {
    & (Join-Path $repoRoot 'installer/Build.ps1') -Compiler $Compiler -BuildRoot $appRoot -PayloadRoot $repoRoot -OutputRoot (Join-Path $repoRoot 'dist')
}
Write-Output "Möbius build complete: $appRoot"
