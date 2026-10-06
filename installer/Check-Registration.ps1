param([ValidateSet('Before','Installed','Removed')][string]$Stage,[string]$InstallDir)
$ErrorActionPreference='Stop'
$testRoot = Join-Path $PSScriptRoot '..\tests'
$extensions=[regex]::Matches((Get-Content (Join-Path $PSScriptRoot '..\source\MainWindow.cs') | Where-Object {$_ -match 'static readonly HashSet<string> Extensions'}),'"(\.[a-z0-9]+)"') | ForEach-Object {$_.Groups[1].Value}
function Read-Value([string]$Path,[string]$Name='') {
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path)
    if(!$key){return $null}
    try {return $key.GetValue($Name)} finally {$key.Dispose()}
}
function Snapshot-Defaults {
    $snapshot=[ordered]@{}
    foreach($extension in $extensions){
        $choice="Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice"
        $snapshot[$extension]=[ordered]@{ClassDefault=(Read-Value "Software\Classes\$extension");ProgId=(Read-Value $choice 'ProgId');Hash=(Read-Value $choice 'Hash')}
    }
    $settings=Join-Path $env:LOCALAPPDATA 'VideoMosaic\settings.json'
    $snapshot['SettingsHash']=if(Test-Path -LiteralPath $settings){(Get-FileHash -LiteralPath $settings).Hash}else{$null}
    return ($snapshot | ConvertTo-Json -Depth 4 -Compress)
}
$baseline=Join-Path $testRoot 'install-baseline.json'
if($Stage -eq 'Before'){
    if(Read-Value 'Software\RegisteredApplications' 'Möbius'){throw 'Möbius already registered; do not overwrite existing installation in this test'}
    Snapshot-Defaults | Set-Content -LiteralPath $baseline -Encoding utf8
    Write-Output 'Baseline captured: extension defaults and user preferences.'
    exit
}
if((Snapshot-Defaults) -ne (Get-Content -LiteralPath $baseline -Raw).Trim()){throw 'Existing defaults or settings changed'}
if($Stage -eq 'Installed'){
    if((Read-Value 'Software\RegisteredApplications' 'Möbius') -ne 'Software\Mobius\Capabilities'){throw 'Missing registered application'}
    $command='"'+[IO.Path]::Combine($InstallDir,'Mobius.exe')+'" "%1"'
    if((Read-Value 'Software\Classes\Mobius.Video\shell\open\command') -ne $command){throw 'Incorrect ProgID open command'}
    if((Read-Value 'Software\Classes\Applications\Mobius.exe\shell\open\command') -ne $command){throw 'Incorrect application open command'}
    foreach($extension in $extensions){
        if((Read-Value 'Software\Mobius\Capabilities\FileAssociations' $extension) -ne 'Mobius.Video'){throw "Missing capability: $extension"}
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\$extension\OpenWithProgids")
        if(!$key -or $key.GetValueNames() -notcontains 'Mobius.Video'){throw "Missing Open With entry: $extension"}
        $key.Dispose()
        if((Read-Value "Software\Classes\SystemFileAssociations\$extension\shell\Mobius\command") -ne $command){throw "Incorrect context menu command: $extension"}
    }
    if(!(Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'Möbius.lnk'))){throw 'Missing Start menu shortcut'}
    $uninstall=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\{49C0D12B-00EF-43B3-BB28-6298C037054A}_is1')
    if(!$uninstall -or $uninstall.GetValue('DisplayVersion') -ne '0.8.3'){throw 'Missing installed app entry'}
    $uninstall.Dispose()
    Write-Output "PASS: $($extensions.Count) extensions, quoted commands, context menus, Start menu, uninstall registration; defaults and preferences unchanged."
}else{
    if(Read-Value 'Software\RegisteredApplications' 'Möbius'){throw 'Registered app entry remains'}
    foreach($path in @('Software\Mobius','Software\Classes\Mobius.Video','Software\Classes\Applications\Mobius.exe','Software\Microsoft\Windows\CurrentVersion\Uninstall\{49C0D12B-00EF-43B3-BB28-6298C037054A}_is1')){
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
        if($key){$key.Dispose();throw "Registry key remains: $path"}
    }
    foreach($extension in $extensions){
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\$extension\OpenWithProgids")
        if($key){try{if($key.GetValueNames() -contains 'Mobius.Video'){throw "Open With entry remains: $extension"}}finally{$key.Dispose()}}
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\SystemFileAssociations\$extension\shell\Mobius")
        if($key){$key.Dispose();throw "Context menu remains: $extension"}
    }
    if(Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'Möbius.lnk')){throw 'Start menu shortcut remains'}
    if(Test-Path -LiteralPath (Join-Path $InstallDir 'Mobius.exe')){throw 'Application remains'}
    Write-Output 'PASS: application, Start menu, uninstall entry and all association registrations removed; defaults and preferences preserved.'
}

