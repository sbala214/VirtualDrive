#!/bin/bash
# Build and Package Script for VirtualDrive (Bash/Linux version)
# Alternative to build-and-pack.bat for Unix-like systems

set -e  # Exit on error

echo ""
echo "========================================"
echo " VirtualDrive - Build & Pack"
echo "========================================"
echo ""

# Check if we're in the correct directory
if [ ! -f "VirtualDrive.sln" ]; then
    echo "Error: VirtualDrive.sln not found in current directory"
    echo "Please run this script from the project root"
    exit 1
fi

# Create publish directory if it doesn't exist
mkdir -p publish

echo "[1/3] Cleaning previous builds..."
dotnet clean -v minimal

echo ""
echo "[2/3] Building Release configuration..."
dotnet build -c Release -v minimal

echo ""
echo "[3/3] Creating NuGet package..."
dotnet pack VirtualDrive.Core/VirtualDrive.Core.csproj -c Release -o ./publish -v minimal

echo ""
echo "========================================"
echo " Build and Pack Completed Successfully!"
echo "========================================"
echo ""
echo "Package location: publish/VirtualDrive.Core.1.0.0.nupkg"
echo ""
echo "Next steps:"
echo " 1. Run: ./publish-to-nuget.sh"
echo " 2. Or manually upload the .nupkg file to NuGet.org"
echo ""
