; DucKING kurulum betiği (Inno Setup 6). Doğrudan değil, build.ps1 üzerinden derleyin:
;   powershell -File build.ps1
; build.ps1 sürümü csproj'dan okuyup MyAppVersion ve SourceDir olarak geçirir.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\app"
#endif

#define MyAppName "DucKING"
#define MyAppExe "DucKING.exe"

[Setup]
; AppId, güncellemelerin eski kurulumun üzerine yazılmasını sağlar; değiştirmeyin.
AppId={{8F3C2A9E-4B7D-4E21-9C55-2D7A1E6B9F10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppName}
AppComments={cm:AppComments}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=DucKING-Setup-{#MyAppVersion}
SetupIconFile=..\src\DucKING\Assets\ducking.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Program açıksa kurulum/kaldırma kapatılmasını ister (App.xaml.cs'teki mutex ile aynı ad).
AppMutex=DucKING.SingleInstance
CloseApplications=yes
; Kurulumun başında dil seçilir (Windows diline göre önceden işaretlenir); seçilen dil DucKING'e de geçer, bkz. [Registry].
ShowLanguageDialog=yes

[Languages]
; Adlar DucKING'in dil kodlarıyla aynıdır: {language} doğrudan programa yazılır (Core/Loc.cs).
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
tr.AppComments=Oyuncular için ücretsiz Windows DNS, DPI ve performans aracı
en.AppComments=Free Windows DNS, DPI and performance tool for gamers
ru.AppComments=Бесплатный инструмент DNS, обхода DPI и оптимизации Windows для геймеров
tr.RevertPrompt=DucKING'in yaptığı ayarlar (performans, gizlilik ve DNS) da eski haline döndürülsün mü?%n%nHayır derseniz ayarlar olduğu gibi kalır; yalnızca DPI servisleri kaldırılır.
en.RevertPrompt=Also restore the settings DucKING changed (performance, privacy and DNS)?%n%nIf you choose No, the settings stay as they are; only the DPI services are removed.
ru.RevertPrompt=Вернуть и настройки, изменённые DucKING (производительность, конфиденциальность и DNS)?%n%nЕсли выбрать «Нет», настройки останутся как есть; будут удалены только службы обхода DPI.
tr.CleanupFailed=Temizlik tamamlanamadı. Ayrıntılar: C:\ProgramData\DucKING\logs\ducking.log
en.CleanupFailed=Cleanup could not be completed. Details: C:\ProgramData\DucKING\logs\ducking.log
ru.CleanupFailed=Не удалось завершить очистку. Подробности: C:\ProgramData\DucKING\logs\ducking.log

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
; Kurulumda seçilen dil (tr / en / ru); DucKING açılışta buradan okur, program içinden değiştirilince de buraya yazar.
Root: HKLM; Subkey: "SOFTWARE\DucKING"; ValueType: string; ValueName: "Language"; ValueData: "{language}"; Flags: uninsdeletekey

[Run]
; DucKING yönetici yetkisi ister. postinstall girdileri varsayılan olarak normal kullanıcı yetkisiyle
; çalıştırılır ve Windows bunu 740 ("yükseltme gerekiyor") hatasıyla reddeder; runascurrentuser ile
; kurulumun zaten sahip olduğu yönetici yetkisiyle, ikinci bir UAC onayı olmadan başlatılır.
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Dosyalar silinmeden önce DucKING'in kendi temizliğini çalıştırır: DPI servisleri, ProxiFyre ve
// güvenlik duvarı kuralı her zaman kaldırılır; ayarlar kullanıcı isterse geri alınır.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Args: String;
  ResultCode: Integer;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  Args := '--uninstall';
  if UninstallSilent or
     (MsgBox(CustomMessage('RevertPrompt'), mbConfirmation, MB_YESNO) = IDYES) then
    Args := Args + ' --revert';

  if not Exec(ExpandConstant('{app}\{#MyAppExe}'), Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    MsgBox(CustomMessage('CleanupFailed'), mbError, MB_OK);
end;
