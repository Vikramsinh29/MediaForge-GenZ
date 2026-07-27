using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface ITranscoder
{
    Task<ConversionResult> TranscodeAsync(
        TranscodeRequest request,
        Stream source,
        Stream destination,
        IProgress<TranscodeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
