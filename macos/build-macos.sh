#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$ROOT/macos/build"
APP="$BUILD/ClearSpace.app"
DMG_ROOT="$BUILD/dmg-root"
OUTPUT="$ROOT/macos/dist/ClearSpace-macOS-1.0.0.dmg"

rm -rf "$BUILD"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$DMG_ROOT" "$ROOT/macos/dist"

xcrun swiftc "$ROOT/macos/ClearSpaceApp.swift" -parse-as-library -O \
  -target arm64-apple-macos12.0 -framework SwiftUI -framework AppKit \
  -o "$BUILD/ClearSpace-arm64"
xcrun swiftc "$ROOT/macos/ClearSpaceApp.swift" -parse-as-library -O \
  -target x86_64-apple-macos12.0 -framework SwiftUI -framework AppKit \
  -o "$BUILD/ClearSpace-x86_64"
lipo -create "$BUILD/ClearSpace-arm64" "$BUILD/ClearSpace-x86_64" -output "$APP/Contents/MacOS/ClearSpace"
cp "$ROOT/macos/Info.plist" "$APP/Contents/Info.plist"
chmod 755 "$APP/Contents/MacOS/ClearSpace"
codesign --force --deep --sign - "$APP"

cp -R "$APP" "$DMG_ROOT/ClearSpace.app"
ln -s /Applications "$DMG_ROOT/Applications"
rm -f "$OUTPUT"
hdiutil create -volname "ClearSpace" -srcfolder "$DMG_ROOT" -ov -format UDZO "$OUTPUT"
hdiutil verify "$OUTPUT"
codesign --verify --deep --strict "$APP"
file "$APP/Contents/MacOS/ClearSpace"
shasum -a 256 "$OUTPUT"
