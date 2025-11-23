#!/bin/bash
# RetroAuto Linux Install Script
# Installs to ~/.local/bin (user-level, no sudo required)

set -e

echo "=== RetroAuto Linux Install Script ==="
echo ""

PROJECT="RetroAuto.CrossPlatform.csproj"
INSTALL_DIR="$HOME/.local/bin"
BUILD_DIR="$(mktemp -d)"

# Detect architecture
ARCH=$(uname -m)
case "$ARCH" in
    x86_64)
        RID="linux-x64"
        ;;
    aarch64|arm64)
        RID="linux-arm64"
        ;;
    *)
        echo "Error: Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

echo "Detected architecture: $ARCH ($RID)"
echo "Install directory: $INSTALL_DIR"
echo ""

# Ensure install directory exists
mkdir -p "$INSTALL_DIR"

# Check if ~/.local/bin is in PATH
if [[ ":$PATH:" != *":$HOME/.local/bin:"* ]]; then
    echo "Warning: $INSTALL_DIR is not in your PATH"
    echo "Add this to your shell profile (~/.bashrc, ~/.zshrc, etc.):"
    echo ""
    echo '  export PATH="$HOME/.local/bin:$PATH"'
    echo ""
fi

# Build
echo "Building RetroAuto for $RID..."
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:EnableCompressionInSingleFile=true \
    -p:CROSS_PLATFORM=true \
    -o "$BUILD_DIR"

echo ""

# Install
echo "Installing to $INSTALL_DIR..."
cp "$BUILD_DIR/RetroAuto" "$INSTALL_DIR/retro-auto"
chmod +x "$INSTALL_DIR/retro-auto"

# Cleanup
rm -rf "$BUILD_DIR"

echo ""
echo "=== Installation Complete ==="
echo ""
echo "RetroAuto installed to: $INSTALL_DIR/retro-auto"
echo ""
echo "Run with: retro-auto"
echo "Or:       retro-auto all"
echo ""

# Verify installation
if command -v retro-auto &> /dev/null; then
    echo "Verification: retro-auto is accessible in PATH"
else
    echo "Note: You may need to restart your shell or run:"
    echo '  export PATH="$HOME/.local/bin:$PATH"'
fi
