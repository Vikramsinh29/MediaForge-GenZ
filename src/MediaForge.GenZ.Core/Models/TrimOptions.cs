namespace MediaForge.Core.Models;

public sealed class TrimOptions
{
    public TimeSpan? Start { get; set; }

    public TimeSpan? End { get; set; }

    public bool Enabled =>
        Start.HasValue || End.HasValue;
}