#!/bin/bash
set -euo pipefail

version=${1:?Release version is required.}
arch=${2:?Target CPU is required.}

case "$arch" in
  x64|arm64) ;;
  *)
    echo "Unsupported macOS target CPU: $arch" >&2
    exit 1
    ;;
esac

app_name="哔哩下载姬.app"
package_name="DownKyi-${version}-osx-${arch}.dmg"

create-dmg \
  --hdiutil-quiet \
  --icon "$app_name" 165 175 \
  --icon-size 120 \
  --app-drop-link 495 175 \
  --window-size 660 400 \
  "$package_name" "$app_name"
