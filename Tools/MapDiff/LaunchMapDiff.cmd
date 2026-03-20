@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "MAPDIFF_EXE=%SCRIPT_DIR%bin\Debug\net8.0-windows\MapDiff.exe"

if not exist "%MAPDIFF_EXE%" (
    echo MapDiff.exe was not found at:
    echo   %MAPDIFF_EXE%
    echo.
    echo Build the tool first with:
    echo   dotnet build Tools\MapDiff\MapDiff.csproj
    pause
    exit /b 1
)

start "MapDiff" "%MAPDIFF_EXE%" %*
