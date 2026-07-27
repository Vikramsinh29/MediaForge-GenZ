# FFmpeg Mobile Build Specification

## Status

**Strategy:** LGPL-only FFmpeg build
**Approval:** Pending legal, security, engineering, and release review
**Binary status:** No FFmpeg source, binary, package, or build output is approved
for this repository or for distribution.

This specification defines evidence required before a build may be evaluated. It
does not certify LGPL compliance, grant legal approval, or establish production
readiness.

## Target matrix

Every produced architecture must have an independent binary hash and test record.

### Android

| ABI | Planned use | Sprint 6 status |
| --- | --- | --- |
| `arm64-v8a` | Required physical-device release target | Specification only |
| `armeabi-v7a` | Optional compatibility target; product decision required | Not approved |
| `x86_64` | Emulator/test target; distribution decision required | Not approved |
| `x86` | Unsupported unless a later written decision adds it | Prohibited |

The Android minimum API level must not be lower than the application minimum and
must be recorded with the pinned NDK version. The exact NDK revision, API level,
compiler, C library, CMake/Ninja versions, and ABI list are release-blocking
fields—not values to infer from a developer machine.

### Future iOS

| Architecture | Planned use | Sprint 6 status |
| --- | --- | --- |
| `arm64` | Required physical-device target | Specification only |
| `arm64` simulator | Required Apple Silicon simulator target | Specification only |
| `x86_64` simulator | Optional Intel simulator target, subject to supported Xcode matrix | Not approved |

The iOS deployment target, Xcode version, SDK build number, compiler, framework
packaging, signing, and static/dynamic linkage approach require separate records.
No simulator slice may be shipped in a device framework.

## Source pinning

A candidate build is invalid until all placeholders below are replaced in a
version-controlled release evidence record:

| Field | Required value |
| --- | --- |
| FFmpeg upstream URL | `https://git.ffmpeg.org/ffmpeg.git` or approved canonical mirror |
| Immutable FFmpeg commit | `<REQUIRED: full 40-character commit>` |
| Human-readable release/tag | `<REQUIRED>` |
| Source archive filename | `<REQUIRED>` |
| Source archive SHA-256 | `<REQUIRED: lowercase 64 hex characters>` |
| Git tree/commit verification | `<REQUIRED: command and captured result>` |
| Patch series | `<REQUIRED: ordered filenames and SHA-256, or NONE>` |
| Build-recipe revision | `<REQUIRED: repository commit>` |
| Build container/toolchain image digest | `<REQUIRED: immutable digest>` |

Tags, branch names, package-manager versions, and mutable download URLs are not
sufficient source pins.

Every optional dependency must have the same source URL, immutable revision,
archive hash, patch hash, license, and build-command evidence in
`THIRD_PARTY_DEPENDENCY_INVENTORY.md`.

## LGPL-only configuration constraints

The candidate configuration must:

- keep GPL code disabled;
- keep nonfree code disabled;
- include only dependencies whose exact versions and linkage preserve the
  approved LGPL-only strategy;
- generate and retain `config.h`, `config.mak`, configure output, enabled
  component lists, and license output for every platform/architecture;
- use the smallest codec, muxer, demuxer, filter, protocol, and device surface
  needed by the approved product matrix;
- disable network protocols unless an explicit offline-product exception is
  approved;
- avoid runtime command execution and expose a typed mobile adapter API;
- retain copyright notices and license texts in source and distributed notices.

The following configure flags are prohibited:

```text
--enable-gpl
--enable-nonfree
--enable-libx264
--enable-libx265
--enable-libxvid
--enable-libvidstab
--enable-librubberband
--enable-libfdk-aac
```

The following component families are prohibited unless this specification,
license analysis, and automated deny-list are all updated through an approved
review:

- x264, x265, and Xvid;
- vid.stab;
- Rubber Band;
- Fraunhofer FDK AAC;
- any FFmpeg component marked GPL in the pinned revision;
- any dependency that causes the combined build to become GPL or nonfree;
- any prebuilt FFmpegKit package or binary from an unapproved fork.

This is a minimum deny-list, not proof that every other component is acceptable.
The pinned FFmpeg revision's license documentation and the complete dependency
graph must be reviewed before enabling anything.

## Required build command record

No canonical command is approved yet. Before execution, each target must add a
verbatim, reviewable command record containing:

```text
SOURCE_DATE_EPOCH=<pinned commit timestamp>
PATH=<pinned toolchain paths only>
CC=<exact compiler>
AR=<exact archiver>
STRIP=<exact strip tool>
PKG_CONFIG_LIBDIR=<controlled dependency metadata directory>

./configure \
  <exact target/cross-prefix/sysroot flags> \
  --disable-gpl \
  --disable-nonfree \
  <explicit enable/disable component allow-list> \
  <reviewed linkage flags>

<exact build command with fixed parallelism>
<exact install/staging command>
<exact symbol/strip command>
```

The record must include:

- working directory and clean-source verification;
- every environment variable that can affect output;
- shell and operating-system image;
- compiler/linker commands after expansion;
- generated configuration files;
- start/end timestamps and exit codes;
- unstripped and stripped artifact hashes;
- archive/framework/AAR packaging commands;
- commands used to enumerate linked libraries and exported symbols.

Ad hoc local commands are not release evidence.

## Reproducibility requirements

For each architecture:

1. Build twice from fresh source trees using the same pinned environment.
2. Build once on an independent approved runner.
3. Compare unstripped and distributable artifact SHA-256 values.
4. Explain and eliminate nondeterminism; a waived mismatch requires written
   security and release approval.
5. Store logs, generated configuration, tool versions, dependency hashes, and
   artifact manifests with the release evidence.

The release record must map:

```text
source commit + patches + dependency hashes + toolchain digest + command hash
    -> unstripped artifact hash
    -> stripped/package artifact hash
```

## Attribution, notices, and source access

Before distribution, the release must include:

- FFmpeg copyright notice and applicable LGPL text;
- notices and license texts for every linked dependency;
- an in-app and distribution-channel third-party notices location;
- complete corresponding FFmpeg/dependency source matching the shipped binaries;
- all project patches, generated build instructions, and configuration;
- a durable source-access URL and retention owner;
- any required relinking/replacement mechanism and instructions, as determined by
  legal review;
- an offer/source-access process appropriate to every applicable license.

Source availability must be tested from a clean, unauthenticated environment.

## SBOM, security, and release evidence

The evidence bundle must contain:

- SPDX or CycloneDX SBOM for source and packaged native artifacts;
- package/component names, versions, licenses, PURLs/CPEs where available;
- vulnerability scan tool/version, database timestamp, raw result, triage, and
  approved exceptions;
- exported symbols and linked-library inventory;
- binary signing/provenance attestations;
- Android AAR contents and per-ABI hashes, if Android packaging is approved;
- iOS XCFramework contents and per-slice hashes, if iOS packaging is approved;
- device/OS/architecture codec-matrix test results;
- legal, security, engineering, product, and release approvals.

Use `RELEASE_COMPLIANCE_CHECKLIST.md` as the distribution gate. Any missing,
placeholder, stale, or contradictory evidence blocks binary distribution.
