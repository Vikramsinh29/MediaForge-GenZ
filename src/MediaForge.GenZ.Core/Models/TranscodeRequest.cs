namespace MediaForge.GenZ.Core.Models;

public sealed record TranscodeRequest(
    MediaAsset Source,
    string OutputFormat,
    VideoCompressionPreset CompressionPreset,
    TrimOptions? Trim = null);
