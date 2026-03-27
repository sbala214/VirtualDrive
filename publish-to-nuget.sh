#!/bin/bash
# Publish to NuGet Script for VirtualDrive (Bash/Linux version)
# Alternative to publish-to-nuget.bat for Unix-like systems

set -e  # Exit on error

echo ""
echo "========================================"
echo " VirtualDrive - Publish to NuGet"
echo "========================================"
echo ""

# Check if package exists
if [ ! -f "publish/VirtualDrive.Core.1.0.0.nupkg" ]; then
    echo "Error: NuGet package not found!"
    echo "Please run build-and-pack.sh first"
    echo ""
    exit 1
fi

# Check if API key was provided as parameter
if [ -z "$1" ]; then
    echo ""
    echo "API Key not provided as parameter."
    echo ""
    echo "You can provide it in two ways:"
    echo "  1. Interactive: Enter it when prompted below"
    echo "  2. Command line: ./publish-to-nuget.sh YOUR_API_KEY"
    echo ""
    echo "To get your API key:"
    echo "  1. Go to https://www.nuget.org/account/apikeys"
    echo "  2. Log in with your NuGet.org account"
    echo "  3. Create a new key with 'Push new packages and package versions' scope"
    echo ""
    read -sp "Enter your NuGet API Key (use Ctrl+C to cancel): " API_KEY
    echo ""
else
    API_KEY=$1
fi

if [ -z "$API_KEY" ]; then
    echo "Error: API Key is required"
    echo "Publish cancelled"
    exit 1
fi

echo ""
echo "Publishing package to NuGet.org..."
echo "Package: publish/VirtualDrive.Core.1.0.0.nupkg"
echo ""

# Publish to NuGet.org
dotnet nuget push "publish/VirtualDrive.Core.1.0.0.nupkg" \
    --api-key "$API_KEY" \
    -s https://api.nuget.org/v3/index.json

if [ $? -ne 0 ]; then
    echo ""
    echo "Error: Publish failed"
    echo "Please check your API key and try again"
    exit 1
fi

echo ""
echo "========================================"
echo " Package Published Successfully!"
echo "========================================"
echo ""
echo "Your package should be available shortly at:"
echo "https://www.nuget.org/packages/VirtualDrive.Core/"
echo ""
