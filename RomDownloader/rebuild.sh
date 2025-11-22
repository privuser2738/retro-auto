#!/bin/bash
# RomDownloader Clean Rebuild Script

set -e

echo "=== RomDownloader Clean Rebuild ==="
echo ""

echo "Cleaning build artifacts..."
rm -rf bin obj dist

echo "Restoring packages..."
dotnet restore

echo ""
./build.sh
