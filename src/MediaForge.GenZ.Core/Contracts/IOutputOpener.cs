using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IOutputOpener
{
    Task<ValidationResult> OpenAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default);
}
