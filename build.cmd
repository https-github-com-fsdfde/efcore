@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& '%~dp0eng\common\build.ps1' -warnAsError $false -restore -build %*"
exit /b %ErrorLevel%
