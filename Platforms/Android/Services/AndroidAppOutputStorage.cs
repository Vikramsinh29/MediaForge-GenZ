#if ANDROID
using System.Security.Cryptography;
using System.Runtime.Versioning;
using Android.Content;
using Android.OS;
using Android.Provider;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidAppOutputStorage : IOutputStorage
{
    private const string OutputMimeType = "audio/mp4";
    private const string RelativeOutputPath = "Music/MediaForge GenZ/";
    private readonly Context _context = global::Android.App.Application.Context;
    private readonly string _temporaryRoot = Path.Combine(
        global::Android.App.Application.Context.CacheDir!.AbsolutePath,
        "conversion-output");
    private readonly string _legacyFinalRoot = Path.Combine(
        FileSystem.AppDataDirectory,
        "DevelopmentExports");

    public Task<TemporaryOutput> CreateTemporaryAsync(
        ConversionJob job,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_temporaryRoot);
        return Task.FromResult(
            new TemporaryOutput(
                Guid.NewGuid().ToString("N"),
                job.Plan.ProposedOutputFileName,
                OutputMimeType));
    }

    public Task<Stream> OpenTemporaryWriteAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            GetTemporaryPath(temporaryOutput),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public async Task<MediaAsset> FinalizeAtomicallyAsync(
        TemporaryOutput temporaryOutput,
        ExportPlan approvedPlan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSafePlan(approvedPlan);
        var temporaryPath = GetTemporaryPath(temporaryOutput);
        ValidateM4A(temporaryPath);

        return OperatingSystem.IsAndroidVersionAtLeast(29)
            ? await PublishToMediaStoreAsync(
                temporaryPath,
                approvedPlan.ProposedOutputFileName,
                cancellationToken)
            : await PublishToAppStorageAsync(
                temporaryPath,
                approvedPlan.ProposedOutputFileName,
                cancellationToken);
    }

    public Task DiscardTemporaryAsync(
        TemporaryOutput temporaryOutput,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetTemporaryPath(temporaryOutput));
        return Task.CompletedTask;
    }

    public Task DiscardFinalizedAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = global::Android.Net.Uri.Parse(output.Id);
        if (uri?.Scheme?.Equals("content", StringComparison.OrdinalIgnoreCase) is true)
        {
            _context.ContentResolver?.Delete(uri, null, null);
        }
        else if (uri?.Scheme?.Equals("file", StringComparison.OrdinalIgnoreCase) is true &&
                 !string.IsNullOrWhiteSpace(uri.Path))
        {
            var fullPath = Path.GetFullPath(uri.Path);
            var root = Path.GetFullPath(_legacyFinalRoot) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }

    [SupportedOSPlatform("android29.0")]
    private async Task<MediaAsset> PublishToMediaStoreAsync(
        string temporaryPath,
        string proposedName,
        CancellationToken cancellationToken)
    {
        var resolver = _context.ContentResolver ??
            throw new IOException("Android media storage is unavailable.");
        var collection = MediaStore.Audio.Media.GetContentUri(
            MediaStore.VolumeExternalPrimary) ??
            throw new IOException("Android audio storage is unavailable.");
        global::Android.Net.Uri? outputUri = null;

        try
        {
            var displayName = FindAvailableDisplayName(resolver, collection, proposedName);
            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
            values.Put(MediaStore.IMediaColumns.MimeType, OutputMimeType);
            values.Put(MediaStore.IMediaColumns.RelativePath, RelativeOutputPath);
            values.Put(MediaStore.IMediaColumns.IsPending, 1);

            outputUri = resolver.Insert(collection, values) ??
                throw new IOException("Android could not reserve the output file.");

            await using (var source = new FileStream(
                             temporaryPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = resolver.OpenOutputStream(outputUri, "w") ??
                throw new IOException("Android could not open the reserved output file."))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            var size = ValidatePublishedM4A(resolver, outputUri);
            var publishValues = new ContentValues();
            publishValues.Put(MediaStore.IMediaColumns.IsPending, 0);
            if (resolver.Update(outputUri, publishValues, null, null) != 1)
            {
                throw new IOException("Android could not publish the completed output.");
            }

            File.Delete(temporaryPath);
            return new MediaAsset(outputUri.ToString()!, displayName, OutputMimeType, size);
        }
        catch
        {
            if (outputUri is not null)
            {
                try
                {
                    resolver.Delete(outputUri, null, null);
                }
                catch
                {
                    // A failed pending item is removed on a best-effort basis.
                }
            }

            throw;
        }
    }

    private Task<MediaAsset> PublishToAppStorageAsync(
        string temporaryPath,
        string proposedName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_legacyFinalRoot);
        var finalPath = GetCollisionSafePath(_legacyFinalRoot, proposedName);
        File.Move(temporaryPath, finalPath, false);
        ValidateM4A(finalPath);
        var info = new FileInfo(finalPath);
        return Task.FromResult(
            new MediaAsset(
                new Uri(finalPath).AbsoluteUri,
                info.Name,
                OutputMimeType,
                info.Length));
    }

    [SupportedOSPlatform("android29.0")]
    private static string FindAvailableDisplayName(
        ContentResolver resolver,
        global::Android.Net.Uri collection,
        string proposedName)
    {
        var baseName = Path.GetFileNameWithoutExtension(proposedName);
        for (var suffix = 1; suffix < 10_000; suffix++)
        {
            var candidate = suffix == 1
                ? $"{baseName}.m4a"
                : $"{baseName}-{suffix}.m4a";
            using var cursor = resolver.Query(
                collection,
                ["_id"],
                $"{MediaStore.IMediaColumns.DisplayName}=? AND {MediaStore.IMediaColumns.RelativePath}=?",
                [candidate, RelativeOutputPath],
                null);
            if (cursor is null || !cursor.MoveToFirst())
            {
                return candidate;
            }
        }

        throw new IOException("A collision-free output filename could not be created.");
    }

    private static long ValidatePublishedM4A(
        ContentResolver resolver,
        global::Android.Net.Uri uri)
    {
        var size = GetSize(resolver, uri);
        if (size <= 0)
        {
            throw new InvalidDataException("The published output is empty.");
        }

        using var descriptor = resolver.OpenAssetFileDescriptor(uri, "r") ??
            throw new InvalidDataException("The published output cannot be reopened.");
        var fileDescriptor = descriptor.FileDescriptor ??
            throw new InvalidDataException("The published output descriptor is unavailable.");
        using var extractor = new global::Android.Media.MediaExtractor();
        extractor.SetDataSource(
            fileDescriptor,
            descriptor.StartOffset,
            descriptor.Length > 0 ? descriptor.Length : size);
        EnsureAacTrack(extractor);
        return size;
    }

    private static long GetSize(ContentResolver resolver, global::Android.Net.Uri uri)
    {
        using var cursor = resolver.Query(
            uri,
            [MediaStore.IMediaColumns.Size],
            null,
            null,
            null);
        return cursor is not null && cursor.MoveToFirst() && !cursor.IsNull(0)
            ? cursor.GetLong(0)
            : throw new InvalidDataException("The published output size is unavailable.");
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

    private static string GetCollisionSafePath(string root, string proposedName)
    {
        var baseName = Path.GetFileNameWithoutExtension(proposedName);
        var candidate = Path.Combine(root, baseName + ".m4a");
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(root, $"{baseName}-{suffix}.m4a");
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
        EnsureAacTrack(extractor);
    }

    private static void EnsureAacTrack(global::Android.Media.MediaExtractor extractor)
    {
        for (var index = 0; index < extractor.TrackCount; index++)
        {
            var format = extractor.GetTrackFormat(index);
            if (format.GetString(global::Android.Media.MediaFormat.KeyMime)
                ?.Equals("audio/mp4a-latm", StringComparison.OrdinalIgnoreCase) is true)
            {
                return;
            }
        }

        throw new InvalidDataException("The output does not contain readable AAC audio.");
    }
}
#endif
