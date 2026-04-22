@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Run-RadicalPlan.ps1" %*
exit /b %ERRORLEVEL%
