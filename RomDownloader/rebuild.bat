@echo off
REM RomDownloader Clean Rebuild Script

echo === RomDownloader Clean Rebuild ===
echo.

echo Cleaning build artifacts...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "dist" rmdir /s /q "dist"

echo Restoring packages...
dotnet restore

echo.
call build.bat
