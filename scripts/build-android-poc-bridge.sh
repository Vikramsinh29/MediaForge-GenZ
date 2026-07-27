#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 --ffmpeg-install PATH --ndk PATH --bridge-source PATH --output PATH --api LEVEL"
}

ffmpeg_install=""
ndk_root=""
bridge_source=""
output_root=""
android_api=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ffmpeg-install) ffmpeg_install="$2"; shift 2 ;;
    --ndk) ndk_root="$2"; shift 2 ;;
    --bridge-source) bridge_source="$2"; shift 2 ;;
    --output) output_root="$2"; shift 2 ;;
    --api) android_api="$2"; shift 2 ;;
    *) usage; exit 2 ;;
  esac
done

if [[ -z "$ffmpeg_install" || -z "$ndk_root" || -z "$bridge_source" ||
      -z "$output_root" || ! "$android_api" =~ ^[0-9]+$ ]]; then
  usage
  exit 2
fi

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ffmpeg_install="$(cd "$ffmpeg_install" && pwd)"
ndk_root="$(cd "$ndk_root" && pwd)"
bridge_source="$(realpath "$bridge_source")"
bridge_source_dir="$(dirname "$bridge_source")"
output_root="$(realpath -m "$output_root")"

case "$output_root/" in
  "$repository_root/"*) echo "Native output must be outside the repository."; exit 2 ;;
esac

if [[ -e "$output_root" ]]; then
  echo "Output path must not already exist: $output_root"
  exit 2
fi

toolchain="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64"
cc="$toolchain/bin/aarch64-linux-android${android_api}-clang"
readelf="$toolchain/bin/llvm-readelf"
test -x "$cc"
test -x "$readelf"
test -f "$bridge_source"
test -f "$bridge_source_dir/mediaforge_poc_bridge.h"
test -f "$ffmpeg_install/lib/libavformat.a"
test -f "$ffmpeg_install/lib/libavcodec.a"
test -f "$ffmpeg_install/lib/libswresample.a"
test -f "$ffmpeg_install/lib/libavutil.a"

mkdir -p "$output_root"
library="$output_root/libmediaforge_poc.so"

"$cc" \
  -std=c11 \
  -O2 \
  -fPIC \
  -fvisibility=hidden \
  -Wall \
  -Wextra \
  -Werror \
  -I"$bridge_source_dir" \
  -I"$ffmpeg_install/include" \
  "$bridge_source" \
  -shared \
  -Wl,-Bsymbolic \
  -Wl,--no-undefined \
  -Wl,-z,defs \
  -Wl,-z,max-page-size=16384 \
  -Wl,-soname,libmediaforge_poc.so \
  -L"$ffmpeg_install/lib" \
  -lavformat \
  -lavcodec \
  -lswresample \
  -lavutil \
  -landroid \
  -llog \
  -lz \
  -lm \
  -o "$library"

"$readelf" -h "$library"
"$readelf" -d "$library"
sha256sum "$library" | tee "$output_root/libmediaforge_poc.so.sha256"

echo "Development bridge created outside the repository: $library"
echo "Do not commit, publish, or distribute this library or an APK containing it."
