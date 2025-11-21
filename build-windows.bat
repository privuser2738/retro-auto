@echo off
REM RetroAuto Windows Build Script
REM Builds native Windows x64 executable with Windows Forms support

echo === RetroAuto Windows Build Script ===
echo.

set PROJECT=RetroAuto.csproj
set OUTPUT_DIR=dist\windows

REM Clean previous builds
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

echo Building for Windows x64...
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUTPUT_DIR%"

echo.
echo === Build Complete ===
echo.
echo Output: %OUTPUT_DIR%\RetroAuto.exe
echo.

REM Show file size
dir "%OUTPUT_DIR%\RetroAuto.exe"

echo.
echo Note: This Windows build includes full GUI popup and window memory features.
pause
