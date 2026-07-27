namespace MediaForge.GenZ.Core.Models;

public sealed record ExportPreset(
    string Id,
    string Name,
    string Description,
    MediaCategory CompatibleMedia,
    OutputFormat OutputFormat,
    ExportQuality Quality,
    AspectRatioTarget AspectRatio,
    string FileNameSuffix,
    int? TargetWidth = null,
    int? TargetHeight = null,
    bool ExtractAudioOnly = false);
