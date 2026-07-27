using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Services;

public sealed class PersistentConversionJobQueue(
    IConversionQueueStore store,
    IMediaSourceReferenceValidator sourceValidator) : IConversionJobQueue
{
    private static readonly IReadOnlyDictionary<ConversionJobState, ConversionJobState[]> AllowedTransitions =
        new Dictionary<ConversionJobState, ConversionJobState[]>
        {
            [ConversionJobState.Queued] =
                [ConversionJobState.Preparing, ConversionJobState.Cancelled],
            [ConversionJobState.Preparing] =
                [ConversionJobState.Processing, ConversionJobState.Failed, ConversionJobState.Cancelled],
            [ConversionJobState.Processing] =
                [ConversionJobState.Completed, ConversionJobState.Failed, ConversionJobState.Cancelled],
            [ConversionJobState.Completed] = [],
            [ConversionJobState.Failed] = [],
            [ConversionJobState.Cancelled] = []
        };

    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private readonly object _snapshotLock = new();
    private List<ConversionJob> _jobs = [];
    private bool _initialized;

    public IReadOnlyList<ConversionJob> GetSnapshot()
    {
        lock (_snapshotLock)
        {
            return _jobs.ToArray();
        }
    }

    public async Task<ConversionQueueLoadResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return new ConversionQueueLoadResult(
                    new ConversionQueueDocument(
                        ConversionQueueDocument.CurrentVersion,
                        GetSnapshot()));
            }

            var loaded = await store.LoadAsync(cancellationToken);
            var restored = new List<ConversionJob>();
            var restoredIds = new HashSet<string>(StringComparer.Ordinal);
            var skipped = 0;

            if (loaded.Document?.Version == ConversionQueueDocument.CurrentVersion)
            {
                foreach (var job in loaded.Document.Jobs ?? [])
                {
                    if (job is null ||
                        string.IsNullOrWhiteSpace(job.Id) ||
                        !restoredIds.Add(job.Id) ||
                        job.State != ConversionJobState.Queued ||
                        !IsSafePlan(job.Plan))
                    {
                        skipped++;
                        continue;
                    }

                    var sourceState = await sourceValidator.GetStateAsync(
                        job.Plan.Source,
                        cancellationToken);
                    restored.Add(job with
                    {
                        State = ConversionJobState.Queued,
                        Progress = 0,
                        ErrorMessage = null,
                        SourceReferenceState = sourceState,
                        StatusMessage = GetSourceMessage(sourceState),
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            lock (_snapshotLock)
            {
                _jobs = restored;
                _initialized = true;
            }

            if (skipped > 0)
            {
                await SaveCurrentAsync(cancellationToken);
            }

            var message = loaded.Message;
            if (skipped > 0)
            {
                message = $"{skipped} invalid or outdated queue item{(skipped == 1 ? " was" : "s were")} skipped.";
            }

            return new ConversionQueueLoadResult(
                new ConversionQueueDocument(ConversionQueueDocument.CurrentVersion, restored),
                message);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<ConversionJob> EnqueueAsync(
        ExportPlan plan,
        CancellationToken cancellationToken = default)
    {
        EnsureSafePlan(plan);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            var now = DateTimeOffset.UtcNow;
            var sourceState = await sourceValidator.GetStateAsync(plan.Source, cancellationToken);
            var job = new ConversionJob(
                Guid.NewGuid().ToString("N"),
                plan,
                ConversionJobState.Queued,
                now,
                now,
                StatusMessage: GetSourceMessage(sourceState),
                SourceReferenceState: sourceState);
            var next = GetSnapshot().Append(job).ToList();
            await SaveAsync(next, cancellationToken);
            return job;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public Task<ValidationResult> MoveAsync(
        string jobId,
        int newIndex,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            jobs =>
            {
                var currentIndex = jobs.FindIndex(job => job.Id == jobId);
                if (currentIndex < 0)
                {
                    return ValidationResult.Failure("The planned export was not found.");
                }

                if (newIndex < 0 || newIndex >= jobs.Count)
                {
                    return ValidationResult.Failure("The requested queue position is invalid.");
                }

                var job = jobs[currentIndex];
                jobs.RemoveAt(currentIndex);
                jobs.Insert(newIndex, job);
                return ValidationResult.Success;
            },
            cancellationToken);

    public Task<ValidationResult> UpdatePlanAsync(
        string jobId,
        ExportPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafePlan(plan))
        {
            return Task.FromResult(
                ValidationResult.Failure("The edited plan must keep a distinct, non-overwriting output name."));
        }

        return MutateAsync(
            jobs =>
            {
                var index = jobs.FindIndex(job => job.Id == jobId);
                if (index < 0)
                {
                    return ValidationResult.Failure("The planned export was not found.");
                }

                var current = jobs[index];
                jobs[index] = current with
                {
                    Plan = plan,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    StatusMessage = GetSourceMessage(current.SourceReferenceState)
                };
                return ValidationResult.Success;
            },
            cancellationToken);
    }

    public Task<ValidationResult> RemoveAsync(
        string jobId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            jobs =>
            {
                var removed = jobs.RemoveAll(job => job.Id == jobId);
                return removed == 1
                    ? ValidationResult.Success
                    : ValidationResult.Failure("The planned export was not found.");
            },
            cancellationToken);

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            await SaveAsync([], cancellationToken);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public Task<ValidationResult> TransitionAsync(
        string jobId,
        ConversionJobState nextState,
        double? progress = null,
        string? statusMessage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            jobs =>
            {
                var index = jobs.FindIndex(job => job.Id == jobId);
                if (index < 0)
                {
                    return ValidationResult.Failure("The conversion job was not found.");
                }

                var current = jobs[index];
                if (!AllowedTransitions[current.State].Contains(nextState))
                {
                    return ValidationResult.Failure(
                        $"A job cannot move from {current.State} to {nextState}.");
                }

                var nextProgress = progress ?? current.Progress;
                if (nextProgress is < 0 or > 1)
                {
                    return ValidationResult.Failure("Progress must be between 0 and 1.");
                }

                if (nextState == ConversionJobState.Completed)
                {
                    nextProgress = 1;
                }

                if (nextState == ConversionJobState.Failed &&
                    string.IsNullOrWhiteSpace(errorMessage))
                {
                    return ValidationResult.Failure("A failed job requires an error message.");
                }

                jobs[index] = current with
                {
                    State = nextState,
                    Progress = nextProgress,
                    StatusMessage = statusMessage,
                    ErrorMessage = errorMessage,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                return ValidationResult.Success;
            },
            cancellationToken);

    public async Task<ValidationResult> CancelAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var current = GetSnapshot().FirstOrDefault(job => job.Id == jobId);
        if (current is null)
        {
            return ValidationResult.Failure("The conversion job was not found.");
        }

        if (current.State == ConversionJobState.Cancelled)
        {
            return ValidationResult.Success;
        }

        return await TransitionAsync(
            jobId,
            ConversionJobState.Cancelled,
            statusMessage: "Cancelled before finalisation.",
            cancellationToken: cancellationToken);
    }

    private async Task<ValidationResult> MutateAsync(
        Func<List<ConversionJob>, ValidationResult> mutation,
        CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            var next = GetSnapshot().ToList();
            var result = mutation(next);
            if (!result.IsValid)
            {
                return result;
            }

            await SaveAsync(next, cancellationToken);
            return ValidationResult.Success;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private async Task SaveAsync(
        List<ConversionJob> jobs,
        CancellationToken cancellationToken)
    {
        await store.SaveAsync(
            new ConversionQueueDocument(ConversionQueueDocument.CurrentVersion, jobs),
            cancellationToken);
        lock (_snapshotLock)
        {
            _jobs = jobs;
        }
    }

    private Task SaveCurrentAsync(CancellationToken cancellationToken) =>
        store.SaveAsync(
            new ConversionQueueDocument(
                ConversionQueueDocument.CurrentVersion,
                GetSnapshot()),
            cancellationToken);

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The conversion queue has not been initialized.");
        }
    }

    private static void EnsureSafePlan(ExportPlan plan)
    {
        if (!IsSafePlan(plan))
        {
            throw new ArgumentException(
                "A conversion job requires a distinct, non-overwriting output name.",
                nameof(plan));
        }
    }

    private static bool IsSafePlan(ExportPlan? plan) =>
        plan is not null &&
        !plan.OverwriteOriginal &&
        !string.IsNullOrWhiteSpace(plan.ProposedOutputFileName) &&
        !string.IsNullOrWhiteSpace(plan.Source.DisplayName) &&
        !string.IsNullOrWhiteSpace(plan.Source.Id) &&
        plan.Source.SizeInBytes is null or >= 0 &&
        Enum.IsDefined(plan.OutputFormat) &&
        Enum.IsDefined(plan.Quality) &&
        Enum.IsDefined(plan.AspectRatio) &&
        !string.Equals(
            plan.ProposedOutputFileName,
            plan.Source.DisplayName,
            StringComparison.OrdinalIgnoreCase);

    private static string GetSourceMessage(MediaSourceReferenceState state) =>
        state switch
        {
            MediaSourceReferenceState.Available =>
                "Plan saved locally. No conversion will run.",
            MediaSourceReferenceState.Unavailable =>
                "Source access expired. Select the original again before future conversion.",
            _ => "Source reference is invalid. Review or remove this plan."
        };
}
