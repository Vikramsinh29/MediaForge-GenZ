namespace MediaForge.GenZ.Core.Models;

public sealed record TrimOptions(TimeSpan? Start = null, TimeSpan? End = null)
{
    public bool Enabled => Start.HasValue || End.HasValue;
}
