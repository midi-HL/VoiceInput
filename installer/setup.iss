; VoiceInput 安装包配置
; Inno Setup 6 脚本

#ifndef VERSION
#define VERSION "1.0.0"
#endif

#define APP_NAME "VoiceInput"
#define APP_VERSION VERSION
#define APP_PUBLISHER "VoiceInput"
#define APP_URL "https://github.com/VoiceInput/VoiceInput"
#define APP_EXE_NAME "VoiceInput.exe"

[Setup]
; 应用信息
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#APP_NAME}
AppVersion={#APP_VERSION}
AppVerName={#APP_NAME} {#APP_VERSION}
AppPublisher={#APP_PUBLISHER}
AppPublisherURL={#APP_URL}
AppSupportURL={#APP_URL}
AppUpdatesURL={#APP_URL}

; 安装目录
DefaultDirName={autopf}\{#APP_NAME}
DefaultGroupName={#APP_NAME}
LicenseFile=..\LICENSE

; 安装包选项
OutputDir=..\dist
OutputBaseFilename=VoiceInput-Setup-v{#VERSION}-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; 系统要求
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.18362

; 外观
UninstallDisplayIcon={app}\{#APP_EXE_NAME}
UninstallDisplayName={#APP_NAME}

; 其他
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
DisableReadyPage=no
ShowLanguageDialog=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "开机自动启动"; GroupDescription: "其他选项:"; Flags: checked

[Files]
; 主程序
Source: "..\publish\{#APP_EXE_NAME}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 开始菜单
Name: "{group}\{#APP_NAME}"; Filename: "{app}\{#APP_EXE_NAME}"
Name: "{group}\{cm:UninstallProgram,{#APP_NAME}}"; Filename: "{uninstallexe}"

; 桌面快捷方式
Name: "{autodesktop}\{#APP_NAME}"; Filename: "{app}\{#APP_EXE_NAME}"; Tasks: desktopicon

; 启动文件夹（开机自启）
Name: "{userstartup}\{#APP_NAME}"; Filename: "{app}\{#APP_EXE_NAME}"; Tasks: startupicon

[Registry]
; 开机自启动（备用方式）
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VoiceInput"; ValueData: """{app}\{#APP_EXE_NAME}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; 安装完成后启动
Filename: "{app}\{#APP_EXE_NAME}"; Description: "{cm:LaunchProgram,{#StringChange(APP_NAME, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// 检查 Windows 版本
function IsWindows10OrLater: Boolean;
begin
  Result := (GetWindowsVersion >= $0A000000);
end;

// 检查语音识别语言包
procedure CheckSpeechLanguagePack;
var
  RegKey: string;
  Installed: Boolean;
begin
  RegKey := 'SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens\MSTTS_V110_zhCN_Huihui';
  Installed := RegKeyExists(HKEY_LOCAL_MACHINE, RegKey) or RegKeyExists(HKEY_CURRENT_USER, RegKey);
  
  if not Installed then
  begin
    if MsgBox('检测到尚未安装中文语音识别语言包。' + #13#10 + #13#10 +
              '本地语音识别模式需要安装对应语言包才能正常工作。' + #13#10 + #13#10 +
              '是否继续安装？（稍后可在 Windows 设置中安装语言包）',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Abort;
    end;
  end;
end;

// 卸载时询问是否保留配置
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  KeepSettings: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    KeepSettings := MsgBox('是否保留用户配置？' + #13#10 + #13#10 +
                           '选择"是"将保留您的 API 配置和偏好设置。',
                           mbConfirmation, MB_YESNO or MB_DEFBUTTON1);
    
    if KeepSettings = IDYES then
    begin
      // 保留注册表配置
      // Inno Setup 会自动处理 [Registry] 段的 uninsdeletevalue
    end
    else
    begin
      // 删除配置
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'SOFTWARE\VoiceInput');
    end;
  end;
end;

procedure InitializeWizard;
begin
  // 检查 Windows 版本
  if not IsWindows10OrLater then
  begin
    MsgBox('此应用需要 Windows 10 版本 1903 或更高版本。', mbError, MB_OK);
    Abort;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // 安装后检查语音语言包
    CheckSpeechLanguagePack;
  end;
end;
