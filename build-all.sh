#!/bin/bash
# RetroAuto - Build All Platforms
# Builds for Windows, Linux, and macOS (x64 and ARM64)

set -e

echo "=== RetroAuto - Build All Platforms ==="
echo ""

PROJECT="RetroAuto.CrossPlatform.csproj"
OUTPUT_DIR="dist"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build for Linux x64
echo "Building for Linux x64..."
dotnet publish "$PROJECT" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/linux-x64"

# Build for Linux ARM64
echo "Building for Linux ARM64..."
dotnet publish "$PROJECT" \
    -c Release \
    -r linux-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/linux-arm64"

# Build for macOS x64 (Intel)
echo "Building for macOS x64 (Intel)..."
dotnet publish "$PROJECT" \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/osx-x64"

# Build for macOS ARM64 (Apple Silicon)
echo "Building for macOS ARM64 (Apple Silicon)..."
dotnet publish "$PROJECT" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$OUTPUT_DIR/osx-arm64"

echo ""
echo "=== Build Complete ==="
echo ""
echo "Output files:"
echo "  Linux x64:            $OUTPUT_DIR/linux-x64/RetroAuto"
echo "  Linux ARM64:          $OUTPUT_DIR/linux-arm64/RetroAuto"
echo "  macOS Intel (x64):    $OUTPUT_DIR/osx-x64/RetroAuto"
echo "  macOS Apple Silicon:  $OUTPUT_DIR/osx-arm64/RetroAuto"
echo ""

# Make executables
chmod +x "$OUTPUT_DIR/linux-x64/RetroAuto" 2>/dev/null || true
chmod +x "$OUTPUT_DIR/linux-arm64/RetroAuto" 2>/dev/null || true
chmod +x "$OUTPUT_DIR/osx-x64/RetroAuto" 2>/dev/null || true
chmod +x "$OUTPUT_DIR/osx-arm64/RetroAuto" 2>/dev/null || true

# Show file sizes
echo "File sizes:"
ls -lh "$OUTPUT_DIR/linux-x64/RetroAuto" 2>/dev/null || echo "  linux-x64: Not found"
ls -lh "$OUTPUT_DIR/linux-arm64/RetroAuto" 2>/dev/null || echo "  linux-arm64: Not found"
ls -lh "$OUTPUT_DIR/osx-x64/RetroAuto" 2>/dev/null || echo "  osx-x64: Not found"
ls -lh "$OUTPUT_DIR/osx-arm64/RetroAuto" 2>/dev/null || echo "  osx-arm64: Not found"
