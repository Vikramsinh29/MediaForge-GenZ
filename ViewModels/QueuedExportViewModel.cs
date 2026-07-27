using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed class QueuedExportViewModel : BaseViewModel
{
    private readonly Func<QueuedExportViewModel, Task> _edit;
    private readonly Func<QueuedExportViewModel, Task> _moveDown;
    private readonly Func<QueuedExportViewModel, Task> _moveUp;
    private readonly Func<QueuedExportViewModel, Task> _remove;
    private bool _canMoveDown;
    private bool _canMoveUp;
    private int _position;
    private string _positionLabel = "#0";

    public QueuedExportViewModel(
        ConversionJob job,
        Func<QueuedExportViewModel, Task> edit,
        Func<QueuedExportViewModel, Task> moveUp,
        Func<QueuedExportViewModel, Task> moveDown,
        Func<QueuedExportViewModel, Task> remove)
    {
        Job = job;
        _edit = edit;
        _moveUp = moveUp;
        _moveDown = moveDown;
        _remove = remove;
        EditCommand = new Command(async () => await _edit(this));
        MoveUpCommand = new Command(async () => await _moveUp(this), () => CanMoveUp);
        MoveDownCommand = new Command(async () => await _moveDown(this), () => CanMoveDown);
        RemoveCommand = new Command(async () => await _remove(this));
    }

    public ConversionJob Job { get; }

    public Command EditCommand { get; }

    public Command MoveUpCommand { get; }

    public Command MoveDownCommand { get; }

    public Command RemoveCommand { get; }

    public string Id => Job.Id;

    public string OutputFileName => Job.Plan.ProposedOutputFileName;

    public string SourceFileName => Job.Plan.Source.DisplayName;

    public string PresetName => Job.Plan.Preset.Name;

    public string StatusMessage => Job.StatusMessage ?? "Plan saved locally.";

    public string PositionLabel
    {
        get => _positionLabel;
        private set => SetProperty(ref _positionLabel, value);
    }

    public bool HasSourceIssue =>
        Job.SourceReferenceState != MediaSourceReferenceState.Available;

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
}
