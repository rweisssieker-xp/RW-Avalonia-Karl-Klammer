@echo off
setlocal
cd /d "%~dp0"
dotnet build CarolusNexus\CarolusNexus.csproj -c Release || exit /b 1
dotnet exec "%~dp0CarolusNexus\bin\Release\net10.0-windows\CarolusNexus.dll"
exit /b %ERRORLEVEL%
