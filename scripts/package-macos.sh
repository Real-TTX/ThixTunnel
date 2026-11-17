#!/bin/sh
# Packs a published macOS build into ThixTunnel.app, a zip (for the update of installed versions) and a disk image
# (for the first installation: drag ThixTunnel into "Programme").
#
#   scripts/package-macos.sh <publish folder> <rid: osx-arm64|osx-x64> <version> <build number> <output folder>
#
# Runs on macOS (sips, iconutil, codesign, ditto, hdiutil). Without an Apple Developer ID the app is signed ad hoc
# (needed on Apple Silicon); Gatekeeper then asks once (Rechtsklick > Öffnen, or Systemeinstellungen > Datenschutz).
set -eu

publish="$1"
rid="$2"
version="$3"
build="$4"
out="$5"

root="$(cd "$(dirname "$0")/.." && pwd)"
arch="${rid#osx-}"
major=$(sed -n 's:.*<ThixTunnelMajor>\(.*\)</ThixTunnelMajor>.*:\1:p' "$root/Directory.Build.props")
minor=$(sed -n 's:.*<ThixTunnelMinor>\(.*\)</ThixTunnelMinor>.*:\1:p' "$root/Directory.Build.props")
work="$(mktemp -d)"
app="$work/ThixTunnel.app"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$out"
cp -R "$publish/." "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/ThixTunnel"

# App icon from the branding PNG (scripts/build-icons.ps1 renders it).
iconset="$work/ThixTunnel.iconset"
mkdir -p "$iconset"
source_png="$root/branding/png/thixtunnel-512.png"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$source_png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  if [ "$double" -le 512 ]; then
    sips -z "$double" "$double" "$source_png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  fi
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/ThixTunnel.icns"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key>
  <string>info.schmoldt.thixtunnel</string>
  <key>CFBundleName</key>
  <string>ThixTunnel</string>
  <key>CFBundleDisplayName</key>
  <string>ThixTunnel</string>
  <key>CFBundleExecutable</key>
  <string>ThixTunnel</string>
  <key>CFBundleIconFile</key>
  <string>ThixTunnel</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${major}.${minor}.${build}</string>
  <key>CFBundleVersion</key>
  <string>${build}</string>
  <key>CFBundleGetInfoString</key>
  <string>ThixTunnel ${version}</string>
  <key>NSHumanReadableCopyright</key>
  <string>© Matthias Schmoldt</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.developer-tools</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>CFBundleDevelopmentRegion</key>
  <string>de</string>
</dict>
</plist>
PLIST

# Ad-hoc signature: Apple Silicon runs only signed code. A Developer ID signature + notarization can replace it later.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

# Update package (MacAppBundle unpacks it with ditto) and disk image for the first installation.
ditto -c -k --keepParent "$app" "$out/ThixTunnel-macOS-${arch}.zip"

dmg_root="$work/dmg"
mkdir -p "$dmg_root"
cp -R "$app" "$dmg_root/"
ln -s /Applications "$dmg_root/Programme"
hdiutil create -volname "ThixTunnel" -srcfolder "$dmg_root" -ov -format UDZO "$out/ThixTunnel-macOS-${arch}.dmg" >/dev/null

rm -rf "$work"
ls -l "$out"
