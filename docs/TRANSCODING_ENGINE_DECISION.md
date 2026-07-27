# Transcoding Engine Decision

**Status:** Not approved
**Decision gate:** Must be completed before Sprint 6 can distribute or integrate
any native transcoding binary.

This document is an engineering and compliance checklist. It is not legal advice,
and it does not claim that legal approval has been obtained.

## Why a maintained mobile engine is required

MediaForge GenZ is Android-first and iOS-ready. Real offline conversion will need
an engine that:

- supports Android and iOS application sandboxes and lifecycle constraints;
- exposes cancellation and structured progress;
- operates on platform-provided file handles without broad storage access;
- publishes supported architectures, minimum OS versions, and codec coverage;
- receives security, compiler, and platform-SDK maintenance;
- has a reproducible, reviewable native build and release process.

The preferred investigation order is:

1. Platform-native media frameworks for formats they support:
   [Android media APIs](https://developer.android.com/media) and
   [Apple AVFoundation](https://developer.apple.com/documentation/avfoundation/).
2. A maintained cross-platform native engine or vendor with explicit Android/iOS
   support, release ownership, provenance, and license documentation.
3. A project-owned, reproducible FFmpeg build only if platform-native coverage is
   insufficient and the maintenance/compliance cost is explicitly accepted.

## Why Windows FFmpeg executables cannot be reused

A Windows `ffmpeg.exe` is a PE executable compiled for Windows APIs and desktop
process conventions. Android packages native code by ABI in ELF shared libraries;
iOS packages signed Mach-O frameworks/libraries and restricts executable code
loading. A desktop executable also does not provide the mobile file-handle,
sandbox, cancellation, background-lifecycle, or app-store integration boundary
required by this architecture.

No binary or command builder from MediaForge Desktop is an input to the mobile
engine decision.

## FFmpegKit retirement and ownership risk

The upstream [FFmpegKit repository](https://github.com/arthenica/ffmpeg-kit) is
archived and read-only. Its own notice says the project is officially retired and
that there will be no further releases.

MediaForge GenZ must not adopt an archived FFmpegKit binary or an arbitrary fork
without a written review of:

- the fork's accountable maintainers and security-response process;
- supported Android/iOS SDKs, ABIs, architectures, and release cadence;
- exact upstream FFmpeg and dependency versions;
- build scripts, CI provenance, signing, and reproducibility;
- open vulnerabilities and patch latency;
- license configuration and complete corresponding source availability;
- a project-owned exit and upgrade plan.

Popularity, package availability, or successful compilation is not approval.

## LGPL-only versus GPL FFmpeg builds

FFmpeg's official [license and legal considerations](https://ffmpeg.org/legal.html)
state that FFmpeg is primarily LGPL 2.1-or-later, while enabling optional GPL
parts makes GPL apply to the resulting FFmpeg build. The exact configure flags and
every linked dependency determine the effective obligations.

An LGPL-only candidate must, at minimum:

- exclude `--enable-gpl` and `--enable-nonfree`;
- document static/dynamic linking choices for each mobile platform;
- preserve notices and license texts;
- provide complete corresponding FFmpeg source matching shipped binaries,
  including patches and build instructions;
- undergo legal review of relinking/replacement requirements and app-store
  distribution constraints.

A GPL-enabled candidate changes the distribution analysis materially and must not
be selected implicitly for access to codecs or filters. It requires a separate,
explicit product and legal decision before integration.

Codec patent, trademark, export-control, and third-party dependency questions are
separate from FFmpeg's copyright license and also require review.

## Required compliance and provenance record

Before any binary is committed, downloaded by CI, or distributed, the approved
engine release must have a versioned record containing:

- engine name, upstream repository, immutable commit/tag, and release version;
- source archive SHA-256 and every downloaded dependency's source/version/hash;
- toolchain, NDK/Xcode/SDK versions, target ABIs/architectures, and minimum OS;
- complete configure/build commands, environment, patches, and generated config;
- reproducible-build instructions and independently verified output hashes;
- software bill of materials and vulnerability scan results;
- effective license conclusion for the exact build configuration;
- third-party copyright notices and complete license texts;
- corresponding-source publication location and retention owner;
- binary signing/provenance details and release approvers;
- security-update, end-of-life, and incident-response owners.

These records must be regenerated for every engine upgrade or configuration
change.

## Sprint 6 approval checklist

All boxes remain deliberately unchecked.

- [ ] Required conversion formats and codec matrix are approved.
- [ ] Platform-native Android coverage and limitations are documented.
- [ ] Platform-native iOS coverage and limitations are documented.
- [ ] Candidate engine has active, accountable maintenance.
- [ ] Android/iOS architecture and minimum-version support are verified.
- [ ] Cancellation, progress, sandboxed input, and temporary-output APIs are proven.
- [ ] Exact dependency graph and licenses are recorded.
- [ ] LGPL-only versus GPL configuration is explicitly selected.
- [ ] Static/dynamic linking implications are reviewed for Android and iOS.
- [ ] Corresponding-source and relinking obligations are documented.
- [ ] Required third-party notices are drafted and reviewed.
- [ ] Reproducible build succeeds from pinned source and toolchains.
- [ ] Source archives, versions, patches, and SHA-256 records are stored.
- [ ] SBOM and vulnerability review are complete.
- [ ] App-store policy review is complete.
- [ ] Legal review is complete and recorded.
- [ ] Engineering, security, product, and legal owners approve Sprint 6.

Until every applicable gate is approved, the repository must remain binary-free
and conversion execution must remain unavailable.
