using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public interface IMediaPreviewService
{
    Task<byte[]?> CreatePreviewAsync(
        MediaAsset asset,
        Stream content,
        CancellationToken cancellationToken = default);
}
