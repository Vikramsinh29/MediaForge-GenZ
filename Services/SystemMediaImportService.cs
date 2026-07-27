using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class SystemMediaImportService : IMediaImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".aac", ".aiff", ".avi", ".bmp", ".flac", ".gif", ".heic", ".heif",
        ".jpeg", ".jpg", ".m4a", ".m4v", ".mkv", ".mov", ".mp3", ".mp4", ".ogg",
        ".opus", ".png", ".tif", ".tiff", ".wav", ".webm", ".webp"
    };

    private static readonly FilePickerFileType SupportedMedia = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["video/*", "audio/*", "image/*"],
            [DevicePlatform.iOS] = ["public.movie", "public.audio", "public.image"]
        });

    private readonly ConcurrentDictionary<string, FileResult> _fileHandles = new();

    public async Task<IReadOnlyList<MediaAsset>> ImportAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<FileResult?> results;
        try
        {
            results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Choose media",
                FileTypes = SupportedMedia
            });
        }
        catch (TaskCanceledException)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var imported = new List<MediaAsset>();
        foreach (var file in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file is null || !IsSupported(file))
            {
                continue;
            }

            var id = CreateStableId(file);
            long? size = await TryReadSizeAsync(file);
            if (size is null)
            {
                continue;
            }

            _fileHandles.TryAdd(id, file);
            imported.Add(new MediaAsset(id, file.FileName, file.ContentType, size));
        }

        return imported;
    }

    public Task<Stream> OpenReadAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_fileHandles.TryGetValue(asset.Id, out var file))
        {
            throw new FileNotFoundException(
                "The selected media file is no longer available.",
                asset.DisplayName);
        }

        return file.OpenReadAsync();
    }

    private static bool IsSupported(FileResult file)
    {
        if (file.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) is true ||
            file.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) is true ||
            file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return true;
        }

        return SupportedExtensions.Contains(Path.GetExtension(file.FileName));
    }

    private static string CreateStableId(FileResult file)
    {
        var identity = string.IsNullOrWhiteSpace(file.FullPath)
            ? $"{file.FileName}|{file.ContentType}"
            : file.FullPath;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash);
    }

    private static async Task<long?> TryReadSizeAsync(FileResult file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            return stream.CanSeek ? stream.Length : 0;
        }
        catch
        {
            return null;
        }
    }
}
