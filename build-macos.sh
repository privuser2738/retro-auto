#!/bin/bash
# RetroAuto macOS Build Script
# Builds for both Intel (x64) and Apple Silicon (ARM64)

set -e

echo "=== RetroAuto macOS Build Script ==="
echo ""

PROJECT="RetroAuto.CrossPlatform.csproj"
OUTPUT_DIR="dist/macos"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

echo "Building for macOS x64 (Intel)..."
dotnet publish "$PROJECT" \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/x64"

echo ""
echo "Building for macOS ARM64 (Apple Silicon)..."
dotnet publish "$PROJECT" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/arm64"

echo ""
echo "=== Build Complete ==="
echo ""
echo "macOS Intel (x64):        $OUTPUT_DIR/x64/RetroAuto"
echo "macOS Apple Silicon:      $OUTPUT_DIR/arm64/RetroAuto"
echo ""

# Make executables
chmod +x "$OUTPUT_DIR/x64/RetroAuto" 2>/dev/null || true
chmod +x "$OUTPUT_DIR/arm64/RetroAuto" 2>/dev/null || true

# Show file sizes
echo "File sizes:"
ls -lh "$OUTPUT_DIR/x64/RetroAuto" 2>/dev/null || echo "  x64: Not found"
ls -lh "$OUTPUT_DIR/arm64/RetroAuto" 2>/dev/null || echo "  arm64: Not found"

echo ""
echo "Note: To run on macOS, you may need to allow the app in"
echo "System Preferences > Security & Privacy after first run."
