[Setup]
AppName=Frikadellen BAF
AppVersion={#AppVersion}
AppPublisher=TreXito
DefaultDirName={pf}\Frikadellen BAF
DefaultGroupName=Frikadellen BAF
OutputDir=installer\output
OutputBaseFilename=FrikadellenBAF_Setup_{#AppVersion}
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
LicenseFile=

[Files]
; Copy everything from staging\app (provided via preprocessor define at build time) to the installation folder
Source: "{#StagingPath}\\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Frikadellen BAF"; Filename: "{app}\Frikadellen.UI.exe"
Name: "{commondesktop}\Frikadellen BAF"; Filename: "{app}\Frikadellen.UI.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
; Optionally run the UI on finish
Filename: "{app}\Frikadellen.UI.exe"; Description: "Launch Frikadellen UI"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; No custom preprocessor macros — staging folder is provided by the packaging script at
; installer\staging\app
