using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IConversionJobQueue
{
    IReadOnlyList<ConversionJob> GetSnapshot();

    Task<ConversionQueueLoadResult> InitializeAsync(
        CancellationToken cancellationToken = default);

    Task<ConversionJob> EnqueueAsync(
        ExportPlan plan,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> MoveAsync(
        string jobId,
        int newIndex,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> UpdatePlanAsync(
        string jobId,
        ExportPlan plan,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> RemoveAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task<ValidationResult> TransitionAsync(
        string jobId,
        ConversionJobState nextState,
        double? progress = null,
        string? statusMessage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> CancelAsync(
        string jobId,
        CancellationToken cancellationToken = default);
}
