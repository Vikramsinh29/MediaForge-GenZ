using System.Collections.ObjectModel;
using MediaForge.GenZ.Core.Contracts;

namespace MediaForge.Universal.ViewModels;

public sealed class HomeViewModel : BaseViewModel
{
    private readonly IMediaImportService _mediaImportService;
    private readonly HashSet<string> _knownAssetIds = new(StringComparer.Ordinal);
    private bool _hasMedia;
    private bool _hasSelection;
    private bool _isBusy;
    private bool _isStatusVisible;
    private bool _showImportActions = true;
    private bool _showWelcome = true;
    private string _importedCountLabel = "0 media files";
    private string _statusMessage = string.Empty;

    public HomeViewModel(
        IMediaImportService mediaImportService,
        MediaDetailsViewModel details)
    {
        _mediaImportService = mediaImportService;
        Details = details;
        Details.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MediaDetailsViewModel.IsVisible))
            {
                RefreshPageChrome();
            }
        };
        Details.Export.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ExportPlanningViewModel.IsVisible))
            {
                RefreshPageChrome();
            }
        };
        ImportMediaCommand = new Command(
            async () => await ImportMediaAsync(),
            () => !IsBusy);
        ClearSelectedCommand = new Command(ClearSelected, () => HasSelection && !IsBusy);
        ConvertSelectedCommand = new Command(
            async () => await OpenSelectedExportAsync(),
            () => HasSelection && !IsBusy);
        ClearAllCommand = new Command(ClearAll, () => HasMedia && !IsBusy);
    }

    public ObservableCollection<MediaLibraryItemViewModel> MediaItems { get; } = [];

    public MediaDetailsViewModel Details { get; }

    public Command ImportMediaCommand { get; }

    public Command ClearSelectedCommand { get; }
    public Command ConvertSelectedCommand { get; }

    public Command ClearAllCommand { get; }

    public string ProductName => "MediaForge GenZ";

    public string Headline => "Create freely.\nConvert privately.";

    public string Description =>
        "Choose videos, audio, and images with Android's secure picker. Your files stay on your device.";

    public Task InitializeAsync() => Details.Export.InitializeAsync();

    public bool HasMedia
    {
        get => _hasMedia;
        private set => SetProperty(ref _hasMedia, value);
    }

    public bool ShowWelcome
    {
        get => _showWelcome;
        private set => SetProperty(ref _showWelcome, value);
    }

    public bool ShowImportActions
    {
        get => _showImportActions;
        private set => SetProperty(ref _showImportActions, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsStatusVisible
    {
        get => _isStatusVisible;
        private set => SetProperty(ref _isStatusVisible, value);
    }

    public string ImportedCountLabel
    {
        get => _importedCountLabel;
        private set => SetProperty(ref _importedCountLabel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private async Task ImportMediaAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsStatusVisible = false;
        RefreshCommands();

        try
        {
            var assets = await _mediaImportService.ImportAsync();
            var added = 0;

            foreach (var asset in assets)
            {
                if (_knownAssetIds.Add(asset.Id))
                {
                    var item = new MediaLibraryItemViewModel(
                        asset,
                        UpdateSelectionState,
                        OpenDetailsAsync,
                        OpenExportAsync);
                    MediaItems.Add(item);
                    item.IsSelected = true;
                    added++;
                }
            }

            StatusMessage = added == 0
                ? "No new supported media was added."
                : $"{added} media file{(added == 1 ? string.Empty : "s")} added and selected.";
            IsStatusVisible = true;
            UpdateLibraryState();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Import cancelled. Your library was not changed.";
            IsStatusVisible = true;
        }
        catch
        {
            StatusMessage = "Some media could not be imported. Please try choosing it again.";
            IsStatusVisible = true;
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private Task OpenDetailsAsync(MediaLibraryItemViewModel item) =>
        Details.LoadAsync(item);

    private async Task OpenExportAsync(MediaLibraryItemViewModel item)
    {
        await Details.LoadAsync(item);
        var selected = MediaItems.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Asset).ToArray();
        var sources = item.IsSelected && selected.Length > 1 ? selected : [item.Asset];
        Details.Export.PrepareBatch(sources, item.Asset.Id);
        Details.Export.OpenCommand.Execute(null);
    }

    private async Task OpenSelectedExportAsync()
    {
        var selectedItems = MediaItems.Where(item => item.IsSelected).ToArray();
        if (selectedItems.Length == 0) return;
        await Details.LoadAsync(selectedItems[0]);
        Details.Export.PrepareBatch(selectedItems.Select(item => item.Asset).ToArray(), selectedItems[0].Asset.Id);
        Details.Export.OpenCommand.Execute(null);
    }

    private void ClearSelected()
    {
        Details.Close();
        var selected = MediaItems.Where(item => item.IsSelected).ToArray();
        foreach (var item in selected)
        {
            MediaItems.Remove(item);
            _knownAssetIds.Remove(item.Asset.Id);
        }

        StatusMessage = $"{selected.Length} selected file{(selected.Length == 1 ? string.Empty : "s")} cleared.";
        IsStatusVisible = selected.Length > 0;
        UpdateLibraryState();
    }

    private void ClearAll()
    {
        Details.Close();
        MediaItems.Clear();
        _knownAssetIds.Clear();
        StatusMessage = "Library cleared. Your original files were not changed.";
        IsStatusVisible = true;
        UpdateLibraryState();
    }

    private void UpdateSelectionState()
    {
        HasSelection = MediaItems.Any(item => item.IsSelected);
        RefreshCommands();
    }

    private void UpdateLibraryState()
    {
        HasMedia = MediaItems.Count > 0;
        ShowWelcome = !HasMedia;
        ImportedCountLabel = $"{MediaItems.Count} media file{(MediaItems.Count == 1 ? string.Empty : "s")}";
        UpdateSelectionState();
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        ImportMediaCommand.ChangeCanExecute();
        ClearSelectedCommand.ChangeCanExecute();
        ConvertSelectedCommand.ChangeCanExecute();
        ClearAllCommand.ChangeCanExecute();
    }

    private void RefreshPageChrome() =>
        ShowImportActions = !Details.IsVisible && !Details.Export.IsVisible;
}
