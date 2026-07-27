using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class UnavailableTranscoder : ITranscoder
{
    public Task<ConversionExecutionResult> ProcessAsync(
        ConversionJob job,
        Stream source,
        Stream temporaryOutput,
        IProgress<ConversionJobProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new ConversionExecutionResult(
                job.Id,
                false,
                "The development-only Android native adapter is unavailable."));
}
