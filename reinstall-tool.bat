@echo off
setlocal

rem ---------------------------------------------------------------------------
rem  reinstall-tool.bat [net8.0^|net10.0]
rem  reinstall-tool.bat --framework ^<net8.0^|net10.0^>
rem
rem  Uninstalls the global docconv tool (if installed), packs src\DocConverter.Cli,
rem  installs it again, and verifies it runs. Refuses to run while docconv.exe is
rem  running, because a running process locks the tool install.
rem ---------------------------------------------------------------------------

set "ROOT_DIR=%~dp0"
set "PACKAGE_ROOT=%ROOT_DIR%artifacts\tool-packages"
set "FRAMEWORK_ARGUMENT=%~1"

if /I "%FRAMEWORK_ARGUMENT%"=="--framework" (
    if "%~2"=="" (
        echo Missing framework value after --framework.
        call :usage
        exit /b 1
    )
    set "FRAMEWORK_ARGUMENT=%~2"
)

if /I "%FRAMEWORK_ARGUMENT%"=="-f" (
    if "%~2"=="" (
        echo Missing framework value after -f.
        call :usage
        exit /b 1
    )
    set "FRAMEWORK_ARGUMENT=%~2"
)

call :resolve_framework "%FRAMEWORK_ARGUMENT%"
if %errorlevel% equ 2 exit /b 0
if %errorlevel% neq 0 exit /b %errorlevel%

set "PACKAGE_SOURCE=%PACKAGE_ROOT%\%FRAMEWORK%"

echo docconv tool reinstall
echo Framework: %FRAMEWORK%
echo Package source: %PACKAGE_SOURCE%
echo.
echo [1/6] Checking for running docconv.exe processes...
tasklist /FI "IMAGENAME eq docconv.exe" 2>nul | find /I "docconv.exe" >nul
if %errorlevel% equ 0 (
    echo A running docconv.exe process is locking the global tool install.
    echo Wait for it to finish and rerun reinstall-tool.bat.
    exit /b 1
)
echo No running docconv.exe process found.
echo.

echo [2/6] Removing existing docconv global tool if present...
set "UNINSTALL_LOG=%TEMP%\docconv-tool-uninstall-%RANDOM%%RANDOM%.log"
dotnet tool uninstall -g DocConverter.Cli > "%UNINSTALL_LOG%" 2>&1
if errorlevel 1 (
    findstr /I /C:"could not be found" /C:"not currently installed" "%UNINSTALL_LOG%" >nul
    if errorlevel 1 (
        type "%UNINSTALL_LOG%"
        del "%UNINSTALL_LOG%" >nul 2>nul
        echo Failed to uninstall docconv. Ensure no docconv processes are running and rerun this script.
        exit /b 1
    )
    echo docconv is not installed; continuing...
) else (
    type "%UNINSTALL_LOG%"
)
del "%UNINSTALL_LOG%" >nul 2>nul
echo.

echo [3/6] Preparing package directory...
if exist "%PACKAGE_SOURCE%" rmdir /s /q "%PACKAGE_SOURCE%"
mkdir "%PACKAGE_SOURCE%"
if %errorlevel% neq 0 exit /b %errorlevel%
echo Package directory ready.
echo.

echo [4/6] Building docconv package for %FRAMEWORK%...
dotnet pack "%ROOT_DIR%src\DocConverter.Cli\DocConverter.Cli.csproj" --configuration Release -p:TargetFrameworks=%FRAMEWORK% --output "%PACKAGE_SOURCE%"
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)
echo Build complete.
echo.

echo [5/6] Installing docconv global tool...
dotnet tool install -g --source "%PACKAGE_SOURCE%" --framework %FRAMEWORK% --disable-parallel DocConverter.Cli
if %errorlevel% neq 0 exit /b %errorlevel%
echo Install complete.
echo.

echo [6/6] Verifying docconv command...
docconv --version
exit /b %errorlevel%

:resolve_framework
set "FRAMEWORK=%~1"
if /I "%FRAMEWORK%"=="/?" (
    call :usage
    exit /b 2
)
if /I "%FRAMEWORK%"=="-h" (
    call :usage
    exit /b 2
)
if /I "%FRAMEWORK%"=="--help" (
    call :usage
    exit /b 2
)

if "%FRAMEWORK%"=="" (
    dotnet --list-sdks | findstr /B /C:"10." >nul
    if errorlevel 1 (
        set "FRAMEWORK=net8.0"
    ) else (
        set "FRAMEWORK=net10.0"
    )
    exit /b 0
)

if /I "%FRAMEWORK%"=="net8" set "FRAMEWORK=net8.0"
if /I "%FRAMEWORK%"=="net8.0" exit /b 0
if /I "%FRAMEWORK%"=="net10" set "FRAMEWORK=net10.0"
if /I "%FRAMEWORK%"=="net10.0" exit /b 0

echo Unsupported framework "%~1".
echo Supported frameworks: net8.0, net10.0.
echo Use net8.0 on systems without a .NET 10 SDK.
exit /b 1

:usage
echo Usage: %~nx0 [net8.0^|net10.0]
echo        %~nx0 --framework ^<net8.0^|net10.0^>
echo Defaults to net10.0 when a .NET 10 SDK is installed, otherwise net8.0.
exit /b 0
