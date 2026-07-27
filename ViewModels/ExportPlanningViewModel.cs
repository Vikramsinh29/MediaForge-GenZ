using System.Collections.ObjectModel;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class ExportPlanningViewModel : BaseViewModel
{
    private readonly IExportPlanner _exportPlanner;
    private readonly IConversionJobQueue _conversionJobQueue;
    private readonly IConversionJobRunner _conversionJobRunner;
    private readonly Dictionary<string, CancellationTokenSource> _runningJobs = new();
    private ExportPlan? _currentPlan;
    private MediaAsset? _source;
    private string? _editingJobId;
    private bool _hasQueuedJobs;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isPlanEditorVisible;
    private bool _isPlanValid;
    private bool _isQueueEmpty = true;
    private bool _isVisible;
    private string _aspectRatio = string.Empty;
    private string _outputFileName = string.Empty;
    private string _outputFormat = string.Empty;
    private string _presetDescription = string.Empty;
    private string _quality = string.Empty;
    private string _queueActionLabel = "Add plan to queue";
    private string _queueCountLabel = "0 export jobs";
    private string _queueMessage = string.Empty;
    private string _settingsSummary = string.Empty;
    private string _validationMessage = string.Empty;
    private ExportPreset? _selectedPreset;

    public ExportPlanningViewModel(
        IExportPlanner exportPlanner,
        IConversionJobQueue conversionJobQueue,
        IConversionJobRunner conversionJobRunner)
    {
        _exportPlanner = exportPlanner;
        _conversionJobQueue = conversionJobQueue;
        _conversionJobRunner = conversionJobRunner;
        OpenCommand = new Command(Open);
        OpenQueueCommand = new Command(OpenQueue);
        CloseCommand = new Command(Close);
        CancelEditCommand = new Command(CancelEdit);
        QueuePlanCommand = new Command(
            async () => await SavePlanAsync(),
            () => IsPlanValid && _currentPlan is not null && !IsBusy);
        ClearQueueCommand = new Command(
            async () => await ClearQueueAsync(),
            () => HasQueuedJobs && !IsBusy);
    }

    public ObservableCollection<ExportPreset> CompatiblePresets { get; } = [];

    public ObservableCollection<QueuedExportViewModel> QueuedJobs { get; } = [];

    public Command OpenCommand { get; }

    public Command OpenQueueCommand { get; }

    public Command CloseCommand { get; }

    public Command CancelEditCommand { get; }

    public Command QueuePlanCommand { get; }

    public Command ClearQueueCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public bool IsPlanEditorVisible
    {
        get => _isPlanEditorVisible;
        private set => SetProperty(ref _isPlanEditorVisible, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsPlanValid
    {
        get => _isPlanValid;
        private set
        {
            if (SetProperty(ref _isPlanValid, value))
            {
                QueuePlanCommand.ChangeCanExecute();
            }
        }
    }

    public bool HasQueuedJobs
    {
        get => _hasQueuedJobs;
        private set
        {
            if (SetProperty(ref _hasQueuedJobs, value))
            {
                IsQueueEmpty = !value;
                ClearQueueCommand.ChangeCanExecute();
            }
        }
    }

    public bool IsQueueEmpty
    {
        get => _isQueueEmpty;
        private set => SetProperty(ref _isQueueEmpty, value);
    }

    public ExportPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                BuildPlan();
            }
        }
    }

    public string PresetDescription
    {
        get => _presetDescription;
        private set => SetProperty(ref _presetDescription, value);
    }

    public string OutputFormat
    {
        get => _outputFormat;
        private set => SetProperty(ref _outputFormat, value);
    }

    public string Quality
    {
        get => _quality;
        private set => SetProperty(ref _quality, value);
    }

    public string AspectRatio
    {
        get => _aspectRatio;
        private set => SetProperty(ref _aspectRatio, value);
    }

    public string OutputFileName
    {
        get => _outputFileName;
        private set => SetProperty(ref _outputFileName, value);
    }

    public string SettingsSummary
    {
        get => _settingsSummary;
        private set => SetProperty(ref _settingsSummary, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string QueueCountLabel
    {
        get => _queueCountLabel;
        private set => SetProperty(ref _queueCountLabel, value);
    }

    public string QueueMessage
    {
        get => _queueMessage;
        private set => SetProperty(ref _queueMessage, value);
    }

    public string QueueActionLabel
    {
        get => _queueActionLabel;
        private set => SetProperty(ref _queueActionLabel, value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _conversionJobQueue.InitializeAsync();
            _isInitialized = true;
            QueueMessage = result.Message ?? string.Empty;
            RefreshQueue();
        }
        catch
        {
            QueueMessage = "Saved plans could not be restored. You can still create a new queue.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Prepare(MediaAsset source)
    {
        _source = source;
        _editingJobId = null;
        IsVisible = false;
        IsPlanEditorVisible = true;
        QueueActionLabel = "Add plan to queue";
        LoadPresets(source, null);
    }

    public void Close()
    {
        IsVisible = false;
        CancelEdit();
    }

    private void Open()
    {
        if (_source is not null)
        {
            IsVisible = true;
            IsPlanEditorVisible = true;
            BuildPlan();
        }
    }

    private void OpenQueue()
    {
        IsVisible = true;
        IsPlanEditorVisible = false;
        QueueMessage = HasQueuedJobs
            ? QueueMessage
            : "Your saved export plans will appear here.";
    }

    private void CancelEdit()
    {
        _editingJobId = null;
        IsPlanEditorVisible = false;
        QueueActionLabel = "Add plan to queue";
    }

    private void LoadPresets(MediaAsset source, string? presetId)
    {
        CompatiblePresets.Clear();
        foreach (var preset in _exportPlanner.GetCompatiblePresets(source))
        {
            CompatiblePresets.Add(preset);
        }

        SelectedPreset = CompatiblePresets.FirstOrDefault(
            preset => preset.Id == presetId) ?? CompatiblePresets.FirstOrDefault();
        if (SelectedPreset is null)
        {
            ClearPlan("No compatible export presets are available.");
        }
    }

    private void BuildPlan()
    {
        if (_source is null || SelectedPreset is null)
        {
            ClearPlan("Choose a preset to preview an export plan.");
            return;
        }

        var validation = _exportPlanner.Validate(_source, SelectedPreset);
        if (!validation.IsValid)
        {
            ClearPlan(string.Join(" ", validation.Errors));
            return;
        }

        var plan = _exportPlanner.CreatePlan(_source, SelectedPreset);
        _currentPlan = plan;
        PresetDescription = plan.Preset.Description;
        OutputFormat = FormatOutputFormat(plan.OutputFormat);
        Quality = FormatQuality(plan.Quality);
        AspectRatio = FormatAspectRatio(plan.AspectRatio);
        OutputFileName = plan.ProposedOutputFileName;
        SettingsSummary = plan.SettingsSummary;
        ValidationMessage = "Valid plan - your original file will never be overwritten.";
        IsPlanValid = !plan.OverwriteOriginal;
    }

    private void ClearPlan(string message)
    {
        _currentPlan = null;
        PresetDescription = SelectedPreset?.Description ?? string.Empty;
        OutputFormat = "Unavailable";
        Quality = "Unavailable";
        AspectRatio = "Unavailable";
        OutputFileName = "No output proposed";
        SettingsSummary = string.Empty;
        ValidationMessage = message;
        IsPlanValid = false;
    }

    private async Task SavePlanAsync()
    {
        if (_currentPlan is null || !IsPlanValid || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingJobId is null)
            {
                await _conversionJobQueue.EnqueueAsync(_currentPlan);
                QueueMessage = "Plan saved locally. No media or output file was copied.";
            }
            else
            {
                var result = await _conversionJobQueue.UpdatePlanAsync(
                    _editingJobId,
                    _currentPlan);
                QueueMessage = result.IsValid
                    ? "Queued plan updated."
                    : string.Join(" ", result.Errors);
            }

            CancelEdit();
            RefreshQueue();
        }
        catch
        {
            QueueMessage = "The queue change could not be saved. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task EditAsync(QueuedExportViewModel item)
    {
        _editingJobId = item.Id;
        _source = item.Job.Plan.Source;
        QueueActionLabel = "Save queued plan";
        IsPlanEditorVisible = true;
        LoadPresets(_source, item.Job.Plan.Preset.Id);
        return Task.CompletedTask;
    }

    private Task MoveUpAsync(QueuedExportViewModel item) =>
        MoveAsync(item, item.Position - 2);

    private Task MoveDownAsync(QueuedExportViewModel item) =>
        MoveAsync(item, item.Position);

    private async Task MoveAsync(QueuedExportViewModel item, int newIndex)
    {
        await RunQueueActionAsync(
            () => _conversionJobQueue.MoveAsync(item.Id, newIndex),
            "Queue order updated.");
    }

    private async Task RemoveAsync(QueuedExportViewModel item)
    {
        await RunQueueActionAsync(
            () => _conversionJobQueue.RemoveAsync(item.Id),
            "Planned export removed.");
        if (_editingJobId == item.Id)
        {
            CancelEdit();
        }
    }

    private async Task RunAsync(QueuedExportViewModel item)
    {
        if (_runningJobs.ContainsKey(item.Id))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _runningJobs.Add(item.Id, cancellation);
        item.SetRunning();
        QueueMessage = "Development conversion is running locally. Do not distribute this build.";
        try
        {
            var progress = new Progress<ConversionJobProgress>(item.Report);
            var result = await _conversionJobRunner.RunAsync(
                item.Id,
                progress,
                cancellation.Token);
            var message = result.Success
                ? $"Validated output saved as {result.Output?.DisplayName}."
                : result.ErrorMessage ?? "Development conversion failed.";
            item.Finish(message, result.Success);
            QueueMessage = message;
        }
        finally
        {
            _runningJobs.Remove(item.Id);
            cancellation.Dispose();
            RefreshQueue();
        }
    }

    private void Cancel(QueuedExportViewModel item)
    {
        if (_runningJobs.TryGetValue(item.Id, out var cancellation))
        {
            item.Report(
                new ConversionJobProgress(
                    item.Id,
                    ConversionJobState.Processing,
                    item.Progress,
                    Message: "Cancelling safely..."));
            cancellation.Cancel();
        }
    }

    private async Task ClearQueueAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _conversionJobQueue.ClearAsync();
            CancelEdit();
            QueueMessage = "All planned exports cleared. Original media was untouched.";
            RefreshQueue();
        }
        catch
        {
            QueueMessage = "The queue could not be cleared. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunQueueActionAsync(
        Func<Task<ValidationResult>> action,
        string successMessage)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await action();
            QueueMessage = result.IsValid
                ? successMessage
                : string.Join(" ", result.Errors);
            RefreshQueue();
        }
        catch
        {
            QueueMessage = "The queue change could not be saved. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshQueue()
    {
        QueuedJobs.Clear();
        foreach (var job in _conversionJobQueue.GetSnapshot())
        {
            QueuedJobs.Add(
                new QueuedExportViewModel(
                    job,
                    _conversionJobRunner.CanRun(job),
                    EditAsync,
                    MoveUpAsync,
                    MoveDownAsync,
                    RemoveAsync,
                    RunAsync,
                    Cancel));
        }

        for (var index = 0; index < QueuedJobs.Count; index++)
        {
            QueuedJobs[index].SetPosition(index, QueuedJobs.Count);
        }

        HasQueuedJobs = QueuedJobs.Count > 0;
        QueueCountLabel = $"{QueuedJobs.Count} export job{(QueuedJobs.Count == 1 ? string.Empty : "s")}";
    }

    private void RefreshCommands()
    {
        QueuePlanCommand.ChangeCanExecute();
        ClearQueueCommand.ChangeCanExecute();
    }

    private static string FormatOutputFormat(OutputFormat format) =>
        format switch
        {
            MediaForge.GenZ.Core.Models.OutputFormat.Mp4 => "MP4",
            MediaForge.GenZ.Core.Models.OutputFormat.WebM => "WebM",
            MediaForge.GenZ.Core.Models.OutputFormat.Mp3 => "MP3",
            MediaForge.GenZ.Core.Models.OutputFormat.M4A => "M4A",
            MediaForge.GenZ.Core.Models.OutputFormat.Jpeg => "JPEG",
            MediaForge.GenZ.Core.Models.OutputFormat.Png => "PNG",
            MediaForge.GenZ.Core.Models.OutputFormat.WebP => "WebP",
            _ => format.ToString()
        };

    private static string FormatQuality(ExportQuality quality) =>
        quality switch
        {
            ExportQuality.Compact => "Compact - smaller file",
            ExportQuality.Balanced => "Balanced",
            ExportQuality.High => "High quality",
            _ => quality.ToString()
        };

    private static string FormatAspectRatio(AspectRatioTarget aspectRatio) =>
        aspectRatio switch
        {
            AspectRatioTarget.Portrait9By16 => "9:16 portrait",
            AspectRatioTarget.Square1By1 => "1:1 square",
            AspectRatioTarget.Landscape16By9 => "16:9 landscape",
            _ => "Original"
        };
}
