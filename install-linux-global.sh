#!/bin/bash
# RetroAuto Linux Global Install Script
# Installs to /usr/local/bin (system-wide, requires sudo)

set -e

echo "=== RetroAuto Linux Global Install Script ==="
echo ""

PROJECT="RetroAuto.CrossPlatform.csproj"
INSTALL_DIR="/usr/local/bin"
BUILD_DIR="$(mktemp -d)"

# Check for root/sudo
if [[ $EUID -ne 0 ]]; then
    echo "This script requires root privileges for global installation."
    echo "Please run with sudo: sudo ./install-linux-global.sh"
    echo ""
    echo "For user-level installation (no sudo), use: ./install-linux.sh"
    exit 1
fi

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

# Build (as the original user if running under sudo)
echo "Building RetroAuto for $RID..."
if [[ -n "$SUDO_USER" ]]; then
    # Running under sudo - build as the original user
    sudo -u "$SUDO_USER" dotnet publish "$PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=false \
        -p:EnableCompressionInSingleFile=true \
        -p:CROSS_PLATFORM=true \
        -o "$BUILD_DIR"
else
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=false \
        -p:EnableCompressionInSingleFile=true \
        -p:CROSS_PLATFORM=true \
        -o "$BUILD_DIR"
fi

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

# Show version/verify
ls -lh "$INSTALL_DIR/retro-auto"
