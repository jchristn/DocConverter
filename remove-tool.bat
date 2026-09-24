@echo off
setlocal

rem ---------------------------------------------------------------------------
rem  remove-tool.bat [net8.0^|net10.0]
rem  remove-tool.bat --framework ^<net8.0^|net10.0^>
rem  remove-tool.bat -f ^<net8.0^|net10.0^>
rem
rem  Uninstalls the global docconv tool and deletes the locally built tool
rem  package for the given framework (artifacts\tool-packages\^<framework^>).
rem  Without a framework, the packages for every framework are deleted.
rem ---------------------------------------------------------------------------

set "ROOT_DIR=%~dp0"
set "PACKAGE_ROOT=%ROOT_DIR%artifacts\tool-packages"

call :parse_args %*
if %errorlevel% equ 2 exit /b 0
if %errorlevel% neq 0 exit /b %errorlevel%

tasklist /FI "IMAGENAME eq docconv.exe" 2>nul | find /I "docconv.exe" >nul
if %errorlevel% equ 0 (
    echo A running docconv.exe process is locking the global tool install.
    echo Wait for it to finish and rerun remove-tool.bat.
    exit /b 1
)

echo Removing docconv...
set "UNINSTALL_LOG=%TEMP%\docconv-tool-uninstall-%RANDOM%%RANDOM%.log"
dotnet tool uninstall -g DocConverter.Cli > "%UNINSTALL_LOG%" 2>&1
if errorlevel 1 (
    findstr /I /C:"could not be found" /C:"not currently installed" "%UNINSTALL_LOG%" >nul
    if errorlevel 1 (
        type "%UNINSTALL_LOG%"
        del "%UNINSTALL_LOG%" >nul 2>nul
        echo Failed to uninstall docconv.
        exit /b 1
    )
    echo docconv is not installed.
) else (
    type "%UNINSTALL_LOG%"
)
del "%UNINSTALL_LOG%" >nul 2>nul

if "%FRAMEWORK%"=="" (
    if exist "%PACKAGE_ROOT%" rmdir /s /q "%PACKAGE_ROOT%"
    echo Deleted local tool packages for all frameworks.
) else (
    if exist "%PACKAGE_ROOT%\%FRAMEWORK%" rmdir /s /q "%PACKAGE_ROOT%\%FRAMEWORK%"
    echo Deleted local tool package for %FRAMEWORK%.
)
exit /b 0

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
set "FRAMEWORK="
if "%FRAMEWORK_ARGUMENT%"=="" exit /b 0
if /I "%FRAMEWORK_ARGUMENT%"=="net8" set "FRAMEWORK=net8.0"
if /I "%FRAMEWORK_ARGUMENT%"=="net8.0" set "FRAMEWORK=net8.0"
if /I "%FRAMEWORK_ARGUMENT%"=="net10" set "FRAMEWORK=net10.0"
if /I "%FRAMEWORK_ARGUMENT%"=="net10.0" set "FRAMEWORK=net10.0"
if defined FRAMEWORK exit /b 0
echo Unsupported framework "%FRAMEWORK_ARGUMENT%".
echo Supported frameworks: net8.0, net10.0.
exit /b 1

:usage
echo Usage: %~nx0 [net8.0^|net10.0]
echo        %~nx0 --framework ^<net8.0^|net10.0^>
echo        %~nx0 -f ^<net8.0^|net10.0^>
echo Uninstalls docconv and deletes the local tool package for the given framework,
echo or for every framework when none is given.
exit /b 0
