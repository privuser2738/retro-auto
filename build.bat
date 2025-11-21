@echo off
echo Building RetroAuto...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: bin\Release\net8.0-windows\win-x64\publish\RetroAuto.exe
) else (
    echo.
    echo Build failed!
)

pause
