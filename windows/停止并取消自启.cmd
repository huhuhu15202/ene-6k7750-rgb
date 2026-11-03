@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0停止并取消自启.ps1"
if errorlevel 1 pause
