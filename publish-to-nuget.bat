@echo off
REM Publish to NuGet Script for VirtualDrive
REM This script publishes the NuGet package to nuget.org

setlocal enabledelayedexpansion

echo.
echo ========================================
echo  VirtualDrive - Publish to NuGet
echo ========================================
echo.

REM Check if package exists
if not exist "publish\VirtualDrive.Core.1.0.0.nupkg" (
    echo Error: NuGet package not found!
    echo Please run build-and-pack.bat first
    echo.
    pause
    exit /b 1
)

REM Check if API key was provided as parameter
if "%1"=="" (
    echo.
    echo API Key not provided as parameter.
    echo.
    echo You can provide it in two ways:
    echo   1. Interactive: Enter it when prompted below
    echo   2. Command line: publish-to-nuget.bat YOUR_API_KEY
    echo.
    echo To get your API key:
    echo   1. Go to https://www.nuget.org/account/apikeys
    echo   2. Log in with your NuGet.org account
    echo   3. Create a new key with "Push new packages and package versions" scope
    echo.
    set /p API_KEY="Enter your NuGet API Key (use Ctrl+C to cancel): "
) else (
    set API_KEY=%1
)

if "!API_KEY!"=="" (
    echo Error: API Key is required
    echo Publish cancelled
    pause
    exit /b 1
)

echo.
echo Publishing package to NuGet.org...
echo Package: publish\VirtualDrive.Core.1.0.0.nupkg
echo.

REM Publish to NuGet.org
dotnet nuget push "publish\VirtualDrive.Core.1.0.0.nupkg" ^
    --api-key !API_KEY! ^
    -s https://api.nuget.org/v3/index.json

if !errorlevel! neq 0 (
    echo.
    echo Error: Publish failed
    echo Please check your API key and try again
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Package Published Successfully!
echo ========================================
echo.
echo Your package should be available shortly at:
echo https://www.nuget.org/packages/VirtualDrive.Core/
echo.
pause
