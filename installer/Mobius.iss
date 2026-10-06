; Build with Inno Setup 6.7+: ISCC.exe /DBuildRoot=... /DPayloadRoot=... /DOutputRoot=... Mobius.iss
; Per-user registration only. Never writes extension defaults or UserChoice.
#ifndef BuildRoot
  #define BuildRoot "..\app"
#endif
#ifndef PayloadRoot
  #define PayloadRoot ".."
#endif
#ifndef OutputRoot
  #define OutputRoot "..\dist"
#endif
#ifndef AppVersionValue
  #define AppVersionValue "0.8.3"
#endif

[Setup]
AppId={{49C0D12B-00EF-43B3-BB28-6298C037054A}
AppName=Möbius
AppVersion={#AppVersionValue}
AppVerName=Möbius {#AppVersionValue}
AppPublisher=Möbius
DefaultDirName={localappdata}\Programs\Mobius
DefaultGroupName=Möbius
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
UninstallDisplayName=Möbius
UninstallDisplayIcon={app}\Mobius.exe
AppMutex=Mobius.Running
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
OutputDir={#OutputRoot}
OutputBaseFilename=Mobius-Setup
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
SetupIconFile=..\source\Assets\Mobius.ico

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
japanese.OpenWith=Möbiusで開く
english.OpenWith=Open with Möbius
spanish.OpenWith=Abrir con Möbius
french.OpenWith=Ouvrir avec Möbius
portuguese.OpenWith=Abrir com Möbius
chinesesimp.OpenWith=使用 Möbius 打开

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildRoot}\*"; DestDir: "{app}"; Excludes: "verification.json,*-test.json,*.log,*.pdb,MobiusUiPreview.exe"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\TRADEMARKS.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\THIRD-PARTY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Möbius"; Filename: "{app}\Mobius.exe"; WorkingDir: "{app}"; AppUserModelID: "Mobius.Player"
Name: "{userdesktop}\Möbius"; Filename: "{app}\Mobius.exe"; WorkingDir: "{app}"; AppUserModelID: "Mobius.Player"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Mobius"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Mobius\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Möbius"
Root: HKCU; Subkey: "Software\Mobius\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Möbius multi-video player"
Root: HKCU; Subkey: "Software\Mobius\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\Mobius.exe,0"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Möbius"; ValueData: "Software\Mobius\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Mobius.Video"; ValueType: string; ValueData: "Möbius Video"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Mobius.Video"; ValueType: string; ValueName: "AppUserModelID"; ValueData: "Mobius.Player"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\Application"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Möbius"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\Application"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Möbius multi-video player"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\Application"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\Mobius.exe,0"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\DefaultIcon"; ValueType: string; ValueData: "{app}\Mobius.exe,0"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\shell\open"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKCU; Subkey: "Software\Classes\Mobius.Video\shell\open\command"; ValueType: string; ValueData: """{app}\Mobius.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\Mobius.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Möbius"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\Mobius.exe\shell\open"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKCU; Subkey: "Software\Classes\Applications\Mobius.exe\shell\open\command"; ValueType: string; ValueData: """{app}\Mobius.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\Mobius.exe"; ValueType: string; ValueData: "{app}\Mobius.exe"; Flags: uninsdeletekey
#include "Associations.iss"

[Run]
Filename: "{app}\Mobius.exe"; Description: "{cm:LaunchProgram,Möbius}"; Flags: nowait postinstall skipifsilent
