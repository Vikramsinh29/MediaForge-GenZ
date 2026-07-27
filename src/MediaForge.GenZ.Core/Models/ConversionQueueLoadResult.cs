namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionQueueLoadResult(
    ConversionQueueDocument? Document,
    string? Message = null)
{
    public static ConversionQueueLoadResult Empty { get; } =
        new(new ConversionQueueDocument(ConversionQueueDocument.CurrentVersion, []));
}
