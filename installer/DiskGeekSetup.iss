; DiskGeek installer script for Inno Setup (https://jrsoftware.org/isinfo.php - free).
; Produces a single DiskGeekSetup.exe that installs DiskGeek, adds Start Menu shortcuts,
; optionally a desktop shortcut, and a proper Windows uninstaller entry.
;
; ---------------------------------------------------------------------------------------
; HOW TO USE
; ---------------------------------------------------------------------------------------
; 1. Publish a self-contained build of the app (run this from the DiskGeek repo root, the
;    folder that contains DiskGeek.sln):
;
;      dotnet publish src\DiskGeek.App\DiskGeek.App.csproj -c Release -r win-x64 ^
;        --self-contained true -o publish\win-x64
;
;    "Self-contained" bundles the .NET 8 runtime into the output, so people installing
;    DiskGeek don't need .NET already installed. It makes the installer bigger (roughly
;    100-150 MB instead of a few MB) - that trade-off is normally the right one for an app
;    aimed at ordinary users rather than developers.
;
; 2. Install Inno Setup (free): https://jrsoftware.org/isdl.php
;
; 3. Place this file (DiskGeekSetup.iss) directly in the repo root, next to DiskGeek.sln,
;    so the relative paths below ("publish\win-x64", "src\...\diskgeek.ico") resolve
;    correctly. (If you'd rather keep it in an "installer" subfolder like this one, adjust
;    SourceDir and SetupIconFile below to add the extra "..\".)
;
; 4. Open this file in the Inno Setup Compiler and press Compile (Ctrl+F9), or run it
;    from the command line:
;
;      ISCC.exe DiskGeekSetup.iss
;
;    Either way, the finished installer appears at installer\Output\DiskGeekSetup.exe.
;
; 5. Test it on a clean-ish machine or VM if you can - first-run behaviour is the easiest
;    thing to get wrong and the hardest to notice on your own dev machine.
;
; 6. Upload DiskGeekSetup.exe to https://techygeekshome.info/downloads/diskgeek/ (the
;    exact path already referenced in daappinfo.xml) and make sure the manifest's
;    <version> matches this build - see the README's "How updates reach you" section.
; ---------------------------------------------------------------------------------------

#define MyAppName "DiskGeek"
; The release workflow passes the version in with /DMyAppVersion=x.y.z so that the
; git tag is the single source of truth. The value below is only a fallback for a
; local build straight out of the Inno Setup Compiler.
#ifndef MyAppVersion
#define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "TechyGeeksHome"
#define MyAppURL "https://techygeekshome.info/diskgeek/"
#define MyAppExeName "DiskGeek.App.exe"
#define SourceDir "..\publish\win-x64"
#define IconFile "..\src\DiskGeek.App\Assets\diskgeek.ico"

[Setup]
; Unique to this app - do NOT regenerate this for future versions of DiskGeek, only for a
; genuinely different product. Windows uses it to recognise "this is an upgrade of the same
; app" rather than a separate install.
AppId={{FABE7889-47A7-4E2E-93F6-FE537C5E334D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
; Stamps the version into DiskGeekSetup.exe's own file properties, so the installer
; reports the release version in Explorer and the release workflow can verify it.
VersionInfoVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Output
OutputBaseFilename=DiskGeekSetup
SetupIconFile={#IconFile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Machine-wide install into Program Files, shared by all users on the PC - this requires
; admin rights, so Windows will show a UAC elevation prompt when the installer starts.
; {autopf} only resolves to Program Files when the install is running elevated like this;
; with the previous "lowest" setting it silently fell back to each user's own
; AppData\Local\Programs folder instead, which is what you just saw happen.
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
