using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IOutputStorage
{
    Task<TemporaryOutput> CreateTemporaryAsync(
        ConversionJob job,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenTemporaryWriteAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default);

    Task<MediaAsset> FinalizeAtomicallyAsync(
        TemporaryOutput temporaryOutput,
        ExportPlan approvedPlan,
        CancellationToken cancellationToken = default);

    Task DiscardTemporaryAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default);
}
