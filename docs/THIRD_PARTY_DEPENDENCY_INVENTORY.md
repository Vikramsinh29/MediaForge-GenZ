# Third-Party Native Dependency Inventory

## Status

Template only. No FFmpeg or native dependency is approved, bundled, or
distributed. One completed section is required per exact dependency version and
build variant.

## Release inventory header

| Field | Value |
| --- | --- |
| Product release | `<REQUIRED>` |
| Evidence revision | `<REQUIRED: repository commit>` |
| Inventory owner | `<REQUIRED>` |
| Review date | `<REQUIRED: YYYY-MM-DD>` |
| Target platforms/architectures | `<REQUIRED>` |
| SBOM filename and SHA-256 | `<REQUIRED>` |
| Legal review record | `<REQUIRED; PENDING is not approval>` |
| Security review record | `<REQUIRED>` |

## Dependency record template

Copy this section for every direct, transitive, build-time, statically linked, or
dynamically linked native dependency.

### `<DEPENDENCY NAME> <EXACT VERSION>`

| Field | Required evidence |
| --- | --- |
| Purpose | `<REQUIRED>` |
| Upstream project URL | `<REQUIRED>` |
| Canonical source URL | `<REQUIRED>` |
| Immutable commit/tag | `<REQUIRED: full immutable identifier>` |
| Source archive | `<REQUIRED>` |
| Source SHA-256 | `<REQUIRED: 64 hex characters>` |
| Applied patches and hashes | `<REQUIRED or NONE>` |
| Copyright holders | `<REQUIRED>` |
| Declared license/SPDX | `<REQUIRED>` |
| Reviewed license files | `<REQUIRED: paths and hashes>` |
| Effective build license | `<REQUIRED; do not infer from project name>` |
| Enabled features/components | `<REQUIRED>` |
| Disabled features/components | `<REQUIRED>` |
| Configure/build command record | `<REQUIRED: evidence path and hash>` |
| Build toolchain/container digest | `<REQUIRED>` |
| Linkage per platform | `<REQUIRED: static/dynamic and rationale>` |
| FFmpeg interaction | `<REQUIRED: linked/loaded/tool-only/not applicable>` |
| Modifications | `<REQUIRED: description or NONE>` |
| Corresponding source location | `<REQUIRED>` |
| Relinking/replacement mechanism | `<REQUIRED or legal-review finding>` |
| Required notice text | `<REQUIRED: evidence path>` |
| Distributed license text | `<REQUIRED: evidence path>` |
| PURL/CPE identifiers | `<REQUIRED where available>` |
| Vulnerability scan evidence | `<REQUIRED: tool/database date/result>` |
| Known vulnerabilities/exceptions | `<REQUIRED or NONE>` |
| Maintenance/security owner | `<REQUIRED>` |
| End-of-life policy | `<REQUIRED>` |
| Android artifact/ABI hashes | `<REQUIRED if applicable>` |
| iOS artifact/slice hashes | `<REQUIRED if applicable>` |
| SBOM component reference | `<REQUIRED>` |
| Legal approval reference | `<REQUIRED before distribution>` |
| Security approval reference | `<REQUIRED before distribution>` |
| Release approval reference | `<REQUIRED before distribution>` |

## Aggregate checks

- [ ] Every packaged native object maps to exactly one inventory record.
- [ ] Every transitive dependency appears in the SBOM and this inventory.
- [ ] Source and binary hashes match the release evidence.
- [ ] No dependency enables a prohibited GPL/nonfree component.
- [ ] Notices and license texts cover the exact configured build.
- [ ] Source-access URLs were tested without privileged credentials.
- [ ] Exceptions have owners, expiration dates, and written approvals.

Unchecked items block native binary distribution.
