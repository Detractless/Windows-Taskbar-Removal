@echo off
powershell.exe -NoExit -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
