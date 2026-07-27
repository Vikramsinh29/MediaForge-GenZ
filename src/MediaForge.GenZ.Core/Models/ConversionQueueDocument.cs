namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionQueueDocument(
    int Version,
    IReadOnlyList<ConversionJob> Jobs)
{
    public const int CurrentVersion = 1;
}
