@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%publish\QimiaoDaily.exe"
if not exist "%EXE%" (
  echo QimiaoDaily.exe was not found in publish.
  echo Build or publish the WPF application first.
  exit /b 1
)
start "QimiaoDaily" "%EXE%"
echo QimiaoDaily started from publish.
endlocal
