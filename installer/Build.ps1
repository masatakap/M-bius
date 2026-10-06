param(
    [Parameter(Mandatory=$true)][string]$Compiler,
    [string]$BuildRoot = (Join-Path $PSScriptRoot '..\app'),
    [string]$PayloadRoot = (Join-Path $PSScriptRoot '..'),
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\dist')
)
$ErrorActionPreference='Stop'
$project = [xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\source\VideoMosaic.csproj') -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Expected a numeric installer version in VideoMosaic.csproj' }
$sourcePath = Join-Path $PSScriptRoot '..\source\MainWindow.cs'
$line = Get-Content -LiteralPath $sourcePath | Where-Object { $_ -match 'static readonly HashSet<string> Extensions' }
$extensions = [regex]::Matches($line,'"(\.[a-z0-9]+)"') | ForEach-Object { $_.Groups[1].Value }
if ($extensions.Count -lt 1) { throw 'No supported extensions found' }
$rows = foreach ($extension in $extensions) {
    'Root: HKCU; Subkey: "Software\Mobius\Capabilities\FileAssociations"; ValueType: string; ValueName: "'+$extension+'"; ValueData: "Mobius.Video"'
    'Root: HKCU; Subkey: "Software\Classes\Applications\Mobius.exe\SupportedTypes"; ValueType: string; ValueName: "'+$extension+'"; ValueData: ""'
    'Root: HKCU; Subkey: "Software\Classes\'+$extension+'\OpenWithProgids"; ValueType: string; ValueName: "Mobius.Video"; ValueData: ""; Flags: uninsdeletevalue'
    'Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\'+$extension+'\shell\Mobius"; ValueType: string; ValueData: "{cm:OpenWith}"; Flags: uninsdeletekey'
    'Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\'+$extension+'\shell\Mobius"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\Mobius.exe,0"'
    'Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\'+$extension+'\shell\Mobius"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"'
    'Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\'+$extension+'\shell\Mobius\command"; ValueType: string; ValueData: """{app}\Mobius.exe"" ""%1"""'
}
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Associations.iss') -Encoding utf8BOM
& $Compiler /Qp "/DAppVersionValue=$version" "/DBuildRoot=$([IO.Path]::GetFullPath($BuildRoot))" "/DPayloadRoot=$([IO.Path]::GetFullPath($PayloadRoot))" "/DOutputRoot=$([IO.Path]::GetFullPath($OutputRoot))" (Join-Path $PSScriptRoot 'Mobius.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed: $LASTEXITCODE" }

