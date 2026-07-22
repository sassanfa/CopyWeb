[Setup]
AppId={{7C9D98A3-9C54-4C27-9B52-2F2B2C6F4E4A}
AppName=CopyWeb
AppVersion=1.3.1
AppPublisher=SassanFa
DefaultDirName={autopf}\CopyWeb
DefaultGroupName=CopyWeb
OutputDir=output
OutputBaseFilename=CopyWeb-Setup-1.3.1
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\CopyWeb.ico

[Files]
Source: "..\bin\Release\net10.0-windows\publish-v51\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\CopyWeb"; Filename: "{app}\CopyWeb.exe"
Name: "{autodesktop}\CopyWeb"; Filename: "{app}\CopyWeb.exe"

[Run]
Filename: "{app}\CopyWeb.exe"; Description: "Launch CopyWeb"; Flags: nowait postinstall skipifsilent
