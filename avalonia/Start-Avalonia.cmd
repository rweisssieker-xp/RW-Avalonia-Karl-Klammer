@echo off
setlocal
cd /d "%~dp0.."
dotnet build CarolusNexus\CarolusNexus.csproj -c Release
if errorlevel 1 exit /b 1
dotnet run --project CarolusNexus\CarolusNexus.csproj -c Release --no-build
exit /b %ERRORLEVEL%
