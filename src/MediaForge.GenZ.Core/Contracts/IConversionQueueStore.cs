using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IConversionQueueStore
{
    Task<ConversionQueueLoadResult> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ConversionQueueDocument document,
        CancellationToken cancellationToken = default);
}
