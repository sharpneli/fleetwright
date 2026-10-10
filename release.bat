@echo off
rem Builds the release: release\Fleetwright\ holds Fleetwright.exe, the .NET runtime (self-contained, so the player
rem needs no .NET install), the native libraries and the game assets (GameAsset in src\Fleetwright\Fleetwright.csproj).
rem The VC++ runtime (vcruntime140, msvcp140, used by SDL3_image and TracyClient) is assumed installed.
setlocal

set SCRIPT_DIR=%~dp0
set OUT=%SCRIPT_DIR%release\Fleetwright

rem Start clean, so files dropped from the build don't linger in the release.
if exist "%OUT%" rmdir /s /q "%OUT%"

dotnet publish "%SCRIPT_DIR%src\Fleetwright" -c Release -r win-x64 --self-contained -o "%OUT%"
if errorlevel 1 (
    echo Release build failed.
    exit /b 1
)

echo.
echo Release: %OUT%
