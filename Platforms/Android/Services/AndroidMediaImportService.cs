#if ANDROID
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Android.Provider;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidMediaImportService : IMediaImportService, IMediaSourceReferenceValidator
{
    private readonly ConcurrentDictionary<string, global::Android.Net.Uri> _uris = new();

    public async Task<IReadOnlyList<MediaAsset>> ImportAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picked = await MainActivity.PickMediaAsync();
        var resolver = global::Android.App.Application.Context.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver is unavailable.");
        var assets = new List<MediaAsset>();
        foreach (var uri in picked.DistinctBy(value => value.ToString()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                resolver.TakePersistableUriPermission(
                    uri,
                    global::Android.Content.ActivityFlags.GrantReadUriPermission);
            }
            catch
            {
                // Some providers grant session-only scoped access.
            }

            var (name, size) = ReadProperties(resolver, uri);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var id = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(uri.ToString() ?? string.Empty)));
            _uris[id] = uri;
            assets.Add(new MediaAsset(id, name, resolver.GetType(uri), size));
        }
        return assets;
    }

    public Task<Stream> OpenReadAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_uris.TryGetValue(asset.Id, out var uri))
            throw new FileNotFoundException("The selected media is no longer accessible.", asset.DisplayName);
        var resolver = global::Android.App.Application.Context.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver is unavailable.");
        Stream stream = resolver.OpenInputStream(uri)
            ?? throw new FileNotFoundException("The selected media could not be opened.", asset.DisplayName);
        return Task.FromResult(stream);
    }

    public Task<MediaSourceReferenceState> GetStateAsync(MediaAsset source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_uris.ContainsKey(source.Id)
            ? MediaSourceReferenceState.Available
            : MediaSourceReferenceState.Unavailable);
    }

    private static (string? Name, long? Size) ReadProperties(
        global::Android.Content.ContentResolver resolver,
        global::Android.Net.Uri uri)
    {
        using var cursor = resolver.Query(
            uri,
            [IOpenableColumns.DisplayName, IOpenableColumns.Size],
            null,
            null,
            null);
        if (cursor is null || !cursor.MoveToFirst()) return (uri.LastPathSegment, null);
        var nameIndex = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
        var sizeIndex = cursor.GetColumnIndex(IOpenableColumns.Size);
        return (
            nameIndex >= 0 ? cursor.GetString(nameIndex) : uri.LastPathSegment,
            sizeIndex >= 0 && !cursor.IsNull(sizeIndex) ? cursor.GetLong(sizeIndex) : null);
    }
}
#endif
