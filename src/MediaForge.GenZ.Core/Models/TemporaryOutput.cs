namespace MediaForge.GenZ.Core.Models;

public sealed record TemporaryOutput(
    string Id,
    string SuggestedFileName,
    string? ContentType = null);
