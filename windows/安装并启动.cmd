@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0安装并启动.ps1"
if errorlevel 1 pause
