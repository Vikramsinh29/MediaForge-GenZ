using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class FallbackMediaInspector : IMetadataReader, IMediaPreviewService
{
    public Task<MediaMetadata> ReadAsync(
        MediaAsset asset,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = Path.GetExtension(asset.DisplayName).TrimStart('.').ToUpperInvariant();
        return Task.FromResult(new MediaMetadata(null, null, null, null, format));
    }

    public Task<byte[]?> CreatePreviewAsync(
        MediaAsset asset,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>(null);
    }
}
