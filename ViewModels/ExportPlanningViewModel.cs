using System.Collections.ObjectModel;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class ExportPlanningViewModel : BaseViewModel
{
    private readonly IExportPlanner _exportPlanner;
    private readonly IConversionJobQueue _conversionJobQueue;
    private readonly IConversionJobRunner _conversionJobRunner;
    private readonly IOutputOpener _outputOpener;
    private readonly IShareService _shareService;
    private readonly Dictionary<string, CancellationTokenSource> _runningJobs = new();
    private readonly Dictionary<string, ExportDraft> _drafts = new(StringComparer.Ordinal);
    private readonly List<MediaAsset> _batchSources = [];
    private int _batchIndex;
    private bool _loadingDraft;
    private ExportPlan? _currentPlan;
    private MediaAsset? _source;
    private string? _editingJobId;
    private bool _hasQueuedJobs;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isPlanEditorVisible;
    private bool _isPlanValid;
    private bool _isQueueEmpty = true;
    private bool _applySettingsToCompatibleFiles;
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
    private OutputFormatOption? _selectedOutputFormat;
    private ExportQualityOption? _selectedQuality;
    private AspectRatioOption? _selectedAspectRatio;

    public ExportPlanningViewModel(
        IExportPlanner exportPlanner,
        IConversionJobQueue conversionJobQueue,
        IConversionJobRunner conversionJobRunner,
        IOutputOpener outputOpener,
        IShareService shareService)
    {
        _exportPlanner = exportPlanner;
        _conversionJobQueue = conversionJobQueue;
        _conversionJobRunner = conversionJobRunner;
        _outputOpener = outputOpener;
        _shareService = shareService;
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
        PreviousSourceCommand = new Command(PreviousSource, () => _batchIndex > 0 && !IsBusy);
        NextSourceCommand = new Command(NextSource, () => _batchIndex < _batchSources.Count - 1 && !IsBusy);
    }

    public ObservableCollection<ExportPreset> CompatiblePresets { get; } = [];

    public ObservableCollection<QueuedExportViewModel> QueuedJobs { get; } = [];
    public ObservableCollection<OutputFormatOption> OutputFormats { get; } = [];
    public ObservableCollection<ExportQualityOption> QualityOptions { get; } =
    [
        new(ExportQuality.Compact, "Compact · smaller file"),
        new(ExportQuality.Balanced, "Balanced"),
        new(ExportQuality.High, "High quality")
    ];
    public ObservableCollection<AspectRatioOption> AspectRatios { get; } = [];

    public Command OpenCommand { get; }

    public Command OpenQueueCommand { get; }

    public Command CloseCommand { get; }

    public Command CancelEditCommand { get; }

    public Command QueuePlanCommand { get; }

    public Command ClearQueueCommand { get; }
    public Command PreviousSourceCommand { get; }
    public Command NextSourceCommand { get; }
    public bool HasMultipleSources => _batchSources.Count > 1;
    public bool IsQueueSectionVisible => !IsPlanEditorVisible;
    public bool HasRunnableJobs => QueuedJobs.Any(job => job.CanRun);
    public string ProcessingGuidance => HasRunnableJobs
        ? "WAV → M4A test jobs can be converted individually. Other plans remain review-only."
        : "Nothing here can run yet. For the current proof of concept, edit a WAV plan and choose M4A.";
    public string SourcePositionLabel => _batchSources.Count == 0 ? string.Empty : $"File {_batchIndex + 1} of {_batchSources.Count}";
    public string CurrentSourceName => _source?.DisplayName ?? string.Empty;
    public string DraftProgressLabel =>
        _batchSources.Count <= 1
            ? string.Empty
            : $"{_drafts.Values.Count(draft => draft.IsConfigured)} of {_batchSources.Count} files configured";

    public bool ApplySettingsToCompatibleFiles
    {
        get => _applySettingsToCompatibleFiles;
        set
        {
            if (SetProperty(ref _applySettingsToCompatibleFiles, value) && value)
            {
                ApplyCurrentSettingsToCompatible();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public bool IsPlanEditorVisible
    {
        get => _isPlanEditorVisible;
        private set
        {
            if (SetProperty(ref _isPlanEditorVisible, value))
            {
                SetProperty(ref _queueSectionVersion, _queueSectionVersion + 1, nameof(IsQueueSectionVisible));
            }
        }
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
                if (!_loadingDraft)
                {
                    LoadSettingOptions(value);
                    BuildPlan();
                }
            }
        }
    }

    public OutputFormatOption? SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set { if (SetProperty(ref _selectedOutputFormat, value) && !_loadingDraft) BuildPlan(); }
    }

    public ExportQualityOption? SelectedQuality
    {
        get => _selectedQuality;
        set { if (SetProperty(ref _selectedQuality, value) && !_loadingDraft) BuildPlan(); }
    }

    public AspectRatioOption? SelectedAspectRatio
    {
        get => _selectedAspectRatio;
        set { if (SetProperty(ref _selectedAspectRatio, value) && !_loadingDraft) BuildPlan(); }
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
        => PrepareBatch([source], source.Id);

    public void PrepareBatch(IReadOnlyList<MediaAsset> sources, string initialSourceId)
    {
        _batchSources.Clear();
        _batchSources.AddRange(sources.DistinctBy(asset => asset.Id));
        _batchIndex = Math.Max(0, _batchSources.FindIndex(asset => asset.Id == initialSourceId));
        _drafts.Clear();
        ApplySettingsToCompatibleFiles = false;
        _editingJobId = null;
        IsVisible = false;
        IsPlanEditorVisible = true;
        QueueActionLabel = _batchSources.Count > 1 ? "Save settings & next" : "Add plan to queue";
        LoadCurrentSource();
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
        if (_source is null || SelectedPreset is null ||
            SelectedOutputFormat is null || SelectedQuality is null || SelectedAspectRatio is null)
        {
            ClearPlan("Choose a preset to preview an export plan.");
            return;
        }

        var settings = new ExportSettings(
            SelectedOutputFormat.Value,
            SelectedQuality.Value,
            SelectedAspectRatio.Value);
        var validation = _exportPlanner.Validate(_source, SelectedPreset, settings);
        if (!validation.IsValid)
        {
            ClearPlan(string.Join(" ", validation.Errors));
            return;
        }

        var plan = _exportPlanner.CreatePlan(_source, SelectedPreset, settings);
        _currentPlan = plan;
        _drafts[_source.Id] = new ExportDraft(
            SelectedPreset.Id,
            settings.OutputFormat,
            settings.Quality,
            settings.AspectRatio,
            _drafts.GetValueOrDefault(_source.Id)?.IsConfigured ?? false);
        if (ApplySettingsToCompatibleFiles && !_loadingDraft)
        {
            ApplyCurrentSettingsToCompatible();
        }
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

        var wasEditing = _editingJobId is not null;
        if (!wasEditing && _batchSources.Count > 1)
        {
            _drafts[_source!.Id] = _drafts[_source.Id] with { IsConfigured = true };
            SetProperty(ref _draftProgressVersion, _draftProgressVersion + 1, nameof(DraftProgressLabel));
            if (_batchIndex < _batchSources.Count - 1)
            {
                _batchIndex++;
                LoadCurrentSource();
                return;
            }

            if (_drafts.Values.Count(draft => draft.IsConfigured) < _batchSources.Count)
            {
                QueueMessage = "Please review each file before adding the batch.";
                var firstPending = _batchSources.FindIndex(source =>
                    !_drafts.GetValueOrDefault(source.Id)?.IsConfigured ?? true);
                _batchIndex = Math.Max(0, firstPending);
                LoadCurrentSource();
                return;
            }
        }

        IsBusy = true;
        try
        {
            if (_editingJobId is null)
            {
                var previousCount = _conversionJobQueue.GetSnapshot().Count;
                if (_batchSources.Count > 1)
                {
                    foreach (var source in _batchSources)
                    {
                        var draft = _drafts[source.Id];
                        var preset = _exportPlanner.GetCompatiblePresets(source)
                            .First(item => item.Id == draft.PresetId);
                        var plan = _exportPlanner.CreatePlan(
                            source,
                            preset,
                            new ExportSettings(draft.Format, draft.Quality, draft.AspectRatio));
                        await _conversionJobQueue.EnqueueAsync(plan);
                    }
                }
                else
                {
                    await _conversionJobQueue.EnqueueAsync(_currentPlan);
                }

                var added = _conversionJobQueue.GetSnapshot().Count - previousCount;
                QueueMessage = added == 0
                    ? "Those files already have identical queued plans."
                    : $"{added} export plan{(added == 1 ? string.Empty : "s")} added. No media was copied.";
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

    private async Task OpenOutputAsync(QueuedExportViewModel item)
    {
        if (item.Job.Output is null)
        {
            item.SetActionMessage("The completed output is unavailable.");
            return;
        }

        try
        {
            var result = await _outputOpener.OpenAsync(item.Job.Output);
            item.SetActionMessage(result.IsValid
                ? "Opening the completed output."
                : string.Join(" ", result.Errors));
        }
        catch (OperationCanceledException)
        {
            item.SetActionMessage("Opening the output was cancelled.");
        }
        catch
        {
            item.SetActionMessage("The completed output could not be opened.");
        }
    }

    private async Task ShareOutputAsync(QueuedExportViewModel item)
    {
        if (item.Job.Output is null)
        {
            item.SetActionMessage("The completed output is unavailable.");
            return;
        }

        try
        {
            var result = await _shareService.ShareAsync(item.Job.Output);
            item.SetActionMessage(result.IsValid
                ? "Android's share sheet was opened."
                : string.Join(" ", result.Errors));
        }
        catch (OperationCanceledException)
        {
            item.SetActionMessage("Sharing was cancelled.");
        }
        catch
        {
            item.SetActionMessage("The completed output could not be shared.");
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
                    Cancel,
                    OpenOutputAsync,
                    ShareOutputAsync));
        }

        for (var index = 0; index < QueuedJobs.Count; index++)
        {
            QueuedJobs[index].SetPosition(index, QueuedJobs.Count);
        }

        HasQueuedJobs = QueuedJobs.Count > 0;
        QueueCountLabel = $"{QueuedJobs.Count} export job{(QueuedJobs.Count == 1 ? string.Empty : "s")}";
        SetProperty(ref _processingGuidanceVersion, _processingGuidanceVersion + 1, nameof(HasRunnableJobs));
        SetProperty(ref _processingGuidanceVersion, _processingGuidanceVersion + 1, nameof(ProcessingGuidance));
    }

    private void RefreshCommands()
    {
        QueuePlanCommand.ChangeCanExecute();
        ClearQueueCommand.ChangeCanExecute();
        PreviousSourceCommand.ChangeCanExecute();
        NextSourceCommand.ChangeCanExecute();
    }

    private void LoadCurrentSource()
    {
        if (_batchSources.Count == 0) return;
        _source = _batchSources[_batchIndex];
        _loadingDraft = true;
        LoadPresets(_source, _drafts.GetValueOrDefault(_source.Id)?.PresetId);
        var draft = _drafts.GetValueOrDefault(_source.Id);
        LoadSettingOptions(SelectedPreset, draft);
        _loadingDraft = false;
        BuildPlan();
        SetProperty(ref _sourcePositionVersion, _sourcePositionVersion + 1, nameof(SourcePositionLabel));
        SetProperty(ref _sourceNameVersion, _sourceNameVersion + 1, nameof(CurrentSourceName));
        SetProperty(ref _multipleSourcesVersion, _multipleSourcesVersion + 1, nameof(HasMultipleSources));
        QueueActionLabel = _batchIndex < _batchSources.Count - 1
            ? "Save settings & next"
            : (_batchSources.Count > 1 ? "Add all plans to queue" : "Add plan to queue");
        SetProperty(ref _draftProgressVersion, _draftProgressVersion + 1, nameof(DraftProgressLabel));
        RefreshCommands();
    }

    private int _sourcePositionVersion;
    private int _sourceNameVersion;
    private int _multipleSourcesVersion;
    private int _queueSectionVersion;
    private int _draftProgressVersion;
    private int _processingGuidanceVersion;

    private void LoadSettingOptions(ExportPreset? preset, ExportDraft? draft = null)
    {
        if (_source is null || preset is null) return;
        OutputFormats.Clear();
        foreach (var value in _exportPlanner.GetCompatibleOutputFormats(_source))
            OutputFormats.Add(new(value, FormatOutputFormat(value)));
        AspectRatios.Clear();
        foreach (var value in _exportPlanner.GetCompatibleAspectRatios(_source))
            AspectRatios.Add(new(value, FormatAspectRatio(value)));
        SelectedOutputFormat = OutputFormats.FirstOrDefault(x => x.Value == (draft?.Format ?? preset.OutputFormat));
        SelectedQuality = QualityOptions.FirstOrDefault(x => x.Value == (draft?.Quality ?? preset.Quality));
        SelectedAspectRatio = AspectRatios.FirstOrDefault(x => x.Value == (draft?.AspectRatio ?? preset.AspectRatio));
    }

    private void PreviousSource() { if (_batchIndex > 0) { _batchIndex--; LoadCurrentSource(); } }
    private void NextSource() { if (_batchIndex < _batchSources.Count - 1) { _batchIndex++; LoadCurrentSource(); } }

    private void ApplyCurrentSettingsToCompatible()
    {
        if (_currentPlan is null || SelectedPreset is null) return;
        var compatible = 0;
        foreach (var source in _batchSources)
        {
            var settings = new ExportSettings(_currentPlan.OutputFormat, _currentPlan.Quality, _currentPlan.AspectRatio);
            if (_exportPlanner.Validate(source, SelectedPreset, settings).IsValid)
            {
                var wasConfigured = _drafts.GetValueOrDefault(source.Id)?.IsConfigured ?? false;
                _drafts[source.Id] = new(
                    SelectedPreset.Id,
                    settings.OutputFormat,
                    settings.Quality,
                    settings.AspectRatio,
                    wasConfigured);
                compatible++;
            }
        }
        QueueMessage = $"Settings linked to {compatible} compatible file{(compatible == 1 ? string.Empty : "s")}. Changes now apply automatically.";
        SetProperty(ref _draftProgressVersion, _draftProgressVersion + 1, nameof(DraftProgressLabel));
    }

    private sealed record ExportDraft(
        string PresetId,
        OutputFormat Format,
        ExportQuality Quality,
        AspectRatioTarget AspectRatio,
        bool IsConfigured = false);

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
