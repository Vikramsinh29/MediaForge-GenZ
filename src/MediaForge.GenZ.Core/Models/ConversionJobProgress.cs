namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionJobProgress(
    string JobId,
    ConversionJobState State,
    double FractionComplete,
    TimeSpan? ProcessedDuration = null,
    string? Message = null);
