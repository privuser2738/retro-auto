#!/bin/bash
# RomDownloader Linux/macOS Build Script

set -e

echo "=== RomDownloader Build Script ==="
echo ""

OUTPUT_DIR="dist"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Detect platform
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    RUNTIME="linux-x64"
    EXECUTABLE="RomDownloader"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    # Check for Apple Silicon
    if [[ $(uname -m) == "arm64" ]]; then
        RUNTIME="osx-arm64"
    else
        RUNTIME="osx-x64"
    fi
    EXECUTABLE="RomDownloader"
else
    echo "Unsupported platform: $OSTYPE"
    echo "Use build.bat for Windows"
    exit 1
fi

echo "Building for $RUNTIME..."
dotnet publish RomDownloader.csproj \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUTPUT_DIR"

chmod +x "$OUTPUT_DIR/$EXECUTABLE"

echo ""
echo "=== Build Complete ==="
echo ""
echo "Output: $OUTPUT_DIR/$EXECUTABLE"
echo ""
ls -lh "$OUTPUT_DIR/$EXECUTABLE"

echo ""
echo "Usage: ./$OUTPUT_DIR/$EXECUTABLE sega_saturn"
echo "       ./$OUTPUT_DIR/$EXECUTABLE list"
