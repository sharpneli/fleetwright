@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set SOURCE_DIR=%SCRIPT_DIR%Content\Shaders\Source
set OUTPUT_DIR=%SCRIPT_DIR%Content\Shaders\Compiled

echo Compiling shaders...
echo Source: %SOURCE_DIR%
echo Output: %OUTPUT_DIR%
echo.

set ERROR_COUNT=0
set SUCCESS_COUNT=0

for %%f in ("%SOURCE_DIR%\*.glsl") do (
    set "FILENAME=%%~nf"
    set "OUTPUT_FILE=%OUTPUT_DIR%\!FILENAME!.spv"

    echo Compiling %%~nxf...
    glslangValidator -V "%%f" -o "!OUTPUT_FILE!"

    if !ERRORLEVEL! neq 0 (
        echo   FAILED: %%~nxf
        set /a ERROR_COUNT+=1
    ) else (
        echo   OK: !FILENAME!.spv
        set /a SUCCESS_COUNT+=1
    )
)

echo.
echo Compilation complete: !SUCCESS_COUNT! succeeded, !ERROR_COUNT! failed

if !ERROR_COUNT! neq 0 (
    exit /b 1
)
