namespace MediaForge.GenZ.Core.Models;

public sealed record ExportPlan(
    MediaAsset Source,
    ExportPreset Preset,
    OutputFormat OutputFormat,
    ExportQuality Quality,
    AspectRatioTarget AspectRatio,
    int? TargetWidth,
    int? TargetHeight,
    bool ExtractAudioOnly,
    string ProposedOutputFileName,
    string SettingsSummary,
    bool OverwriteOriginal = false);
