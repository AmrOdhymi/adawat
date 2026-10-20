#!/bin/bash
# Fetches the LAME MP3 encoder source the app builds into libadawat-mp3.so. LGPL 2.0, from lame.sourceforge.io.
set -euo pipefail
cd "$(dirname "$0")/.."
target=app/src/main/cpp/lame
[ -f "$target/libmp3lame/lame.c" ] && exit 0
version=3.100
sha=ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
curl -sSfL -o "$work/lame.tar.gz" "https://downloads.sourceforge.net/project/lame/lame/$version/lame-$version.tar.gz"
echo "$sha  $work/lame.tar.gz" | shasum -a 256 -c - >/dev/null
tar -xzf "$work/lame.tar.gz" -C "$work"
mkdir -p "$target"
cp -R "$work/lame-$version/libmp3lame" "$work/lame-$version/include" "$work/lame-$version/COPYING" "$target/"
echo "LAME $version in $target"
