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

## Sprint 2: Media Import & Library

The app uses the operating system's document picker to select multiple video,
audio, and image files. It requests no broad storage permission. Imported entries
retain lightweight file handles and basic properties only; file contents are not
buffered and thumbnails are not extracted.

The in-memory library supports duplicate prevention, selection, clearing selected
items, and clearing the full list. Clearing the library never deletes original
files.

## Build Android

```powershell
dotnet build MediaForge.Universal.csproj --framework net10.0-android -p:TargetFrameworks=net10.0-android -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" -p:JavaSdkDirectory=C:\jdk
```
