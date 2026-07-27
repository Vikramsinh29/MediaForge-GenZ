namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionExecutionResult(
    string JobId,
    bool Success,
    string? ErrorMessage = null);
