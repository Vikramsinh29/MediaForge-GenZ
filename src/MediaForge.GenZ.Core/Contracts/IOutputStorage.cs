using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IOutputStorage
{
    Task<Stream> OpenWriteAsync(
        string suggestedFileName,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<MediaAsset> CompleteAsync(
        string suggestedFileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}
