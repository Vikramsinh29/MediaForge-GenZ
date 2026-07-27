# MediaForge GenZ

MediaForge GenZ is an open-source, offline-first media toolkit for creators. The
current app is Android-first and keeps an iOS target ready for later development.

## Architecture

- `MediaForge.Universal` contains the .NET MAUI presentation, platform services,
  and composition root.
- `src/MediaForge.GenZ.Core` contains platform-neutral models and contracts.
- Platform services implement the core contracts without leaking device APIs into
  view models.
- No media conversion is implemented yet.

The core project deliberately has no MAUI, Android, iOS, Windows, WPF, or FFmpeg
dependency.

## Media Import & Library

The app uses the operating system's document picker to select multiple video,
audio, and image files. It requests no broad storage permission. Imported entries
retain lightweight file handles and basic properties only; file contents are not
buffered. Clearing the library never deletes original files.

## Lightweight Preview & Metadata

Selecting a library card opens media details. Android reads metadata through the
platform-neutral metadata contract using seek-based stream access. Images are
decoded first for bounds and then sampled to a bounded preview. Video previews use
a scaled frame API, and audio uses a local waveform placeholder. Corrupt,
inaccessible, or unsupported details degrade to partial metadata without changing
the source file.

No full media playback, thumbnail extraction pipeline, transcoding, FFmpeg,
network service, cloud feature, or AI feature is included.

## Creator Export Planning

The platform-neutral core provides creator presets for Instagram Reels, YouTube
Shorts, TikTok, WhatsApp sharing, audio extraction, image compression, and custom
exports. Only presets compatible with the selected media type are shown.

The planner validates compatibility and produces a review-only plan containing
the proposed format, quality, aspect ratio, settings, and a sanitized output name.
Every output name includes a preset suffix and every plan explicitly prevents
overwriting the source. Sprint 4 performs no conversion and writes no files.

## Local Transcoding Architecture

Sprint 5 defines a neutral conversion lifecycle from Queued through Preparing and
Processing to Completed, Failed, or Cancelled. A planning-only in-memory queue can
display validated export plans, but it cannot execute them.

`ITranscoder` is the future mobile-engine boundary. `IOutputStorage` separates
temporary output from safe atomic finalisation and requires the approved,
non-overwriting `ExportPlan`. No engine implementation or output writer exists.

The native-engine approval gate and compliance requirements are documented in
[`docs/TRANSCODING_ENGINE_DECISION.md`](docs/TRANSCODING_ENGINE_DECISION.md).
The Android/iOS adapter and finalisation flow are documented in
[`docs/MOBILE_MEDIA_ENGINE_ARCHITECTURE.md`](docs/MOBILE_MEDIA_ENGINE_ARCHITECTURE.md).
No legal approval is claimed.

## LGPL Mobile Engine Readiness

Sprint 6 records an LGPL-only FFmpeg build as the selected technical direction,
pending legal, security, engineering, and release approval. It adds no FFmpeg
source, package, binary, native implementation, or conversion execution.

The future build and evidence requirements are defined in
[`docs/FFMPEG_MOBILE_BUILD_SPEC.md`](docs/FFMPEG_MOBILE_BUILD_SPEC.md).
[`docs/THIRD_PARTY_DEPENDENCY_INVENTORY.md`](docs/THIRD_PARTY_DEPENDENCY_INVENTORY.md)
provides the dependency record template, and
[`docs/RELEASE_COMPLIANCE_CHECKLIST.md`](docs/RELEASE_COMPLIANCE_CHECKLIST.md)
keeps native binary distribution blocked until all applicable evidence and
approvals are complete.

The repository validation script rejects prohibited GPL/nonfree FFmpeg
configuration flags and native binary artifacts:

```powershell
./scripts/validate-native-compliance.ps1
```

The guard is a technical control, not legal advice, compliance certification, or
production-readiness approval.

## Persistent Conversion Queue

Sprint 7 stores ordered export plans as a versioned JSON document in app-owned
local data. Users can reorder, edit, remove, and clear planned jobs. Saves use a
temporary document followed by replacement so an interrupted write cannot expose
a partially written queue.

Only lightweight plan and source-reference metadata is stored. Media bytes,
previews, outputs, native handles, and platform picker objects are never persisted.
If scoped source access is unavailable after restart, the plan remains visible
and is clearly marked for source reselection. The queue still performs no
conversion and creates no media output.

## Build Android

```powershell
dotnet build MediaForge.Universal.csproj --framework net10.0-android -p:TargetFrameworks=net10.0-android -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" -p:JavaSdkDirectory=C:\jdk
```
