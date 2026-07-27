using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface ITranscoder
{
    Task<ConversionExecutionResult> ProcessAsync(
        ConversionJob job,
        Stream source,
        Stream temporaryOutput,
        IProgress<ConversionJobProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
