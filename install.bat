@echo off

:: Re-launch as Administrator if not already elevated
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c cd /d \"%~dp0\" && powershell -NoExit -ExecutionPolicy Bypass -File install.ps1 %*' -Verb RunAs"
    exit /b
)

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
