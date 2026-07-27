using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IMediaSourceReferenceValidator
{
    Task<MediaSourceReferenceState> GetStateAsync(
        MediaAsset source,
        CancellationToken cancellationToken = default);
}
