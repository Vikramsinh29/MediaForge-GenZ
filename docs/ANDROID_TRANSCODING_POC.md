# Android Transcoding Proof of Concept

## Result and status

**Result:** The external static-library build, managed adapter, bridge build, and
physical-device proof are verified for the narrow development-only path.

On 2026-07-27 the developer built the narrow FFmpeg static libraries under WSL
outside this repository. No FFmpeg source, native library, archive, AAR, or APK
was copied into Git. The normal Android build remains binary-free; the local
bridge is included only when an explicit external path is supplied.

This is a private development plan only. It is not legal approval, LGPL
compliance certification, production readiness, or authorization to distribute
any native artifact.

## Narrow proof boundary

The only permitted proof is:

```text
WAV audio input -> AAC audio in an M4A container
```

No video, image, batch, network, cloud, AI, background, or iOS conversion belongs
to this proof. FFmpegKit and prebuilt FFmpeg packages are prohibited.

The future Android adapter may accept a queued plan only when:

- the input is WAV (`audio/wav`, `audio/x-wav`, or `.wav`);
- the planned output is M4A;
- the output name differs from the input name;
- `OverwriteOriginal` is false;
- scoped source access is currently available.

## Required local prerequisites

All prerequisites must already be installed or supplied by the developer. The
repository scripts do not download them.

| Requirement | Required record |
| --- | --- |
| Android SDK | `C:\Users\<user>\AppData\Local\Android\Sdk` or explicit path |
| Android NDK | Exact installed revision under `<SDK>\ndk\<revision>` |
| CMake | Exact installed revision under `<SDK>\cmake\<revision>` |
| Ninja | Executable supplied by the recorded CMake/toolchain installation |
| JDK | Existing `C:\jdk` installation used by the managed Android build |
| Shell | WSL2/Linux/macOS shell capable of running FFmpeg `configure` |
| FFmpeg source | `/home/vikram/mediaforge-native-poc/ffmpeg-source` (external) |
| Source commit | `38b88335f99e76ed89ff3c93f877fdefce736c13` |
| Source archive SHA-256 | `4a26df901e05368cbd522dd784ddbdcaf13c2c62bd748352f6208f3e2344113a` |
| NDK revision | `27.3.13750724` (`android-ndk-r27d`) |
| NDK archive SHA-256 | `601246087a682d1944e1e16dd85bc6e49560fe8b6d61255be2829178c8ed15d9` |
| External work root | `/home/vikram/mediaforge-native-poc` |

Run the non-mutating prerequisite check:

```powershell
./scripts/Test-AndroidTranscodingPocPrerequisites.ps1 `
  -FfmpegSourceRoot C:\path\outside\repo\ffmpeg `
  -ExpectedCommit <40-character-commit> `
  -ExpectedSourceSha256 <64-character-sha256>
```

Any missing, placeholder, or mismatched value blocks the proof.

## Development-only build

The build recipe is
[`scripts/build-ffmpeg-android-poc.sh`](../scripts/build-ffmpeg-android-poc.sh).
It accepts only explicit source, NDK, output, commit, hash, and API-level inputs.
The source and output roots must be outside the MediaForge GenZ repository.

Example from a compatible shell:

```bash
./scripts/build-ffmpeg-android-poc.sh \
  --source /external/ffmpeg \
  --source-archive /external/downloads/ffmpeg-pinned.tar.xz \
  --ndk /external/android-sdk/ndk/<PINNED_REVISION> \
  --output /external/MediaForgeGenZ/native-poc/arm64-v8a \
  --commit <REQUIRED_40_CHARACTER_COMMIT> \
  --source-sha256 <REQUIRED_64_CHARACTER_SHA256> \
  --api 21
