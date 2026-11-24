; ClipBind PDF インストーラー (Inno Setup 6)
; ビルド: build.ps1 を実行するか、dist\app を用意してから ISCC.exe installer\ClipBindPDF.iss

#define AppName "ClipBind PDF"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExe "ClipBind PDF.exe"
#define AppPublisher "Kou-256"
#define AppUrl "https://github.com/Kou-256/ClipBind-PDF"

[Setup]
AppId={{6F0E2B6C-8A51-4D1B-9C3E-5B7A2F4C9D10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Kou Hashizume
; インストール時に MIT ライセンスを表示
LicenseFile=..\LICENSE
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; 管理者権限なしで自分用にインストールできる (必要ならすべてのユーザー用も選べる)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=ClipBindPDF-Setup-{#AppVersion}
SetupIconFile=..\ClipBind.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto
CloseApplications=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
en.LaunchApp=Launch ClipBind PDF
ja.LaunchApp=ClipBind PDF を起動する
en.AppComment=Combine clipboard images into one PDF
ja.AppComment=クリップボードの画像をまとめて 1 つの PDF に

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppComment}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppComment}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
