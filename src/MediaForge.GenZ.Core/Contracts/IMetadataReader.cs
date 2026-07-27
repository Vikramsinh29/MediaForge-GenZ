using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IMetadataReader
{
    Task<MediaMetadata> ReadAsync(
        MediaAsset asset,
        Stream content,
        CancellationToken cancellationToken = default);
}
