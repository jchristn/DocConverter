@echo off
setlocal

rem ---------------------------------------------------------------------------
rem  publish-nuget.bat ^<NuGet-API-key^>
rem
rem  Packs every packable project in src\DocConverter.sln in Release and publishes
rem  each package, with its .snupkg symbol package, to nuget.org:
rem
rem    DocConverter       the library     (+ DocConverter.^<version^>.snupkg)
rem    DocConverter.Cli   the docconv tool (+ DocConverter.Cli.^<version^>.snupkg)
rem
rem  Test and benchmark projects are not packable and are skipped. The version
rem  comes from src\Directory.Build.props. Versions already on nuget.org are
rem  skipped rather than treated as errors, so the script is safe to re-run.
rem  Packages are written to artifacts\nuget, which is cleaned first.
rem ---------------------------------------------------------------------------

if "%~1"=="" goto :usage
if /I "%~1"=="/?" goto :usage
if /I "%~1"=="-h" goto :usage
if /I "%~1"=="--help" goto :usage
if not "%~2"=="" (
    echo Unexpected argument "%~2". Pass only the API key.
    goto :usage
)

set "APIKEY=%~1"
set "SOLUTION=src\DocConverter.sln"
set "OUTPUT=artifacts\nuget"
set "SOURCE=https://api.nuget.org/v3/index.json"
set "SYMBOL_SOURCE=https://api.nuget.org/v3/index.json"
set "CONFIG=Release"
set "PUSHED=0"

pushd "%~dp0"

echo.
echo === [1/4] Cleaning %OUTPUT% ===
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"
mkdir "%OUTPUT%"
if errorlevel 1 goto :error

echo.
echo === [2/4] Packing %SOLUTION% (%CONFIG%) ===
dotnet pack "%SOLUTION%" -c %CONFIG% -o "%OUTPUT%"
if errorlevel 1 goto :error

echo.
echo === [3/4] Verifying packages and symbol packages ===
rem  The patterns are inline because "call" would double their caret anchors.
dir /b "%OUTPUT%\*.nupkg" 2>nul | findstr /r /i /c:"^DocConverter\.[0-9]" >nul
if errorlevel 1 (
    echo Expected the DocConverter package in %OUTPUT%, found none.
    goto :error
)
dir /b "%OUTPUT%\*.nupkg" 2>nul | findstr /r /i /c:"^DocConverter\.Cli\.[0-9]" >nul
if errorlevel 1 (
    echo Expected the DocConverter.Cli package in %OUTPUT%, found none.
    goto :error
)
for %%P in ("%OUTPUT%\*.nupkg") do (
    if not exist "%%~dpnP.snupkg" (
        echo Missing symbol package for %%~nxP ^(expected %%~nP.snupkg^).
        goto :error
    )
    echo   %%~nxP  +  %%~nP.snupkg
)

echo.
echo === [4/4] Pushing to %SOURCE% ===
for %%P in ("%OUTPUT%\*.nupkg") do (
    echo.
    echo --- %%~nxP and %%~nP.snupkg
    dotnet nuget push "%%~fP" --api-key "%APIKEY%" --source "%SOURCE%" --symbol-source "%SYMBOL_SOURCE%" --symbol-api-key "%APIKEY%" --skip-duplicate
    if errorlevel 1 goto :error
    set /a PUSHED+=1
)

echo.
echo === Done. Published %PUSHED% package^(s^) with their symbol packages. ===
popd
endlocal
exit /b 0

:usage
echo Usage: publish-nuget.bat ^<NuGet-API-key^>
echo Packs DocConverter and DocConverter.Cli in Release and publishes both packages
echo and their .snupkg symbol packages to nuget.org. Already-published versions are skipped.
exit /b 1

:error
echo.
echo === Publish FAILED. ===
popd
endlocal
exit /b 1
