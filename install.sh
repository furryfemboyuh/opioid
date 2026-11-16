#!/usr/bin/env bash
# Installs the `opioid` command for the current user (no sudo needed).
#
#   curl -fsSL https://raw.githubusercontent.com/furryfemboyuh/opioid/main/install.sh | bash
#
# or, from a clone of the repo:  ./install.sh

set -euo pipefail

REPO="${OPIOID_REPO:-furryfemboyuh/opioid}"
BIN="$HOME/.local/bin"
TARGET="$BIN/opioid"

mkdir -p "$BIN"

# Use the launcher next to this script if there is one, otherwise fetch it from GitHub.
here="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" 2>/dev/null && pwd || true)"
if [ -n "$here" ] && [ -f "$here/installer/opioid" ]; then
    cp "$here/installer/opioid" "$TARGET"
else
    curl -fsSL "https://raw.githubusercontent.com/$REPO/main/installer/opioid" -o "$TARGET"
fi
chmod +x "$TARGET"
echo "Installed launcher to $TARGET"

# Make sure ~/.local/bin is on PATH for future shells.
case ":$PATH:" in
    *":$BIN:"*) ;;
    *)
        line='export PATH="$HOME/.local/bin:$PATH"'
        for rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
            if [ -f "$rc" ] && ! grep -qF "$line" "$rc"; then
                printf '\n%s\n' "$line" >> "$rc"
                echo "Added ~/.local/bin to PATH in $rc"
            fi
        done
        if command -v fish >/dev/null 2>&1; then
            fish -c "fish_add_path -U $BIN" 2>/dev/null && echo "Added ~/.local/bin to fish PATH"
        fi
        echo "Open a new terminal for the 'opioid' command to be found."
        ;;
esac

# Grab the newest build now so the first launch is instant (and works offline).
"$TARGET" --update || echo "Couldn't download a build yet; 'opioid' will try again next run."

echo
echo "Done. Run:  opioid"
