using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;
using MediaForge.Universal.Services;

namespace MediaForge.Universal.ViewModels;

public sealed class MediaDetailsViewModel : BaseViewModel
{
    private readonly IMediaImportService _mediaImportService;
    private readonly IMetadataReader _metadataReader;
    private readonly IMediaPreviewService _previewService;
    private CancellationTokenSource? _loadCancellation;
    private bool _hasCodec;
    private bool _hasDimensions;
    private bool _hasDuration;
    private bool _hasPreview;
    private bool _isAudio;
    private bool _isLoading;
    private bool _isVisible;
    private bool _showPreviewPlaceholder;
    private string _codec = string.Empty;
    private string _dimensions = string.Empty;
    private string _duration = string.Empty;
    private string _errorMessage = string.Empty;
    private string _fileName = string.Empty;
    private string _fileSize = string.Empty;
    private string _mediaType = string.Empty;
    private ImageSource? _previewImage;

    public MediaDetailsViewModel(
        IMediaImportService mediaImportService,
        IMetadataReader metadataReader,
        IMediaPreviewService previewService,
        ExportPlanningViewModel export)
    {
        _mediaImportService = mediaImportService;
        _metadataReader = metadataReader;
        _previewService = previewService;
        Export = export;
        CloseCommand = new Command(Close);
    }

    public Command CloseCommand { get; }

    public ExportPlanningViewModel Export { get; }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasPreview
    {
        get => _hasPreview;
        private set => SetProperty(ref _hasPreview, value);
    }

    public bool IsAudio
    {
        get => _isAudio;
        private set => SetProperty(ref _isAudio, value);
    }

    public bool ShowPreviewPlaceholder
    {
        get => _showPreviewPlaceholder;
        private set => SetProperty(ref _showPreviewPlaceholder, value);
    }

    public bool HasDuration
    {
        get => _hasDuration;
        private set => SetProperty(ref _hasDuration, value);
    }

    public bool HasDimensions
    {
        get => _hasDimensions;
        private set => SetProperty(ref _hasDimensions, value);
    }

    public bool HasCodec
    {
        get => _hasCodec;
        private set => SetProperty(ref _hasCodec, value);
    }

    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    public string MediaType
    {
        get => _mediaType;
        private set => SetProperty(ref _mediaType, value);
    }

    public string FileSize
    {
        get => _fileSize;
        private set => SetProperty(ref _fileSize, value);
    }

    public string Duration
    {
        get => _duration;
        private set => SetProperty(ref _duration, value);
    }

    public string Dimensions
    {
        get => _dimensions;
        private set => SetProperty(ref _dimensions, value);
    }

    public string Codec
    {
        get => _codec;
        private set => SetProperty(ref _codec, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public async Task LoadAsync(MediaLibraryItemViewModel item)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        Reset(item);
        Export.Prepare(item.Asset);
        IsVisible = true;
        IsLoading = true;

        try
        {
            MediaMetadata metadata;
            await using (var metadataStream =
                         await _mediaImportService.OpenReadAsync(item.Asset, cancellationToken))
            {
                metadata = await _metadataReader.ReadAsync(
                    item.Asset,
                    metadataStream,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ApplyMetadata(metadata, item.MediaType);

            if (!IsAudio)
            {
                await using var previewStream =
                    await _mediaImportService.OpenReadAsync(item.Asset, cancellationToken);
                var preview = await _previewService.CreatePreviewAsync(
                    item.Asset,
                    previewStream,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                if (preview is { Length: > 0 })
                {
                    PreviewImage = ImageSource.FromStream(() => new MemoryStream(preview));
                    HasPreview = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            ErrorMessage = "Some details are unavailable. The file may have moved or be damaged.";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
                ShowPreviewPlaceholder = !HasPreview && !IsAudio;
            }
        }
    }

    public void Close()
    {
        _loadCancellation?.Cancel();
        Export.Close();
        IsVisible = false;
        IsLoading = false;
        PreviewImage = null;
    }

    private void Reset(MediaLibraryItemViewModel item)
    {
        FileName = item.FileName;
        MediaType = item.MediaType;
        FileSize = item.FileSize;
        IsAudio = item.MediaType == "Audio";
        Duration = string.Empty;
        Dimensions = string.Empty;
        Codec = string.Empty;
        ErrorMessage = string.Empty;
        HasDuration = false;
        HasDimensions = false;
        HasCodec = false;
        HasPreview = false;
        ShowPreviewPlaceholder = false;
        PreviewImage = null;
    }

    private void ApplyMetadata(MediaMetadata metadata, string mediaType)
    {
        if (metadata.Duration is { } duration)
        {
            Duration = duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
            HasDuration = true;
        }

        if (metadata.Width is > 0 && metadata.Height is > 0)
        {
            Dimensions = $"{metadata.Width} × {metadata.Height}";
            HasDimensions = true;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Codec))
        {
            Codec = metadata.Codec;
            HasCodec = true;
        }

        if (!HasDuration && mediaType is "Video" or "Audio")
        {
            Duration = "Unavailable";
            HasDuration = true;
        }

        if (!HasDimensions && mediaType is "Video" or "Image")
        {
            Dimensions = "Unavailable";
            HasDimensions = true;
        }
    }
}
