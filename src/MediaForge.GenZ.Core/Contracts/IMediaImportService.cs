using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IMediaImportService
{
    Task<IReadOnlyList<MediaAsset>> ImportAsync(CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(MediaAsset asset, CancellationToken cancellationToken = default);
}
