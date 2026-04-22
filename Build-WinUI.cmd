@echo off
setlocal
cd /d "%~dp0"
dotnet build CarolusNexus.WinUI\CarolusNexus.WinUI.csproj -c Release -p:WindowsAppSDKSelfContained=false
exit /b %ERRORLEVEL%
