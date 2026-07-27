using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Services;

public sealed class InMemoryConversionJobQueue : IConversionJobQueue
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

    private readonly Dictionary<string, ConversionJob> _jobs = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public IReadOnlyList<ConversionJob> GetSnapshot()
    {
        lock (_sync)
        {
            return _jobs.Values
                .OrderByDescending(job => job.CreatedAt)
                .ToArray();
        }
    }

    public ConversionJob Enqueue(ExportPlan plan)
    {
        if (plan.OverwriteOriginal)
        {
            throw new ArgumentException(
                "A conversion job cannot overwrite its source.",
                nameof(plan));
        }

        if (string.IsNullOrWhiteSpace(plan.ProposedOutputFileName) ||
            string.Equals(
                plan.ProposedOutputFileName,
                plan.Source.DisplayName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A conversion job requires a distinct proposed output name.",
                nameof(plan));
        }

        var now = DateTimeOffset.UtcNow;
        var job = new ConversionJob(
            Guid.NewGuid().ToString("N"),
            plan,
            ConversionJobState.Queued,
            now,
            now,
            StatusMessage: "Waiting for an approved media engine.");

        lock (_sync)
        {
            _jobs.Add(job.Id, job);
        }

        return job;
    }

    public ValidationResult Transition(
        string jobId,
        ConversionJobState nextState,
        double? progress = null,
        string? statusMessage = null,
        string? errorMessage = null)
    {
        lock (_sync)
        {
            if (!_jobs.TryGetValue(jobId, out var current))
            {
                return ValidationResult.Failure("The conversion job was not found.");
            }

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

            _jobs[jobId] = current with
            {
                State = nextState,
                Progress = nextProgress,
                StatusMessage = statusMessage,
                ErrorMessage = errorMessage,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            return ValidationResult.Success;
        }
    }

    public ValidationResult Cancel(string jobId)
    {
        lock (_sync)
        {
            if (!_jobs.TryGetValue(jobId, out var current))
            {
                return ValidationResult.Failure("The conversion job was not found.");
            }

            if (current.State == ConversionJobState.Cancelled)
            {
                return ValidationResult.Success;
            }
        }

        return Transition(
            jobId,
            ConversionJobState.Cancelled,
            statusMessage: "Cancelled before finalisation.");
    }
}
