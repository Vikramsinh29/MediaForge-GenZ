using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class UnavailableOutputActions : IOutputOpener, IShareService
{
    public Task<ValidationResult> OpenAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ValidationResult.Failure(
            "Opening completed output is currently available only on Android."));

    public Task<ValidationResult> ShareAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ValidationResult.Failure(
            "Sharing completed output is currently available only on Android."));
}
