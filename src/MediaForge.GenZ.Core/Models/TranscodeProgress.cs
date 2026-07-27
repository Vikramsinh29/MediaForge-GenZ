namespace MediaForge.GenZ.Core.Models;

public sealed record TranscodeProgress(
    double FractionComplete,
    TimeSpan? ProcessedDuration = null);
