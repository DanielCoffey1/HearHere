@echo off
setlocal

set "DOTNET_EXE=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 (
    if exist "%ProgramFiles%\dotnet\dotnet.exe" (
        set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
    ) else (
        echo .NET SDK not found. Install .NET 8 SDK, then re-run this script.
        exit /b 1
    )
)

echo === Publishing HearHere ===
"%DOTNET_EXE%" publish src\HearHere\HearHere.csproj -c Release -o publish
if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)

echo.
echo === Building Installer ===
set "ISCC_EXE=iscc"
where iscc >nul 2>nul
if errorlevel 1 (
    if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
    ) else if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    ) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"
    ) else (
        echo Inno Setup compiler (iscc) not found.
        echo Install Inno Setup from https://jrsoftware.org/isdownload.php
        echo Then re-run this script.
        exit /b 1
    )
)

"%ISCC_EXE%" installer\HearHere.iss
if errorlevel 1 (
    echo ERROR: Inno Setup compilation failed.
    exit /b 1
)

echo.
echo === Done ===
echo Installer: publish\HearHereSetup.exe