```

The recipe targets Android `arm64-v8a`, disables GPL and nonfree code, disables
external libraries and network support, and enables only the minimum WAV/PCM
input, AAC encoder, and MOV/M4A output surface. It creates development artifacts
outside Git. Its output is not approved for packaging or distribution.

Before any repeated local build, review the exact pinned revision's
license/component data and retain:

- NDK, compiler, linker, CMake/Ninja, shell, and host versions;
- clean source status and immutable commit;
- source/archive/patch hashes;
- exact configure and build logs;
- generated configuration and enabled-component lists;
- unstripped and stripped artifact hashes;
- linked libraries and exported symbols.

## Managed/native integration design

The source-only implementation now provides:

1. A development-only Android adapter implementing `ITranscoder`.
2. A small typed C ABI boundary consuming app-private temporary paths without
   shell execution.
3. The queue moves `Queued -> Preparing -> Processing`.
4. Native timestamps report normalized, monotonic progress.
5. Cancellation signals the native operation and awaits termination.
6. The adapter validates a non-empty M4A container with a readable AAC audio
   stream before finalisation.
7. `IOutputStorage` atomically publishes a collision-free name and never
   overwrites the WAV source.
8. Only successful validation/finalisation permits `Completed`; failures and
   cancellation discard the temporary output.

The local native artifact must remain external to Git and must not be copied into
`Platforms/Android`, `Resources`, `jniLibs`, NuGet content, an APK, or an AAR.

## Build the local bridge outside Git

From WSL, after the external static-library build:

```bash
bash /mnt/c/Users/vikra/MediaForge-GenZ/scripts/build-android-poc-bridge.sh \
  --ffmpeg-install /home/vikram/mediaforge-native-poc/arm64-v8a-run1/install \
  --ndk /home/vikram/mediaforge-native-poc/android-ndk-r27d \
  --bridge-source /mnt/c/Users/vikra/MediaForge-GenZ/Platforms/Android/Native/mediaforge_poc_bridge.c \
  --output /home/vikram/mediaforge-native-poc/bridge-arm64-v8a-run5 \
  --api 21
```

Build a private development APK by explicitly pointing MSBuild at the external
library. Omit this property for the normal binary-free build:

```powershell
dotnet build MediaForge.Universal.csproj --framework net10.0-android `
  -p:TargetFrameworks=net10.0-android `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
  -p:JavaSdkDirectory=C:\jdk `
  -p:MediaForgeNativePocLibrary="\\wsl.localhost\Ubuntu\home\vikram\mediaforge-native-poc\bridge-arm64-v8a-run5\libmediaforge_poc.so"
```

This APK is private development evidence only. Do not publish, upload, commit,
share, or distribute it.

The final verified bridge is AArch64, uses 16 KB maximum page alignment, and has
SHA-256
`6195d7feca4fa4ca9a3dc1feb272675583e66d09083242bce6046925a466befa`.

## Device acceptance test

1. Use a small, known-good WAV fixture selected through the system picker.
2. Queue the M4A proof preset and confirm the proposed name differs from input.
3. Start the development-only job and verify Preparing/Processing progress.
4. Cancel once and verify no final output or partial temporary output remains.
5. Run again to completion.
6. Verify the WAV source is byte-for-byte unchanged.
7. Verify the M4A exists under a new name, is non-empty, and metadata reports an
   AAC audio stream in an M4A/MOV container.
8. Play the complete output using an Android system player.
9. Re-run with the same proposed destination and confirm collision-safe naming.
10. Capture device/API/ABI, input/output hashes, duration, logs, and result.

Device proof completed on an Android 14 ARM64 Xiaomi device:

- the five-second WAV source retained SHA-256
  `5ed9e874d6e6cb55d098831ac4e965b03633d8186b6dae7128decda2e3f90c02`;
- Android validated and played the 81,284-byte AAC/M4A output with SHA-256
  `af7c3238ddeb2054bed17f12fe7fd8dbdd40d1c99c13e86acf69422b1c06fe88`;
- a repeated export produced the collision-safe `-2.m4a` name;
- cancellation reached `Cancelled`, published no output, and left both
  app-private temporary directories empty.

This proves only the private WAV-to-M4A development path on the tested device.
It is not broad device compatibility, legal approval, compliance certification,
production readiness, or permission to distribute an APK or native artifact.
