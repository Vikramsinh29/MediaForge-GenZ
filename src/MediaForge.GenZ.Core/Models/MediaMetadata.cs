namespace MediaForge.GenZ.Core.Models;

public sealed record MediaMetadata(
    TimeSpan? Duration,
    int? Width,
    int? Height,
    long? Bitrate,
    string? Codec);
