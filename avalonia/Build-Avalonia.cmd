@echo off
setlocal
cd /d "%~dp0.."
dotnet build CarolusNexus\CarolusNexus.csproj -c Release
exit /b %ERRORLEVEL%
