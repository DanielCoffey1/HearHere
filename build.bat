@echo off
setlocal

echo === Publishing HearHere ===
dotnet publish src\HearHere\HearHere.csproj -c Release -o publish
if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)

echo.
echo === Building Installer ===
where iscc >nul 2>nul
if errorlevel 1 (
    echo Inno Setup compiler (iscc) not found on PATH.
    echo Install Inno Setup from https://jrsoftware.org/isdownload.php
    echo Then re-run this script, or run manually:
    echo   iscc installer\HearHere.iss
    exit /b 1
)

iscc installer\HearHere.iss
if errorlevel 1 (
    echo ERROR: Inno Setup compilation failed.
    exit /b 1
)

echo.
echo === Done ===
echo Installer: publish\HearHereSetup.exe
