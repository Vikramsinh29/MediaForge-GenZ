using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class MediaLibraryItemViewModel : BaseViewModel
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public MediaLibraryItemViewModel(
        MediaAsset asset,
        Action selectionChanged,
        Func<MediaLibraryItemViewModel, Task> openDetails)
    {
        Asset = asset;
        _selectionChanged = selectionChanged;
        OpenDetailsCommand = new Command(async () => await openDetails(this));
    }

    public MediaAsset Asset { get; }

    public Command OpenDetailsCommand { get; }

    public string FileName => Asset.DisplayName;

    public string MediaType => GetMediaType(Asset);

    public string FileSize => FormatFileSize(Asset.SizeInBytes);

    public string TypeBadge => MediaType.ToUpperInvariant();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }

    private static string GetMediaType(MediaAsset asset)
    {
        if (asset.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return "Video";
        }

        if (asset.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return "Audio";
        }

        if (asset.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return "Image";
        }

        var extension = Path.GetExtension(asset.DisplayName).ToLowerInvariant();
        return extension switch
        {
            ".mp3" or ".m4a" or ".aac" or ".wav" or ".flac" or ".ogg" or ".opus" or ".aiff" => "Audio",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".heic" or ".heif" or ".bmp" or ".tif" or ".tiff" => "Image",
            _ => "Video"
        };
    }

    private static string FormatFileSize(long? bytes)
    {
        if (bytes is null or <= 0)
        {
            return "Size unavailable";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes.Value;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
