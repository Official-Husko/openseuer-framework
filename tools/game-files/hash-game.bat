@echo off
rem Writes the SHA-256 hash of every file of an Obenseuer install to a list. Options: see hash-game.ps1.
rem   hash-game.bat [game folder] [list.json] [-Version 0.5.13] [-Build 26179069]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0hash-game.ps1" %*
set "CODE=%ERRORLEVEL%"
pause
exit /b %CODE%
