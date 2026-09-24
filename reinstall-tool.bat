@echo off
setlocal

rem ---------------------------------------------------------------------------
rem  reinstall-tool.bat [net8.0^|net10.0]
rem  reinstall-tool.bat --framework ^<net8.0^|net10.0^>
rem  reinstall-tool.bat -f ^<net8.0^|net10.0^>
rem
rem  Uninstalls the global docconv tool (if installed), packs src\DocConverter.Cli
rem  for ONE target framework, installs it for that framework only, and verifies
rem  it. Refuses to run while docconv.exe is running, because a running process
rem  locks the tool install. Without a framework: net10.0 when a .NET 10 SDK is
rem  installed, otherwise net8.0.
rem ---------------------------------------------------------------------------

set "ROOT_DIR=%~dp0"
set "PACKAGE_ROOT=%ROOT_DIR%artifacts\tool-packages"
set "TOOL_HOME=%USERPROFILE%"
if defined DOTNET_CLI_HOME set "TOOL_HOME=%DOTNET_CLI_HOME%"
set "TOOL_STORE=%TOOL_HOME%\.dotnet\tools\.store\docconverter.cli"

call :parse_args %*
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
echo.

echo [4/6] Building docconv for %FRAMEWORK% only...
dotnet pack "%ROOT_DIR%src\DocConverter.Cli\DocConverter.Cli.csproj" --configuration Release -p:TargetFrameworks=%FRAMEWORK% --output "%PACKAGE_SOURCE%"
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)
echo.

echo [5/6] Installing docconv for %FRAMEWORK%...
dotnet tool install -g --source "%PACKAGE_SOURCE%" --framework %FRAMEWORK% --disable-parallel DocConverter.Cli
if %errorlevel% neq 0 exit /b %errorlevel%
echo.

echo [6/6] Verifying the install...
call :verify_install
if %errorlevel% neq 0 exit /b %errorlevel%
docconv --version
exit /b %errorlevel%

rem ---------------------------------------------------------------------------
rem  Argument parsing: [tfm] or --framework tfm or -f tfm, or help.
rem ---------------------------------------------------------------------------
:parse_args
set "FRAMEWORK_ARGUMENT="
:parse_loop
if "%~1"=="" goto parse_done
if /I "%~1"=="/?" goto parse_help
if /I "%~1"=="-h" goto parse_help
if /I "%~1"=="--help" goto parse_help
if /I "%~1"=="--framework" goto parse_option
if /I "%~1"=="-f" goto parse_option
if defined FRAMEWORK_ARGUMENT goto parse_extra
set "FRAMEWORK_ARGUMENT=%~1"
shift
goto parse_loop
:parse_option
if "%~2"=="" (
    echo Missing framework value after %~1.
    call :usage
    exit /b 1
)
if defined FRAMEWORK_ARGUMENT goto parse_extra
set "FRAMEWORK_ARGUMENT=%~2"
shift
shift
goto parse_loop
:parse_extra
echo Unexpected argument "%~1". Specify one framework.
call :usage
exit /b 1
:parse_help
call :usage
exit /b 2
:parse_done
call :resolve_framework "%FRAMEWORK_ARGUMENT%"
exit /b %errorlevel%

:resolve_framework
set "FRAMEWORK=%~1"
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
if /I "%FRAMEWORK%"=="net8.0" (
    set "FRAMEWORK=net8.0"
    exit /b 0
)
if /I "%FRAMEWORK%"=="net10" set "FRAMEWORK=net10.0"
if /I "%FRAMEWORK%"=="net10.0" (
    set "FRAMEWORK=net10.0"
    exit /b 0
)
echo Unsupported framework "%~1".
echo Supported frameworks: net8.0, net10.0.
echo Use net8.0 on systems without a .NET 10 SDK.
exit /b 1

rem ---------------------------------------------------------------------------
rem  Confirms the installed tool contains the requested framework and no other.
rem ---------------------------------------------------------------------------
:verify_install
if not exist "%TOOL_STORE%" (
    echo Note: tool store not found at %TOOL_STORE%; skipping the framework check.
    exit /b 0
)
set "FOUND_TARGET="
set "FOUND_OTHER="
for /d %%V in ("%TOOL_STORE%\*") do (
    for /d %%T in ("%%~fV\docconverter.cli\%%~nxV\tools\*") do (
        if /I "%%~nxT"=="%FRAMEWORK%" (
            set "FOUND_TARGET=1"
        ) else (
            set "FOUND_OTHER=%%~nxT"
        )
    )
)
if defined FOUND_OTHER (
    echo The installed tool also contains %FOUND_OTHER%; expected %FRAMEWORK% only.
    exit /b 1
)
if not defined FOUND_TARGET (
    echo The installed tool does not contain %FRAMEWORK%.
    exit /b 1
)
echo Installed for %FRAMEWORK% only.
exit /b 0

:usage
echo Usage: %~nx0 [net8.0^|net10.0]
echo        %~nx0 --framework ^<net8.0^|net10.0^>
echo        %~nx0 -f ^<net8.0^|net10.0^>
echo Removes docconv, then builds and installs it for the given framework only.
echo Defaults to net10.0 when a .NET 10 SDK is installed, otherwise net8.0.
exit /b 0
