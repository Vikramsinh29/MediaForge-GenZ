using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class QueuedExportViewModel : BaseViewModel
{
    private readonly Func<QueuedExportViewModel, Task> _edit;
    private readonly Func<QueuedExportViewModel, Task> _moveDown;
    private readonly Func<QueuedExportViewModel, Task> _moveUp;
    private readonly Func<QueuedExportViewModel, Task> _remove;
    private readonly Func<QueuedExportViewModel, Task> _run;
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
        Action<QueuedExportViewModel> cancel)
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
        EditCommand = new Command(async () => await _edit(this), () => !IsRunning);
        MoveUpCommand = new Command(async () => await _moveUp(this), () => CanMoveUp && !IsRunning);
        MoveDownCommand = new Command(async () => await _moveDown(this), () => CanMoveDown && !IsRunning);
        RemoveCommand = new Command(async () => await _remove(this), () => !IsRunning);
        RunCommand = new Command(async () => await _run(this), () => CanRun && !IsRunning);
        CancelCommand = new Command(() => _cancel(this), () => IsRunning);
    }

    public ConversionJob Job { get; }

    public Command EditCommand { get; }

    public Command MoveUpCommand { get; }

    public Command MoveDownCommand { get; }

    public Command RemoveCommand { get; }

    public Command RunCommand { get; }

    public Command CancelCommand { get; }

    public string Id => Job.Id;

    public string OutputFileName => Job.Plan.ProposedOutputFileName;

    public string SourceFileName => Job.Plan.Source.DisplayName;

    public string PresetName => Job.Plan.Preset.Name;

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

    private void RefreshCommands()
    {
        EditCommand.ChangeCanExecute();
        MoveUpCommand.ChangeCanExecute();
        MoveDownCommand.ChangeCanExecute();
        RemoveCommand.ChangeCanExecute();
        RunCommand.ChangeCanExecute();
        CancelCommand.ChangeCanExecute();
    }
}
