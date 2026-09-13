@echo off
setlocal
pwsh -NoLogo -NoProfile -File "%~dp0unity-editor.ps1" %*
exit /b %ERRORLEVEL%
