@echo off
REM Build and Package Script for VirtualDrive
REM This script builds the project in Release mode and creates a NuGet package

setlocal enabledelayedexpansion

echo.
echo ========================================
echo  VirtualDrive - Build & Pack
echo ========================================
echo.

REM Check if we're in the correct directory
if not exist "VirtualDrive.sln" (
    echo Error: VirtualDrive.sln not found in current directory
    echo Please run this script from the project root
    pause
    exit /b 1
)

REM Create publish directory if it doesn't exist
if not exist "publish" mkdir publish

echo [1/3] Cleaning previous builds...
dotnet clean -v minimal
if !errorlevel! neq 0 (
    echo Error: Clean failed
    pause
    exit /b 1
)

echo.
echo [2/3] Building Release configuration...
dotnet build -c Release -v minimal
if !errorlevel! neq 0 (
    echo Error: Build failed
    pause
    exit /b 1
)

echo.
echo [3/3] Creating NuGet package...
dotnet pack VirtualDrive.Core/VirtualDrive.Core.csproj -c Release -o ./publish -v minimal
if !errorlevel! neq 0 (
    echo Error: Pack failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Build and Pack Completed Successfully!
echo ========================================
echo.
echo Package location: publish\VirtualDrive.Core.1.0.0.nupkg
echo.
echo Next steps:
echo  1. Run: publish-to-nuget.bat
echo  2. Or manually upload the .nupkg file to NuGet.org
echo.
pause
