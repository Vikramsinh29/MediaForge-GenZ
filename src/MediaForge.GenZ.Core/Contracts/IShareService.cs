using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IShareService
{
    Task ShareAsync(MediaAsset asset, CancellationToken cancellationToken = default);
}
