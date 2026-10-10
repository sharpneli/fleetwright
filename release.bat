@echo off
rem Builds a release folder: Fleetwright.exe, the .NET runtime (self-contained, so the player needs no .NET install),
rem the native libraries and the game assets (GameAsset in src\Fleetwright\Fleetwright.csproj).
rem   release.bat               release\Fleetwright\          for players: no console window, no Tracy
rem   release.bat DevRelease    release\Fleetwright-DevRelease\  for us: optimized, with the console and Tracy
rem The VC++ runtime (vcruntime140, msvcp140, used by SDL3_image and TracyClient) is assumed installed.
setlocal

set SCRIPT_DIR=%~dp0
set CONFIG=%~1
if "%CONFIG%"=="" set CONFIG=Release
if /i "%CONFIG%"=="Release" (
    set OUT=%SCRIPT_DIR%release\Fleetwright
) else if /i "%CONFIG%"=="DevRelease" (
    set OUT=%SCRIPT_DIR%release\Fleetwright-DevRelease
) else (
    echo Unknown configuration "%CONFIG%": use Release or DevRelease.
    exit /b 1
)

rem Start clean, so files dropped from the build don't linger in the release.
if exist "%OUT%" rmdir /s /q "%OUT%"

dotnet publish "%SCRIPT_DIR%src\Fleetwright" -c %CONFIG% -r win-x64 --self-contained -o "%OUT%"
if errorlevel 1 (
    echo Release build failed.
    exit /b 1
)

echo.
echo %CONFIG%: %OUT%
