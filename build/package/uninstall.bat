@echo off
rem Takes ObenseuerExtended out of Obenseuer again: the loader and the changes it made to Unity's startup files.
rem The Mods folder (and your mod settings) stays unless you pass --remove-mods.
setlocal
"%~dp0tool\win-x64\oben.exe" uninstall %*
pause
