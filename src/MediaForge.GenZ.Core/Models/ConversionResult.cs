namespace MediaForge.GenZ.Core.Models;

public sealed record ConversionResult(
    bool Success,
    MediaAsset? Output,
    string? ErrorMessage = null);
