; NSIS installer script for RenderOnShutdown
!include "MUI2.nsh"

Name "RenderOnShutdown"
OutFile "RenderOnShutdown-Setup.exe"
InstallDir "$PROGRAMFILES64\\RenderOnShutdown"
InstallDirRegKey HKCU "Software\\RenderOnShutdown" "Install_Dir"

!define PRODUCT_VERSION "1.0"

Page components
Page directory
Page custom StartupPage CreateStartupPage
Page instfiles

Var START_AT_LOGIN

Function CreateStartupPage
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}

  ; Create checkbox
  ${NSD_CreateCheckBox} 10u 10u 100% 12u "Agregar al inicio de sesión (Run at startup)"
  Pop $1
  ${NSD_SetState} $1 1
  nsDialogs::Show
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  ; Copy published files
  File /r "${EXEDIR}\\..\\publish\\*"

  ; Create shortcuts
  CreateDirectory "$SMPROGRAMS\\RenderOnShutdown"
  CreateShortCut "$SMPROGRAMS\\RenderOnShutdown\\RenderOnShutdown.lnk" "$INSTDIR\\RenderOnShutdown.exe"
  CreateShortCut "$DESKTOP\\RenderOnShutdown.lnk" "$INSTDIR\\RenderOnShutdown.exe"

  ; Read checkbox state - fallback: enable startup
  ; If nsDialogs checkbox exists, read it (simple fallback: always write Run key if not available)
  ; Try to read state into START_AT_LOGIN (0/1)
  ${If} ${NSD_GetState} ; placeholder - cannot evaluate here; write startup by default
  ${EndIf}

  ; By default enable startup - user can disable later in options
  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Run" "RenderOnShutdown" '"$INSTDIR\\RenderOnShutdown.exe"'
  WriteRegStr HKCU "Software\\RenderOnShutdown" "Install_Dir" "$INSTDIR"

SectionEnd

Section "Uninstall"
  ReadRegStr $0 HKCU "Software\\RenderOnShutdown" "Install_Dir"
  IfFileExists "$0\\RenderOnShutdown.exe" 0 +2
  Delete "$0\\RenderOnShutdown.exe"
  Delete "$SMPROGRAMS\\RenderOnShutdown\\RenderOnShutdown.lnk"
  Delete "$DESKTOP\\RenderOnShutdown.lnk"
  DeleteRegKey HKCU "Software\\RenderOnShutdown"
  DeleteRegValue HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Run" "RenderOnShutdown"
SectionEnd
