using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class UnavailableOutputStorage : IOutputStorage
{
    private static PlatformNotSupportedException CreateException() =>
        new("Development conversion output is implemented only for Android.");

    public Task<TemporaryOutput> CreateTemporaryAsync(
        ConversionJob job,
        CancellationToken cancellationToken = default) =>
        Task.FromException<TemporaryOutput>(CreateException());

    public Task<Stream> OpenTemporaryWriteAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default) =>
        Task.FromException<Stream>(CreateException());

    public Task<MediaAsset> FinalizeAtomicallyAsync(
        TemporaryOutput temporaryOutput,
        ExportPlan approvedPlan,
        CancellationToken cancellationToken = default) =>
        Task.FromException<MediaAsset>(CreateException());

    public Task DiscardTemporaryAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DiscardFinalizedAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
