#!/bin/bash
# RetroAuto Linux Build Script
# Builds for both x64 and ARM64 architectures

set -e

echo "=== RetroAuto Linux Build Script ==="
echo ""

PROJECT="RetroAuto.CrossPlatform.csproj"
OUTPUT_DIR="dist/linux"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

echo "Building for Linux x64..."
dotnet publish "$PROJECT" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/x64"

echo ""
echo "Building for Linux ARM64..."
dotnet publish "$PROJECT" \
    -c Release \
    -r linux-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/arm64"

echo ""
echo "=== Build Complete ==="
echo ""
echo "Linux x64:   $OUTPUT_DIR/x64/RetroAuto"
echo "Linux ARM64: $OUTPUT_DIR/arm64/RetroAuto"
echo ""

# Make executables
chmod +x "$OUTPUT_DIR/x64/RetroAuto" 2>/dev/null || true
chmod +x "$OUTPUT_DIR/arm64/RetroAuto" 2>/dev/null || true

# Show file sizes
echo "File sizes:"
ls -lh "$OUTPUT_DIR/x64/RetroAuto" 2>/dev/null || echo "  x64: Not found"
ls -lh "$OUTPUT_DIR/arm64/RetroAuto" 2>/dev/null || echo "  arm64: Not found"
