# Android Transcoding Proof of Concept

## Result and status

**Result:** Blocked at the prerequisite gate on 2026-07-27.

The configured Android SDK does not contain an NDK or CMake installation, and no
host CMake, Ninja, or Clang toolchain was found. No dependency was downloaded, no
FFmpeg source was obtained, no native build ran, and no binary or APK containing
FFmpeg was created.

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
| FFmpeg source | Developer-supplied clean source tree outside this repository |
| Source commit | `<REQUIRED: full immutable 40-character FFmpeg commit>` |
| Source archive SHA-256 | `<REQUIRED: lowercase 64-character hash>` |
| Source tree verification | `<REQUIRED: captured git/archive verification>` |
| External work root | `%LOCALAPPDATA%\MediaForgeGenZ\native-poc` or equivalent |

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

Before any local build, replace all placeholders, review the exact pinned
revision's license/component data, and record:

- NDK, compiler, linker, CMake/Ninja, shell, and host versions;
- clean source status and immutable commit;
- source/archive/patch hashes;
- exact configure and build logs;
- generated configuration and enabled-component lists;
- unstripped and stripped artifact hashes;
- linked libraries and exported symbols.

## Future managed/native integration design

No adapter is implemented in this sprint because the native prerequisite gate
failed. After a local build exists and its evidence is reviewed:

1. A development-only Android adapter implements `ITranscoder`.
2. A small typed JNI boundary consumes scoped input and app-owned temporary
   output descriptors; it must not execute a shell command.
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

## Device acceptance test, after prerequisites are satisfied

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

None of these device steps was performed in Sprint 8 because the native
prerequisites were unavailable.
