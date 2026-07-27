using Android.Graphics;
using Android.Media;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;
using CoreMediaMetadata = MediaForge.GenZ.Core.Models.MediaMetadata;
using IOPath = System.IO.Path;
using IOStream = System.IO.Stream;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidMediaMetadataReader : IMetadataReader
{
    public Task<CoreMediaMetadata> ReadAsync(
        MediaAsset asset,
        IOStream content,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ReadMetadata(asset, content, cancellationToken),
            cancellationToken);

    private static CoreMediaMetadata ReadMetadata(
        MediaAsset asset,
        IOStream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return IsImage(asset)
                ? ReadImageMetadata(asset, content)
                : ReadAudioVideoMetadata(asset, content);
        }
        catch
        {
            return BasicMetadata(asset);
        }
    }

    private static CoreMediaMetadata ReadImageMetadata(MediaAsset asset, IOStream content)
    {
        var options = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeStream(content, null, options);

        return new CoreMediaMetadata(
            null,
            PositiveOrNull(options.OutWidth),
            PositiveOrNull(options.OutHeight),
            null,
            DescribeFormat(options.OutMimeType, asset.DisplayName));
    }

    private static CoreMediaMetadata ReadAudioVideoMetadata(MediaAsset asset, IOStream content)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23) || !content.CanSeek)
        {
            return BasicMetadata(asset);
        }

        using var source = new StreamMediaDataSource(content);
        using var retriever = new MediaMetadataRetriever();
        retriever.SetDataSource(source);

        var durationMilliseconds = ParseLong(
            retriever.ExtractMetadata(MetadataKey.Duration));
        TimeSpan? duration = durationMilliseconds is > 0
            ? TimeSpan.FromMilliseconds(durationMilliseconds.Value)
            : null;

        return new CoreMediaMetadata(
            duration,
            ParseInt(retriever.ExtractMetadata(MetadataKey.VideoWidth)),
            ParseInt(retriever.ExtractMetadata(MetadataKey.VideoHeight)),
            ParseLong(retriever.ExtractMetadata(MetadataKey.Bitrate)),
            DescribeFormat(
                retriever.ExtractMetadata(MetadataKey.Mimetype),
                asset.DisplayName));
    }

    private static CoreMediaMetadata BasicMetadata(MediaAsset asset) =>
        new(
            null,
            null,
            null,
            null,
            DescribeFormat(asset.ContentType, asset.DisplayName));

    private static bool IsImage(MediaAsset asset) =>
        asset.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true ||
        new[] { ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp" }
            .Contains(IOPath.GetExtension(asset.DisplayName), StringComparer.OrdinalIgnoreCase);

    private static string DescribeFormat(string? mimeType, string fileName)
    {
        var extension = IOPath.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return string.IsNullOrWhiteSpace(extension) ? "Unknown" : extension;
        }

        var shortMime = mimeType[(mimeType.IndexOf('/') + 1)..].ToUpperInvariant();
        return string.Equals(shortMime, extension, StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(extension)
            ? shortMime
            : $"{shortMime} / {extension}";
    }

    private static int? PositiveOrNull(int value) => value > 0 ? value : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
}
