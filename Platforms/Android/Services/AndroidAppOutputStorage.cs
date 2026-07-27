#if ANDROID
using System.Security.Cryptography;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidAppOutputStorage : IOutputStorage
{
    private readonly string _temporaryRoot = Path.Combine(
        global::Android.App.Application.Context.CacheDir!.AbsolutePath,
        "conversion-output");
    private readonly string _finalRoot = Path.Combine(
        FileSystem.AppDataDirectory,
        "DevelopmentExports");

    public Task<TemporaryOutput> CreateTemporaryAsync(
        ConversionJob job,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_temporaryRoot);
        var id = Guid.NewGuid().ToString("N");
        return Task.FromResult(
            new TemporaryOutput(id, job.Plan.ProposedOutputFileName, "audio/mp4"));
    }

    public Task<Stream> OpenTemporaryWriteAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetTemporaryPath(temporaryOutput);
        Stream stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task<MediaAsset> FinalizeAtomicallyAsync(
        TemporaryOutput temporaryOutput,
        ExportPlan approvedPlan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSafePlan(approvedPlan);
        var temporaryPath = GetTemporaryPath(temporaryOutput);
        ValidateM4A(temporaryPath);
        Directory.CreateDirectory(_finalRoot);

        var finalPath = GetCollisionSafePath(approvedPlan.ProposedOutputFileName);
        File.Move(temporaryPath, finalPath, false);
        var info = new FileInfo(finalPath);
        var id = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(finalPath)));
        return Task.FromResult(
            new MediaAsset(id, info.Name, "audio/mp4", info.Length));
    }

    public Task DiscardTemporaryAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetTemporaryPath(temporaryOutput));
        return Task.CompletedTask;
    }

    private string GetTemporaryPath(TemporaryOutput temporaryOutput)
    {
        if (string.IsNullOrWhiteSpace(temporaryOutput.Id) ||
            temporaryOutput.Id.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The temporary output identifier is invalid.");
        }

        return Path.Combine(_temporaryRoot, temporaryOutput.Id + ".m4a.tmp");
    }

    private string GetCollisionSafePath(string proposedName)
    {
        var baseName = Path.GetFileNameWithoutExtension(proposedName);
        var extension = ".m4a";
        var candidate = Path.Combine(_finalRoot, baseName + extension);
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(_finalRoot, $"{baseName}-{suffix}{extension}");
        }

        return candidate;
    }

    private static void EnsureSafePlan(ExportPlan plan)
    {
        if (plan.OverwriteOriginal ||
            plan.OutputFormat != OutputFormat.M4A ||
            !Path.GetExtension(plan.Source.DisplayName)
                .Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                plan.Source.DisplayName,
                plan.ProposedOutputFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output plan failed WAV-to-M4A safety validation.");
        }
    }

    private static void ValidateM4A(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 12)
        {
            throw new InvalidDataException("The temporary M4A output is empty.");
        }

        using var extractor = new global::Android.Media.MediaExtractor();
        extractor.SetDataSource(path);
        var hasAacAudio = false;
        for (var index = 0; index < extractor.TrackCount; index++)
        {
            var format = extractor.GetTrackFormat(index);
            var mime = format.GetString(global::Android.Media.MediaFormat.KeyMime);
            if (mime?.Equals("audio/mp4a-latm", StringComparison.OrdinalIgnoreCase) is true)
            {
                hasAacAudio = true;
                break;
            }
        }

        if (!hasAacAudio)
        {
            throw new InvalidDataException("The temporary output does not contain readable AAC audio.");
        }
    }
}
#endif
