using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class QueuedExportViewModel : BaseViewModel
{
    private readonly Func<QueuedExportViewModel, Task> _edit;
    private readonly Func<QueuedExportViewModel, Task> _moveDown;
    private readonly Func<QueuedExportViewModel, Task> _moveUp;
    private readonly Func<QueuedExportViewModel, Task> _remove;
    private readonly Func<QueuedExportViewModel, Task> _run;
    private readonly Func<QueuedExportViewModel, Task> _openOutput;
    private readonly Func<QueuedExportViewModel, Task> _shareOutput;
    private readonly Action<QueuedExportViewModel> _cancel;
    private bool _canMoveDown;
    private bool _canMoveUp;
    private bool _isRunning;
    private double _progress;
    private int _position;
    private string _positionLabel = "#0";
    private string _runtimeMessage = string.Empty;
    private string _stateLabel;

    public QueuedExportViewModel(
        ConversionJob job,
        bool canRun,
        Func<QueuedExportViewModel, Task> edit,
        Func<QueuedExportViewModel, Task> moveUp,
        Func<QueuedExportViewModel, Task> moveDown,
        Func<QueuedExportViewModel, Task> remove,
        Func<QueuedExportViewModel, Task> run,
        Action<QueuedExportViewModel> cancel,
        Func<QueuedExportViewModel, Task> openOutput,
        Func<QueuedExportViewModel, Task> shareOutput)
    {
        Job = job;
        CanRun = canRun;
        _stateLabel = job.State.ToString();
        _edit = edit;
        _moveUp = moveUp;
        _moveDown = moveDown;
        _remove = remove;
        _run = run;
        _cancel = cancel;
        _openOutput = openOutput;
        _shareOutput = shareOutput;
        EditCommand = new Command(async () => await _edit(this), () => !IsRunning);
        MoveUpCommand = new Command(async () => await _moveUp(this), () => CanMoveUp && !IsRunning);
        MoveDownCommand = new Command(async () => await _moveDown(this), () => CanMoveDown && !IsRunning);
        RemoveCommand = new Command(async () => await _remove(this), () => !IsRunning);
        RunCommand = new Command(async () => await _run(this), () => CanRun && !IsRunning);
        CancelCommand = new Command(() => _cancel(this), () => IsRunning);
        OpenOutputCommand = new Command(
            async () => await _openOutput(this),
            () => HasCompletedOutput && !IsRunning);
        ShareOutputCommand = new Command(
            async () => await _shareOutput(this),
            () => HasCompletedOutput && !IsRunning);
    }

    public ConversionJob Job { get; }

    public Command EditCommand { get; }

    public Command MoveUpCommand { get; }

    public Command MoveDownCommand { get; }

    public Command RemoveCommand { get; }

    public Command RunCommand { get; }

    public Command CancelCommand { get; }
    public Command OpenOutputCommand { get; }
    public Command ShareOutputCommand { get; }

    public string Id => Job.Id;

    public string OutputFileName => Job.Plan.ProposedOutputFileName;

    public string SourceFileName => Job.Plan.Source.DisplayName;

    public string PresetName => Job.Plan.Preset.Name;

    public string ConversionRouteLabel =>
        $"{FormatSource(Job.Plan.Source.DisplayName)} → {FormatOutput(Job.Plan.OutputFormat)}";

    public string SettingsLabel =>
        $"{FormatQuality(Job.Plan.Quality)} · {FormatAspectRatio(Job.Plan.AspectRatio)}";

    public string CompatibilityLabel =>
        CanRun
            ? "Development WAV → M4A conversion is available."
            : $"Plan compatible with {FormatMediaType(Job.Plan.Source)} media · conversion is not available yet.";

    public bool ShowRunAction => CanRun;
    public bool IsCompleted => Job.State == ConversionJobState.Completed;
    public bool IsQueued => Job.State == ConversionJobState.Queued;
    public bool IsTerminal =>
        Job.State is ConversionJobState.Completed or
            ConversionJobState.Failed or ConversionJobState.Cancelled;
    public bool HasCompletedOutput => IsCompleted && Job.Output is not null;
    public bool IsOutputUnavailable => IsCompleted && Job.Output is null;
    public bool ShowRemoveOnly =>
        Job.State is ConversionJobState.Failed or ConversionJobState.Cancelled;
    public bool ShowStatusMessage => HasSourceIssue || IsTerminal;

    public string StatusMessage =>
        string.IsNullOrWhiteSpace(RuntimeMessage)
            ? Job.StatusMessage ?? "Plan saved locally."
            : RuntimeMessage;

    public string PositionLabel
    {
        get => _positionLabel;
        private set => SetProperty(ref _positionLabel, value);
    }

    public bool CanRun { get; }

    public bool HasSourceIssue =>
        Job.SourceReferenceState != MediaSourceReferenceState.Available;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool ShowProgress
    {
        get => _showProgress;
        private set => SetProperty(ref _showProgress, value);
    }

    public double Progress
    {
        get => _progress;
        private set
        {
            if (SetProperty(ref _progress, value))
            {
                ShowProgress = IsRunning || value > 0;
            }
        }
    }

    private bool _showProgress;

    public string RuntimeMessage
    {
        get => _runtimeMessage;
        private set
        {
            if (SetProperty(ref _runtimeMessage, value))
            {
                SetProperty(ref _statusMessageVersion, _statusMessageVersion + 1, nameof(StatusMessage));
            }
        }
    }

    private int _statusMessageVersion;

    public string StateLabel
    {
        get => _stateLabel;
        private set => SetProperty(ref _stateLabel, value);
    }

    public int Position
    {
        get => _position;
        private set
        {
            if (SetProperty(ref _position, value))
            {
                PositionLabel = $"#{value}";
            }
        }
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        private set
        {
            if (SetProperty(ref _canMoveUp, value))
            {
                MoveUpCommand.ChangeCanExecute();
            }
        }
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        private set
        {
            if (SetProperty(ref _canMoveDown, value))
            {
                MoveDownCommand.ChangeCanExecute();
            }
        }
    }

    public void SetPosition(int index, int count)
    {
        Position = index + 1;
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;
    }

    public void SetRunning()
    {
        IsRunning = true;
        StateLabel = "Processing";
        RuntimeMessage = "Preparing development conversion.";
        ShowProgress = true;
    }

    public void Report(ConversionJobProgress progress)
    {
        Progress = Math.Clamp(progress.FractionComplete, 0, 1);
        StateLabel = progress.State.ToString();
        RuntimeMessage = progress.Message ?? StateLabel;
    }

    public void Finish(string message, bool success)
    {
        IsRunning = false;
        StateLabel = success ? "Completed" : "Failed";
        RuntimeMessage = message;
        if (success)
        {
            Progress = 1;
        }
    }

    public void SetActionMessage(string message) => RuntimeMessage = message;

    private void RefreshCommands()
    {
        EditCommand.ChangeCanExecute();
        MoveUpCommand.ChangeCanExecute();
        MoveDownCommand.ChangeCanExecute();
        RemoveCommand.ChangeCanExecute();
        RunCommand.ChangeCanExecute();
        CancelCommand.ChangeCanExecute();
        OpenOutputCommand.ChangeCanExecute();
        ShareOutputCommand.ChangeCanExecute();
    }

    private static string FormatOutput(OutputFormat format) => format switch
    {
        OutputFormat.Mp4 => "MP4",
        OutputFormat.WebM => "WebM",
        OutputFormat.Mp3 => "MP3",
        OutputFormat.M4A => "M4A",
        OutputFormat.Jpeg => "JPEG",
        OutputFormat.Png => "PNG",
        OutputFormat.WebP => "WebP",
        _ => format.ToString()
    };

    private static string FormatSource(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension)
            ? "MEDIA"
            : extension.ToUpperInvariant();
    }

    private static string FormatMediaType(MediaAsset source)
    {
        if (source.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) is true) return "audio";
        if (source.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true) return "image";
        if (source.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) is true) return "video";
        return "selected";
    }

    private static string FormatQuality(ExportQuality quality) => quality switch
    {
        ExportQuality.Compact => "Compact",
        ExportQuality.Balanced => "Balanced",
        ExportQuality.High => "High",
        _ => quality.ToString()
    };

    private static string FormatAspectRatio(AspectRatioTarget aspectRatio) => aspectRatio switch
    {
        AspectRatioTarget.Portrait9By16 => "9:16",
        AspectRatioTarget.Square1By1 => "1:1",
        AspectRatioTarget.Landscape16By9 => "16:9",
        _ => "Original"
    };
}
