using System.Collections.ObjectModel;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class ExportPlanningViewModel : BaseViewModel
{
    private readonly IExportPlanner _exportPlanner;
    private readonly IConversionJobQueue _conversionJobQueue;
    private ExportPlan? _currentPlan;
    private MediaAsset? _source;
    private bool _hasQueuedJobs;
    private bool _isPlanValid;
    private bool _isVisible;
    private string _aspectRatio = string.Empty;
    private string _outputFileName = string.Empty;
    private string _outputFormat = string.Empty;
    private string _presetDescription = string.Empty;
    private string _quality = string.Empty;
    private string _queueCountLabel = "0 planned exports";
    private string _queueMessage = string.Empty;
    private string _settingsSummary = string.Empty;
    private string _validationMessage = string.Empty;
    private ExportPreset? _selectedPreset;

    public ExportPlanningViewModel(
        IExportPlanner exportPlanner,
        IConversionJobQueue conversionJobQueue)
    {
        _exportPlanner = exportPlanner;
        _conversionJobQueue = conversionJobQueue;
        OpenCommand = new Command(Open);
        CloseCommand = new Command(Close);
        QueuePlanCommand = new Command(QueuePlan, () => IsPlanValid && _currentPlan is not null);
    }

    public ObservableCollection<ExportPreset> CompatiblePresets { get; } = [];

    public ObservableCollection<ConversionJob> QueuedJobs { get; } = [];

    public Command OpenCommand { get; }

    public Command CloseCommand { get; }

    public Command QueuePlanCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
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
        private set => SetProperty(ref _hasQueuedJobs, value);
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

    public void Prepare(MediaAsset source)
    {
        _source = source;
        IsVisible = false;
        CompatiblePresets.Clear();

        foreach (var preset in _exportPlanner.GetCompatiblePresets(source))
        {
            CompatiblePresets.Add(preset);
        }

        SelectedPreset = CompatiblePresets.FirstOrDefault();
        if (SelectedPreset is null)
        {
            ClearPlan("No compatible export presets are available.");
        }
    }

    public void Close()
    {
        IsVisible = false;
    }

    private void Open()
    {
        if (_source is not null)
        {
            IsVisible = true;
            BuildPlan();
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
        ValidationMessage = "Valid plan · your original file will never be overwritten.";
        IsPlanValid = !plan.OverwriteOriginal;
        QueuePlanCommand.ChangeCanExecute();
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
        QueuePlanCommand.ChangeCanExecute();
    }

    private void QueuePlan()
    {
        if (_currentPlan is null || !IsPlanValid)
        {
            return;
        }

        try
        {
            _conversionJobQueue.Enqueue(_currentPlan);
            RefreshQueue();
            QueueMessage = "Queued for architecture preview only. No media engine is installed.";
        }
        catch (ArgumentException exception)
        {
            QueueMessage = exception.Message;
        }
    }

    private void RefreshQueue()
    {
        QueuedJobs.Clear();
        foreach (var job in _conversionJobQueue.GetSnapshot())
        {
            QueuedJobs.Add(job);
        }

        HasQueuedJobs = QueuedJobs.Count > 0;
        QueueCountLabel = $"{QueuedJobs.Count} planned export{(QueuedJobs.Count == 1 ? string.Empty : "s")}";
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
            ExportQuality.Compact => "Compact · smaller file",
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
