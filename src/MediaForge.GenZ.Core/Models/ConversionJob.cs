namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionJob(
    string Id,
    ExportPlan Plan,
    ConversionJobState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    double Progress = 0,
    string? StatusMessage = null,
    string? ErrorMessage = null,
    MediaSourceReferenceState SourceReferenceState = MediaSourceReferenceState.Available,
    MediaAsset? Output = null);
