# Mobile Media Engine Architecture

## Status and scope

The repository contains a source-only, development-gated Android WAV-to-M4A
adapter proof. It contains no native binary. Normal builds remain binary-free,
and the iOS boundary remains unimplemented.

## Core contract boundary

`MediaForge.GenZ.Core` owns platform-neutral intent and lifecycle:

- `ExportPlan` is the validated, non-overwriting conversion intent.
- `ConversionJob` snapshots that plan and tracks state and progress.
- `IConversionJobQueue` enforces lifecycle transitions.
- `ITranscoder` is the future engine adapter contract.
- `IOutputStorage` owns temporary output and atomic finalisation contracts.

Core must not reference MAUI, Android, iOS, a command-line syntax, native paths,
FFmpeg types, file-picker types, or UI state.

## Queue persistence boundary

`IConversionJobQueue` owns ordered, editable planning jobs.
`IConversionQueueStore` is the platform-neutral persistence boundary, and
`IMediaSourceReferenceValidator` reports whether a saved source reference is
usable in the current app session.

The versioned queue document contains only lightweight `ExportPlan` and job
metadata. It never contains media bytes, thumbnails, output bytes, platform file
objects, or native handles. The application adapter stores the JSON document
under app-owned data and replaces it through a temporary file so an interrupted
save does not partially overwrite the last complete document.

Startup restoration accepts only the current document version, queued lifecycle
state, and plans that preserve the distinct-name/no-overwrite rules. Invalid or
outdated records are skipped with a user-visible diagnostic. A source reference
that cannot be reopened is retained as an unavailable plan so the user can review,
edit, reorder, or remove it; it must be selected again before future execution.

Queue persistence does not authorize transcoding, source copying, output
creation, background work, or temporary-media cleanup.

The valid job lifecycle is:

```text
Queued -> Preparing -> Processing -> Completed
   |          |             |
   +----------+-------------+----> Cancelled
              +-------------+----> Failed
```

Completed, Failed, and Cancelled are terminal. A failed transition must include an
error. Completed forces progress to 100%.

## Android native adapter boundary

A future Android adapter will implement `ITranscoder` using only the engine
approved by `TRANSCODING_ENGINE_DECISION.md`. It will:

- consume content/file descriptors supplied through the app's scoped access;
- translate `ExportPlan` settings into typed native API calls;
- report normalized progress through `IProgress<ConversionJobProgress>`;
- observe `CancellationToken` and stop native work deterministically;
- write only to the `TemporaryOutput` stream supplied by `IOutputStorage`;
- return a neutral `ConversionExecutionResult`;
- never choose the final destination or overwrite the source.

JNI/NDK/vendor types must remain under the Android platform adapter and must never
cross into core or view models.

Sprint 8 narrows the first private adapter proof to WAV input and AAC audio in an
M4A container. The development adapter design must use typed JNI calls rather
than shell execution, write only to `IOutputStorage` temporary output, report
normalized progress, honor cancellation, validate the resulting container/audio
stream, and finalise only through the existing no-overwrite boundary.

The Android proof stages scoped input to app-private cache storage, calls a typed
C ABI bridge, copies output through `IOutputStorage`, validates AAC/M4A with the
Android media extractor, then uses a collision-safe atomic move. Native artifacts
remain outside the repository and are included only in an explicitly opted-in
private development build. They must never be committed or distributed.

## Future iOS native adapter boundary

The iOS adapter will implement the same contracts using an approved Apple-native
or maintained mobile engine. AVFoundation and related Apple frameworks should be
evaluated first for supported workflows.

The adapter will use security-scoped/platform-approved inputs, map native progress
and cancellation into core types, and write only to temporary output. Objective-C,
Swift, AVFoundation, VideoToolbox, or vendor types must remain behind the adapter.

Android and iOS may use different engines while presenting the same core
capabilities and lifecycle.

## Execution and safe-finalisation flow

1. **Validate plan**
   - Re-run export compatibility validation.
   - Reject `OverwriteOriginal = true`.
   - Reject a proposed output name equal to the source name.
2. **Queue**
   - Create an immutable job in `Queued`.
3. **Prepare**
   - Confirm input access is still valid.
   - Reserve a `TemporaryOutput` in app-controlled temporary storage.
   - Open the source and temporary-output streams.
4. **Process**
   - Transition to `Processing`.
   - Invoke the approved platform adapter.
   - Normalize progress to `0..1`; never infer completion from process exit alone.
5. **Cancel or fail**
   - Signal the native engine and await termination.
   - Close streams and discard temporary output.
   - Transition exactly once to Cancelled or Failed.
6. **Validate output**
   - Confirm the temporary output exists, is non-empty, and matches the planned
     media/container expectations.
   - Optionally re-read metadata before finalisation.
7. **Atomically finalise**
   - Call `FinalizeAtomicallyAsync` with the original approved plan.
   - Re-check destination collision/non-overwrite rules.
   - Publish via a platform-safe atomic move/commit where supported.
   - Never expose a partial final file.
8. **Complete**
   - Transition to Completed only after finalisation succeeds.
   - Report 100% and return the final neutral `MediaAsset`.

## Temporary output rules

- Temporary identifiers are opaque core values, not paths.
- Platform storage chooses private, scoped locations.
- Temporary names cannot be treated as final user-visible destinations.
- Startup recovery must remove abandoned temporary outputs after a defined age.
- Cancellation, failure, validation rejection, and finalisation exceptions must
  attempt cleanup.
- Cleanup failures are logged without changing a successful finalisation into a
  source overwrite or destructive retry.

## Android completed-output boundary

The Android output adapter owns all MediaStore and content-URI operations.
Core, queue, and view-model code see only `MediaAsset` metadata whose identifier
is an opaque platform reference. On Android 10 and later finalisation follows:

1. validate the app-private temporary M4A;
2. reserve a collision-free pending `MediaStore.Audio` item;
3. copy into that pending item;
4. reopen and validate file size plus the readable AAC track;
5. publish by clearing the pending flag;
6. attach the opaque finalized output to the completed queue job.

Any failure before step 5 deletes the pending item and retains no completed
output reference. Failure to persist step 6 rolls back the newly published
output before the job enters `Failed`. Open and share behavior is provided through the neutral
`IOutputOpener` and `IShareService` boundaries. Clearing a completed queue entry
removes only the lightweight job record and never deletes the user's published
file. Completed records may be restored only when they contain a structurally
valid opaque output reference and positive lightweight size metadata.

## Progress and cancellation rules

- Queued and Preparing may report 0%.
- Processing reports a monotonic normalized fraction between 0 and 1.
- Native callbacks must be throttled before reaching the UI.
- Cancellation is cooperative at the core boundary and must map to the native
  engine's stop mechanism.
- Completion is a lifecycle result, not merely a progress value.
- No cancellation path may delete or modify the source.

## Adapter acceptance tests required before Sprint 6

- state-transition and terminal-state tests;
- cancellation during preparation and processing;
- corrupt/inaccessible input handling;
- temporary-output cleanup after failure and process termination;
- destination collision and source-name collision rejection;
- atomic-finalisation interruption tests;
- progress monotonicity and callback throttling;
- codec/container matrix tests per OS version and device architecture;
- reproducible native build and binary provenance verification.
