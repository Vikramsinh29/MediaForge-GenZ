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

## Build Android

```powershell
dotnet build MediaForge.Universal.csproj --framework net10.0-android -p:TargetFrameworks=net10.0-android -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" -p:JavaSdkDirectory=C:\jdk
```
