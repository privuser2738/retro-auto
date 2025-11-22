@echo off
REM RomDownloader Windows Build Script

echo === RomDownloader Build Script ===
echo.

set OUTPUT_DIR=dist

REM Clean previous builds
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

echo Building for Windows x64...
dotnet publish RomDownloader.csproj ^
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
echo Output: %OUTPUT_DIR%\RomDownloader.exe
echo.

dir "%OUTPUT_DIR%\RomDownloader.exe"

echo.
echo Usage: RomDownloader.exe sega_saturn
echo        RomDownloader.exe list
pause
