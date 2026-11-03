@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" exit /b 1
"%CSC%" /nologo /target:winexe /platform:x64 /main:MaxsunRgb.Background /out:MaxsunRGBBackground.exe /r:System.Web.Extensions.dll EneDevice.cs Probe.cs Background.cs
if errorlevel 1 pause
endlocal
