; NSIS installer script for RenderOnShutdown
Unicode true

!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

; Version can be overridden with: makensis /DPRODUCTVERSION=1.0.0 RenderOnShutdown.nsi
!ifndef PRODUCTVERSION
  !define PRODUCTVERSION "1.0"
!endif

!define APP_NAME "RenderOnShutdown"
!define APP_EXE "RenderOnShutdown.exe"
!define PUBLISH_DIR "${__FILEDIR__}\..\publish"
!define APP_ICON "${__FILEDIR__}\..\icon.ico"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

Name "${APP_NAME}"
Icon "${APP_ICON}"
UninstallIcon "${APP_ICON}"
OutFile "RenderOnShutdown-Setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_NAME}" "Install_Dir"
RequestExecutionLevel user
Unicode true

VIProductVersion "${PRODUCTVERSION}.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileVersion" "${PRODUCTVERSION}"
VIAddVersionKey "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey "LegalCopyright" ""

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
Page custom StartupPage StartupPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Spanish"

Var START_AT_LOGIN
Var START_CHECKBOX
Var START_STATE

Function StartupPage
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}

  ${NSD_CreateCheckBox} 0 0 100% 12u "Agregar al inicio de sesión (Run at startup)"
  Pop $START_CHECKBOX
  ${If} $START_AT_LOGIN == 1
    ${NSD_SetState} $START_CHECKBOX ${BST_CHECKED}
  ${EndIf}

  nsDialogs::Show
FunctionEnd

Function StartupPageLeave
  ${NSD_GetState} $START_CHECKBOX $START_STATE
  ${If} $START_STATE == ${BST_CHECKED}
    StrCpy $START_AT_LOGIN 1
  ${Else}
    StrCpy $START_AT_LOGIN 0
  ${EndIf}
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  WriteRegStr HKCU "Software\${APP_NAME}" "Install_Dir" "$INSTDIR"

  ${If} $START_AT_LOGIN == 1
    WriteRegStr HKCU "${RUN_KEY}" "${APP_NAME}" '"$INSTDIR\${APP_EXE}"'
  ${Else}
    DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
  ${EndIf}

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${PRODUCTVERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
  DeleteRegKey HKCU "Software\${APP_NAME}"
  DeleteRegKey HKCU "${UNINST_KEY}"

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  RMDir /r "$INSTDIR"
SectionEnd
