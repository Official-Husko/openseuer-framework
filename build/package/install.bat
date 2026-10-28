@echo off
rem Installs ObenseuerExtended into Obenseuer.
rem Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "D:\Games\Obenseuer".
setlocal
set "HERE=%~dp0"
set "OBEN=%HERE%tool\win-x64\oben.exe"

echo ObenseuerExtended installer
echo.
echo This adds the mod loader and the mods to Obenseuer. Close the game first.
echo It looks for the game in Heroic, in your Steam libraries and in OBENSEUER_DIR.
echo Installing again updates the mods and keeps your settings.
echo.

"%OBEN%" install --mods "%HERE%Mods" %*
if errorlevel 1 (
    echo.
    echo The installation didn't finish. See the message above.
    pause
    exit /b 1
)
echo.
echo Done. Start Obenseuer as usual; the mods show up in the MODS menu.
echo To take everything out again, run uninstall.bat.
pause
