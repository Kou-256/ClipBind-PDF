; ClipBind PDF インストーラー (Inno Setup 6)
; ビルド: build.ps1 を実行するか、dist\app を用意してから ISCC.exe installer\ClipBindPDF.iss
; 流れ: 言語の選択 → インストール先の指定 → ショートカットの選択 → インストール → 完了 (アイコン入りの完了画面)
; 完了画面の画像: python scripts/make-installer-art.py

#ifdef TestBuild
  ; 動作確認用: 本物のインストールやショートカットと重ならない名前と AppId
  #define AppName "ClipBind PDF Test"
#else
  #define AppName "ClipBind PDF"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExe "ClipBind PDF.exe"
#define AppPublisher "Kou Hashizume"
#define AppUrl "https://github.com/Kou-256/ClipBind-PDF"

[Setup]
#ifdef TestBuild
AppId=ClipBindPDF.Test
#else
AppId={{6F0E2B6C-8A51-4D1B-9C3E-5B7A2F4C9D10}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://clipbind-pdf.vercel.app/
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Kou Hashizume
; 管理者権限なしで %LOCALAPPDATA%\Programs にインストール
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableDirPage=no
DisableWelcomePage=yes
DisableReadyPage=yes
; 前回の選択を引き継がず、毎回 既定 (デスクトップのみオン) から始める
UsePreviousTasks=no
DisableProgramGroupPage=yes
DefaultGroupName={#AppName}
ShowLanguageDialog=yes
LanguageDetectionMethod=none
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
#ifdef TestBuild
OutputBaseFilename=ClipBindPDF-Setup-Test
#else
OutputBaseFilename=ClipBindPDF-Setup-{#AppVersion}
#endif
SetupIconFile=..\ClipBind.ico
; 完了画面の左側: ブランドのグラデーションにアイコンと名前
WizardImageFile=wizard-164.png,wizard-246.png,wizard-328.png,wizard-410.png,wizard-492.png
WizardSmallImageFile=..\ClipBind.png
WizardImageBackColor=$5A2913
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
#if Ver >= EncodeVer(6, 6, 0)
WizardStyle=modern light excludelightcontrols
#else
WizardStyle=modern
#endif
CloseApplications=yes
RestartApplications=no

[Languages]
; 先頭の日本語が言語選択の既定
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ja.AppComment=クリップボードの画像をまとめて 1 つの PDF に
en.AppComment=Combine clipboard images into one PDF
ja.StartMenuShortcut=スタートメニューに登録する
en.StartMenuShortcut=Add to the Start menu

[Tasks]
; デスクトップのみ既定でオン
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: startmenuicon; Description: "{cm:StartMenuShortcut}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "{cm:AppComment}"; Tasks: desktopicon
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "{cm:AppComment}"; Tasks: startmenuicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DesktopCheck, StartMenuCheck, LaunchCheck: TNewCheckBox;
  UpdatingChecks: Boolean;

procedure ShortcutChanged(Sender: TObject);
var Tasks: String;
begin
  if UpdatingChecks then Exit;
  Tasks := '';
  if DesktopCheck.Checked then Tasks := 'desktopicon';
  if StartMenuCheck.Checked then begin
    if Tasks <> '' then Tasks := Tasks + ',';
    Tasks := Tasks + 'startmenuicon';
  end;
  WizardSelectTasks(Tasks);
end;

procedure LaunchChanged(Sender: TObject);
begin
  if (not UpdatingChecks) and (WizardForm.RunList.Items.Count > 0) then
    WizardForm.RunList.Checked[0] := LaunchCheck.Checked;
end;

procedure InitializeWizard();
begin
  WizardForm.WizardSmallBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage2.Visible := False;
  { ショートカットの画面: 一覧の代わりに、高 DPI でも欠けないネイティブのチェックボックスを 2 つ並べる }
  DesktopCheck := TNewCheckBox.Create(WizardForm);
  DesktopCheck.Parent := WizardForm.TasksList.Parent;
  DesktopCheck.SetBounds(WizardForm.TasksList.Left + ScaleX(8), WizardForm.TasksList.Top + ScaleY(8), WizardForm.TasksList.Width - ScaleX(16), ScaleY(28));
  DesktopCheck.Caption := ExpandConstant('{cm:CreateDesktopIcon}');
  DesktopCheck.OnClick := @ShortcutChanged;
  StartMenuCheck := TNewCheckBox.Create(WizardForm);
  StartMenuCheck.Parent := WizardForm.TasksList.Parent;
  StartMenuCheck.SetBounds(DesktopCheck.Left, DesktopCheck.Top + ScaleY(36), DesktopCheck.Width, ScaleY(28));
  StartMenuCheck.Caption := CustomMessage('StartMenuShortcut');
  StartMenuCheck.OnClick := @ShortcutChanged;
  { 完了画面の「起動する」も同じくネイティブのチェックボックスに }
  LaunchCheck := TNewCheckBox.Create(WizardForm);
  LaunchCheck.Parent := WizardForm.RunList.Parent;
  LaunchCheck.SetBounds(WizardForm.RunList.Left + ScaleX(8), WizardForm.RunList.Top + ScaleY(8), WizardForm.RunList.Width - ScaleX(16), ScaleY(28));
  LaunchCheck.Caption := ExpandConstant('{cm:LaunchProgram,{#AppName}}');
  LaunchCheck.OnClick := @LaunchChanged;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  UpdatingChecks := True;
  WizardForm.WizardSmallBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage2.Visible := CurPageID = wpFinished;
  if CurPageID = wpSelectTasks then begin
    DesktopCheck.Checked := WizardIsTaskSelected('desktopicon');
    StartMenuCheck.Checked := WizardIsTaskSelected('startmenuicon');
    WizardForm.TasksList.Visible := False;
    { 確認画面を省いたので、この画面のボタンを「インストール」にする }
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall);
  end;
  if (CurPageID = wpFinished) and (WizardForm.RunList.Items.Count > 0) then begin
    LaunchCheck.Checked := WizardForm.RunList.Checked[0];
    WizardForm.RunList.Visible := False;
  end;
  UpdatingChecks := False;
end;
