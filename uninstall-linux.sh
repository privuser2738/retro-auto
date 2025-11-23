#!/bin/bash
# RetroAuto Linux Uninstall Script
# Removes retro-auto from common install locations

echo "=== RetroAuto Linux Uninstall Script ==="
echo ""

LOCAL_PATH="$HOME/.local/bin/retro-auto"
GLOBAL_PATH="/usr/local/bin/retro-auto"

removed=0

# Check local installation
if [[ -f "$LOCAL_PATH" ]]; then
    echo "Found local installation: $LOCAL_PATH"
    rm -f "$LOCAL_PATH"
    echo "  Removed."
    removed=1
fi

# Check global installation
if [[ -f "$GLOBAL_PATH" ]]; then
    echo "Found global installation: $GLOBAL_PATH"
    if [[ $EUID -ne 0 ]]; then
        echo "  Requires sudo to remove. Run: sudo rm $GLOBAL_PATH"
    else
        rm -f "$GLOBAL_PATH"
        echo "  Removed."
        removed=1
    fi
fi

echo ""
if [[ $removed -eq 0 ]]; then
    echo "No RetroAuto installation found."
else
    echo "Uninstall complete."
fi
