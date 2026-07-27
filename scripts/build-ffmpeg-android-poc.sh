#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 --source PATH --source-archive FILE --ndk PATH --output PATH --commit SHA --source-sha256 SHA256 --api LEVEL"
}

source_root=""
source_archive=""
ndk_root=""
output_root=""
expected_commit=""
source_sha256=""
android_api=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) source_root="$2"; shift 2 ;;
    --source-archive) source_archive="$2"; shift 2 ;;
    --ndk) ndk_root="$2"; shift 2 ;;
    --output) output_root="$2"; shift 2 ;;
    --commit) expected_commit="$2"; shift 2 ;;
    --source-sha256) source_sha256="$2"; shift 2 ;;
    --api) android_api="$2"; shift 2 ;;
    *) usage; exit 2 ;;
  esac
done

if [[ -z "$source_root" || -z "$source_archive" || -z "$ndk_root" || -z "$output_root" ||
      ! "$expected_commit" =~ ^[0-9a-fA-F]{40}$ ||
      ! "$source_sha256" =~ ^[0-9a-fA-F]{64}$ ||
      ! "$android_api" =~ ^[0-9]+$ ]]; then
  usage
  exit 2
fi

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_root="$(cd "$source_root" && pwd)"
if [[ ! -f "$source_archive" ]]; then
  echo "Pinned FFmpeg source archive is missing."
  exit 2
fi
source_archive="$(cd "$(dirname "$source_archive")" && pwd)/$(basename "$source_archive")"
ndk_root="$(cd "$ndk_root" && pwd)"
output_root="$(realpath -m "$output_root")"

case "$source_root/" in "$repository_root/"*) echo "Source must be outside the repository."; exit 2 ;; esac
case "$output_root/" in "$repository_root/"*) echo "Output must be outside the repository."; exit 2 ;; esac
mkdir -p "$output_root"

actual_commit="$(git -C "$source_root" rev-parse HEAD)"
if [[ "$actual_commit" != "$expected_commit" ]]; then
  echo "FFmpeg commit mismatch."
  exit 3
fi
if [[ -n "$(git -C "$source_root" status --porcelain)" ]]; then
  echo "FFmpeg source tree must be clean."
  exit 3
fi

actual_source_sha256="$(sha256sum "$source_archive" | awk '{print $1}')"
if [[ "$actual_source_sha256" != "${source_sha256,,}" ]]; then
  echo "FFmpeg source archive SHA-256 mismatch."
  exit 3
fi
toolchain="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64"
if [[ ! -d "$toolchain" ]]; then
  echo "Expected Linux Android NDK LLVM toolchain is missing: $toolchain"
  exit 4
fi

target="aarch64-linux-android"
export CC="$toolchain/bin/${target}${android_api}-clang"
export CXX="$toolchain/bin/${target}${android_api}-clang++"
export AR="$toolchain/bin/llvm-ar"
export NM="$toolchain/bin/llvm-nm"
export RANLIB="$toolchain/bin/llvm-ranlib"
export STRIP="$toolchain/bin/llvm-strip"

build_root="$output_root/build"
install_root="$output_root/install"
if [[ -e "$build_root" || -e "$install_root" ]]; then
  echo "Build and install paths must not already exist."
  exit 4
fi
mkdir -p "$build_root" "$install_root"
cd "$build_root"

"$source_root/configure" \
  --prefix="$install_root" \
  --target-os=android \
  --arch=aarch64 \
  --cc="$CC" \
  --cxx="$CXX" \
  --ar="$AR" \
  --nm="$NM" \
  --ranlib="$RANLIB" \
  --strip="$STRIP" \
  --disable-gpl \
  --disable-nonfree \
  --disable-autodetect \
  --disable-network \
  --disable-programs \
  --disable-doc \
  --disable-debug \
  --disable-everything \
  --enable-avcodec \
  --enable-avformat \
  --enable-avutil \
  --enable-swresample \
  --enable-decoder=pcm_s16le \
  --enable-decoder=pcm_s24le \
  --enable-decoder=pcm_s32le \
  --enable-decoder=pcm_f32le \
  --enable-demuxer=wav \
  --enable-encoder=aac \
  --enable-muxer=mov \
  --enable-protocol=file \
  --enable-small \
  --enable-pic \
  --enable-cross-compile \
  --enable-static \
  --disable-shared

make -j1
make install

find "$install_root" -type f -print0 | sort -z | xargs -0 sha256sum
echo "Development-only static outputs created outside the repository."
echo "Do not package, commit, or distribute these artifacts."
