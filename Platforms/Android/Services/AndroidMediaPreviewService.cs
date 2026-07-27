using Android.Graphics;
using Android.Media;
using MediaForge.GenZ.Core.Models;
using MediaForge.Universal.Services;
using IOPath = System.IO.Path;
using IOStream = System.IO.Stream;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidMediaPreviewService : IMediaPreviewService
{
    private const int MaxPreviewWidth = 720;
    private const int MaxPreviewHeight = 480;

    public Task<byte[]?> CreatePreviewAsync(
        MediaAsset asset,
        IOStream content,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => CreatePreview(asset, content, cancellationToken),
            cancellationToken);

    private static byte[]? CreatePreview(
        MediaAsset asset,
        IOStream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!content.CanSeek)
            {
                return null;
            }

            return IsImage(asset)
                ? CreateImagePreview(content, cancellationToken)
                : IsVideo(asset)
                    ? CreateVideoPreview(content, cancellationToken)
                    : null;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? CreateImagePreview(
        IOStream content,
        CancellationToken cancellationToken)
    {
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeStream(content, null, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        {
            return null;
        }

        var sampleSize = 1;
        while (bounds.OutWidth / sampleSize > MaxPreviewWidth * 2 ||
               bounds.OutHeight / sampleSize > MaxPreviewHeight * 2)
        {
            sampleSize *= 2;
        }

        cancellationToken.ThrowIfCancellationRequested();
        content.Seek(0, SeekOrigin.Begin);
        var options = new BitmapFactory.Options
        {
            InSampleSize = sampleSize,
            InPreferredConfig = Bitmap.Config.Argb8888
        };

        using var decoded = BitmapFactory.DecodeStream(content, null, options);
        if (decoded is null)
        {
            return null;
        }

        return CompressBounded(decoded);
    }

    private static byte[]? CreateVideoPreview(
        IOStream content,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var source = new StreamMediaDataSource(content);
        using var retriever = new MediaMetadataRetriever();
        retriever.SetDataSource(source);
        using var frame = retriever.GetScaledFrameAtTime(
            -1,
            Option.ClosestSync,
            MaxPreviewWidth,
            MaxPreviewHeight);

        return frame is null ? null : Compress(frame);
    }

    private static byte[] CompressBounded(Bitmap bitmap)
    {
        var scale = Math.Min(
            Math.Min(
                (double)MaxPreviewWidth / bitmap.Width,
                (double)MaxPreviewHeight / bitmap.Height),
            1);

        if (scale >= 1)
        {
            return Compress(bitmap);
        }

        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        using var scaled = Bitmap.CreateScaledBitmap(bitmap, width, height, true);
        return Compress(scaled);
    }

    private static byte[] Compress(Bitmap bitmap)
    {
        using var output = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Jpeg!, 82, output);
        return output.ToArray();
    }

    private static bool IsImage(MediaAsset asset) =>
        asset.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true;

    private static bool IsVideo(MediaAsset asset) =>
        asset.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) is true ||
        new[] { ".3gp", ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".webm" }
            .Contains(IOPath.GetExtension(asset.DisplayName), StringComparer.OrdinalIgnoreCase);
}
