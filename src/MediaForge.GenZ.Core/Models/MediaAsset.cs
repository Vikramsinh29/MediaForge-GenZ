namespace MediaForge.GenZ.Core.Models;

public sealed record MediaAsset(
    string Id,
    string DisplayName,
    string? ContentType,
    long? SizeInBytes);
