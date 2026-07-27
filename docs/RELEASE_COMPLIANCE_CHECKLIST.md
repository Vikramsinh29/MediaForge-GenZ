# Native Media Release Compliance Checklist

## Gate status

**Default result: BLOCKED**

This checklist does not certify LGPL compliance, constitute legal advice, claim
legal approval, or establish production readiness. Native binary distribution is
blocked until every applicable item is complete and the named reviewers approve
the exact evidence bundle.

## Scope identity

- [ ] Product release and repository commit are immutable and recorded.
- [ ] FFmpeg source commit/tag and source SHA-256 are recorded.
- [ ] Patch series and every patch SHA-256 are recorded, or explicitly `NONE`.
- [ ] Build-recipe commit and toolchain/container digest are recorded.
- [ ] Android/iOS target matrix matches `FFMPEG_MOBILE_BUILD_SPEC.md`.
- [ ] No placeholder such as `<REQUIRED>`, `TBD`, or `PENDING` remains in release evidence.

## LGPL-only configuration

- [ ] GPL and nonfree configuration are disabled.
- [ ] Automated native-compliance validation passes.
- [ ] Generated FFmpeg configuration and enabled-component lists are archived.
- [ ] No prohibited component from the mobile build specification is enabled.
- [ ] The exact dependency graph has been reviewed for effective licensing.
- [ ] Static/dynamic linkage and relinking implications are documented per platform.
- [ ] Codec patent, trademark, export-control, and store-policy reviews are complete.

## Source, reproducibility, and provenance

- [ ] Clean builds succeeded twice with matching hashes.
- [ ] An independent approved runner reproduced the artifacts.
- [ ] Exact commands, environment, tool versions, and logs are archived.
- [ ] Unstripped, stripped, packaged, and per-architecture SHA-256 values are recorded.
- [ ] Linked libraries and exported symbols are recorded.
- [ ] Build provenance/signing attestations are verified.
- [ ] Every artifact maps back to pinned source, patches, dependencies, and commands.

## Inventory and security

- [ ] Third-party inventory covers direct, transitive, and build-time dependencies.
- [ ] SPDX or CycloneDX SBOM covers source and packaged native artifacts.
- [ ] Vulnerability scan evidence includes tool/version and database timestamp.
- [ ] Vulnerabilities are resolved or have approved, expiring exceptions.
- [ ] Security-update, incident-response, and end-of-life owners are recorded.
- [ ] No archived/unmaintained dependency lacks an approved ownership plan.

## Notices and corresponding source

- [ ] FFmpeg and dependency copyright notices are complete.
- [ ] Applicable license texts are packaged and user-accessible.
- [ ] Complete corresponding source matches the distributed binaries.
- [ ] Patches, generated configuration, and reproducible build instructions are published.
- [ ] Source-access URL and retention owner are recorded.
- [ ] Source access was tested anonymously from a clean environment.
- [ ] Any required relinking/replacement mechanism is implemented and tested.
- [ ] App, store listing, website, and download-channel attribution are reviewed.

## Functional safety evidence

- [ ] Android/iOS codec matrix passes on approved OS/device/architecture coverage.
- [ ] Progress and cancellation behavior pass adapter acceptance tests.
- [ ] Temporary-output cleanup passes failure/interruption tests.
- [ ] Output validation and atomic-finalisation tests pass.
- [ ] Source overwrite and destination collision tests pass.
- [ ] No broad storage permission or unapproved network behavior was added.

## Required approvals

- [ ] Engineering owner approves the exact implementation and evidence.
- [ ] Security owner approves the exact dependency/build risk.
- [ ] Product owner approves formats, device coverage, and maintenance cost.
- [ ] Release owner verifies artifacts and distribution evidence.
- [ ] Legal counsel approves the exact build, linkage, notices, and distribution plan.

## Release decision

| Field | Required value |
| --- | --- |
| Decision | `BLOCKED` or `APPROVED` |
| Exact artifact manifest hash | `<REQUIRED>` |
| Evidence bundle hash | `<REQUIRED>` |
| Approval record links | `<REQUIRED>` |
| Decision date | `<REQUIRED>` |
| Release owner | `<REQUIRED>` |

If any applicable checkbox is unchecked, any required field is missing, or any
evidence hash differs, the decision must remain `BLOCKED`.
