#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
APP="dist/NotchPrompter.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
swift build -c release --arch arm64
swift build -c release --arch x86_64
lipo -create .build/arm64-apple-macosx/release/NotchPrompter \
  .build/x86_64-apple-macosx/release/NotchPrompter -output "$APP/Contents/MacOS/NotchPrompter"
cp Info.plist "$APP/Contents/Info.plist"
ICON_SOURCE="../website/assets/icon-256.png"
if [ -f "$ICON_SOURCE" ]; then
  mkdir -p dist/AppIcon.iconset
  for SIZE in 16 32 128 256 512; do
    sips -z "$SIZE" "$SIZE" "$ICON_SOURCE" --out "dist/AppIcon.iconset/icon_${SIZE}x${SIZE}.png" >/dev/null
    DOUBLE=$((SIZE * 2))
    sips -z "$DOUBLE" "$DOUBLE" "$ICON_SOURCE" --out "dist/AppIcon.iconset/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
  done
  iconutil -c icns dist/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
fi
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
lipo "$APP/Contents/MacOS/NotchPrompter" -verify_arch arm64 x86_64
ditto -c -k --sequesterRsrc --keepParent "$APP" dist/NotchPrompter-macOS-universal.zip
(cd dist && shasum -a 256 NotchPrompter-macOS-universal.zip > SHA256-macOS.txt)
# Verify the actual distributed archive after extraction, not only the pre-ZIP app.
VERIFY_DIR=$(mktemp -d "${TMPDIR:-/tmp}/notchprompter-verify.XXXXXX")
trap 'rm -rf "$VERIFY_DIR"' EXIT
ditto -x -k dist/NotchPrompter-macOS-universal.zip "$VERIFY_DIR"
codesign --verify --deep --strict "$VERIFY_DIR/NotchPrompter.app"
lipo "$VERIFY_DIR/NotchPrompter.app/Contents/MacOS/NotchPrompter" -verify_arch arm64 x86_64
codesign -d --verbose=2 "$VERIFY_DIR/NotchPrompter.app" 2>dist/signature-verification.txt
printf '%s\n' 'Distribution: ad-hoc developer build; no Apple Developer ID; no notarization ticket.' >> dist/signature-verification.txt
echo "Built universal macOS app (Apple Silicon + Intel), ad-hoc signed; not notarized."
