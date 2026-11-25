; Instalador de TaskbarMin (Inno Setup 6)
; Compilar: ISCC.exe /DAppVersion=1.0.0 installer\TaskbarMin.iss
; Requiere haber ejecutado antes "dotnet publish" con salida en ..\publish

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "TaskbarMin"
#define AppExe  "TaskbarMin.exe"

[Setup]
AppId={{1DDDD983-D763-419E-B08D-A13B793D0F1F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=TaskbarMin-Setup-{#AppVersion}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "autostart";   Description: "Iniciar TaskbarMin automáticamente con Windows"; GroupDescription: "Opciones adicionales:"
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio";       GroupDescription: "Opciones adicionales:"; Flags: unchecked

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Inicio automático (misma clave que usa la propia app en su menú)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart
; Limpieza al desinstalar (también si el usuario activó el inicio automático desde la app)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: dontcreatekey uninsdeletevalue
; Configuración de la app
Root: HKCU; Subkey: "Software\{#AppName}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "Iniciar {#AppName} ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Cierre ordenado: la app devuelve la barra de tareas a su estado normal antes de salir
Filename: "{app}\{#AppExe}"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "QuitTaskbarMin"

[Code]
// Al actualizar: cerrar la versión que esté en ejecución antes de copiar archivos.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Exe: String;
begin
  Exe := ExpandConstant('{app}\{#AppExe}');
  if FileExists(Exe) then
  begin
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(800);
  end;
  Result := '';
end;
