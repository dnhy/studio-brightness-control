[Setup]
AppName=BrightnessApp
AppVersion=1.0
DefaultDirName={pf}\BrightnessApp
DefaultGroupName=BrightnessApp
OutputDir=Output
OutputBaseFilename=BrightnessAppInstaller
Compression=lzma
SolidCompression=yes

[Files]
Source: "bin\Release\BrightnessApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\*.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\BrightnessApp"; Filename: "{app}\BrightnessApp.exe"
Name: "{group}\Uninstall BrightnessApp"; Filename: "{uninstallexe}"
