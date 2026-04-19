@echo off
setlocal
cd /d "%~dp0"
dotnet run --project CarolusNexus\CarolusNexus.csproj -c Release -- --smoke
exit /b %ERRORLEVEL%
