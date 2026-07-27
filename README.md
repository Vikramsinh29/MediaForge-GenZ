# MediaForge GenZ

MediaForge GenZ is an open-source, offline-first media toolkit for creators. The
current app is Android-first and keeps an iOS target ready for later development.

## Sprint 1 foundation

- `MediaForge.Universal` contains the .NET MAUI presentation and composition root.
- `src/MediaForge.GenZ.Core` contains platform-neutral models and contracts.
- Platform services will implement the core contracts in later sprints.
- No media conversion is implemented yet.

The core project deliberately has no MAUI, Android, iOS, Windows, WPF, or FFmpeg
dependency. Media import, metadata, transcoding, output storage, and sharing are
represented only by contracts so each platform can supply safe implementations.

## Build Android

```powershell
dotnet build MediaForge.Universal.csproj --framework net10.0-android -p:TargetFrameworks=net10.0-android -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" -p:JavaSdkDirectory=C:\jdk
```
