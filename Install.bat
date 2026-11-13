@echo off
setlocal
chcp 65001 >nul
echo Heroes' Vow - Standalone Parry 2.0.0
echo 1 Install / 2 Remove / Q Cancel
set "choice="
set /p "choice=Select (Enter=Install): "
if /i "%choice%"=="Q" exit /b 0
set "action=Install"
if "%choice%"=="2" set "action=Remove"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" -Action %action%
if errorlevel 1 echo Installation did not complete. See the message above.
pause
