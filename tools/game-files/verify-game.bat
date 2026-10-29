@echo off
rem Checks an Obenseuer install against a hash list and lists every changed or missing file.
rem   verify-game.bat [game folder] [list.json]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-game.ps1" %*
set "CODE=%ERRORLEVEL%"
pause
exit /b %CODE%
