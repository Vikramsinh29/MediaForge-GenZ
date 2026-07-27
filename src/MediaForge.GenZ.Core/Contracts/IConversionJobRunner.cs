using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IConversionJobRunner
{
    bool CanRun(ConversionJob job);

    Task<ConversionExecutionResult> RunAsync(
        string jobId,
        IProgress<ConversionJobProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
