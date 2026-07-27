namespace MediaForge.GenZ.Core.Models;

public sealed record ExportSettings(
    OutputFormat OutputFormat,
    ExportQuality Quality,
    AspectRatioTarget AspectRatio);
