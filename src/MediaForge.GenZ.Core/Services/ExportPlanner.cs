using System.Text;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Services;

public sealed class ExportPlanner : IExportPlanner
{
    private static readonly IReadOnlyList<ExportPreset> Presets =
    [
        new(
            "instagram-reel",
            "Instagram Reel",
            "Vertical, high-quality video ready for Reels.",
            MediaCategory.Video,
            OutputFormat.Mp4,
            ExportQuality.High,
            AspectRatioTarget.Portrait9By16,
            "instagram-reel",
            1080,
            1920),
        new(
            "youtube-short",
            "YouTube Short",
            "A crisp vertical video plan for YouTube Shorts.",
            MediaCategory.Video,
            OutputFormat.Mp4,
            ExportQuality.High,
            AspectRatioTarget.Portrait9By16,
            "youtube-short",
            1080,
            1920),
        new(
            "tiktok",
            "TikTok",
            "Balanced vertical video for quick creator uploads.",
            MediaCategory.Video,
            OutputFormat.Mp4,
            ExportQuality.Balanced,
            AspectRatioTarget.Portrait9By16,
            "tiktok",
            1080,
            1920),
        new(
            "whatsapp-video",
            "WhatsApp share",
            "Compact video that is easier to share in chats.",
            MediaCategory.Video,
            OutputFormat.Mp4,
            ExportQuality.Compact,
            AspectRatioTarget.Original,
            "whatsapp",
            1280,
            720),
        new(
            "whatsapp-audio",
            "WhatsApp share",
            "Compact audio for fast, friendly sharing.",
            MediaCategory.Audio,
            OutputFormat.M4A,
            ExportQuality.Compact,
            AspectRatioTarget.Original,
            "whatsapp"),
        new(
            "whatsapp-image",
            "WhatsApp share",
            "A smaller image while keeping it clear on phones.",
            MediaCategory.Image,
            OutputFormat.Jpeg,
            ExportQuality.Compact,
            AspectRatioTarget.Original,
            "whatsapp",
            1600,
            1600),
        new(
            "audio-extraction",
            "Audio extraction",
            "Plan an audio-only MP3 from the selected video.",
            MediaCategory.Video,
            OutputFormat.Mp3,
            ExportQuality.High,
            AspectRatioTarget.Original,
            "audio",
            ExtractAudioOnly: true),
        new(
            "image-compression",
            "Image compression",
            "Reduce image size with a modern WebP output.",
            MediaCategory.Image,
            OutputFormat.WebP,
            ExportQuality.Balanced,
            AspectRatioTarget.Original,
            "compressed"),
        new(
            "custom-video",
            "Custom export",
            "Keep the original shape with balanced video settings.",
            MediaCategory.Video,
            OutputFormat.Mp4,
            ExportQuality.Balanced,
            AspectRatioTarget.Original,
            "custom"),
        new(
            "custom-audio",
            "Custom export",
            "Keep the full audio with balanced M4A settings.",
            MediaCategory.Audio,
            OutputFormat.M4A,
            ExportQuality.Balanced,
            AspectRatioTarget.Original,
            "custom"),
        new(
            "custom-image",
            "Custom export",
            "Keep the original shape with a high-quality PNG.",
            MediaCategory.Image,
            OutputFormat.Png,
            ExportQuality.High,
            AspectRatioTarget.Original,
            "custom")
    ];

    public IReadOnlyList<ExportPreset> GetCompatiblePresets(MediaAsset source)
    {
        var category = Classify(source);
        return Presets.Where(preset => preset.CompatibleMedia == category).ToArray();
    }

    public ValidationResult Validate(MediaAsset source, ExportPreset preset)
    {
        var errors = new List<string>();
        var category = Classify(source);

        if (string.IsNullOrWhiteSpace(source.DisplayName))
        {
            errors.Add("The source file needs a valid name.");
        }

        if (preset.CompatibleMedia != category)
        {
            errors.Add($"{preset.Name} is not compatible with this {category.ToString().ToLowerInvariant()}.");
        }

        if (!IsFormatCompatible(category, preset.OutputFormat))
        {
            errors.Add($"{preset.OutputFormat} is not a supported output for this media type.");
        }

        if (preset.ExtractAudioOnly && category != MediaCategory.Video)
        {
            errors.Add("Audio extraction requires a video source.");
        }

        if (preset.TargetWidth.HasValue != preset.TargetHeight.HasValue ||
            preset.TargetWidth is <= 0 ||
            preset.TargetHeight is <= 0)
        {
            errors.Add("Target dimensions must be positive when provided.");
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(false, errors);
    }

    public ExportPlan CreatePlan(MediaAsset source, ExportPreset preset)
    {
        var validation = Validate(source, preset);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join(" ", validation.Errors),
                nameof(preset));
        }

        var outputFileName = BuildOutputFileName(source.DisplayName, preset);
        return new ExportPlan(
            source,
            preset,
            preset.OutputFormat,
            preset.Quality,
            preset.AspectRatio,
            preset.TargetWidth,
            preset.TargetHeight,
            preset.ExtractAudioOnly,
            outputFileName,
            BuildSettingsSummary(preset),
            OverwriteOriginal: false);
    }

    private static MediaCategory Classify(MediaAsset source)
    {
        if (source.ContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return MediaCategory.Audio;
        }

        if (source.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return MediaCategory.Image;
        }

        if (source.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) is true)
        {
            return MediaCategory.Video;
        }

        var extension = Path.GetExtension(source.DisplayName).ToLowerInvariant();
        return extension switch
        {
            ".mp3" or ".m4a" or ".aac" or ".wav" or ".flac" or ".ogg" or ".opus" or ".aiff" =>
                MediaCategory.Audio,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".heic" or ".heif" or ".bmp" or ".tif" or ".tiff" =>
                MediaCategory.Image,
            _ => MediaCategory.Video
        };
    }

    private static bool IsFormatCompatible(MediaCategory category, OutputFormat format) =>
        category switch
        {
            MediaCategory.Video => format is OutputFormat.Mp4 or OutputFormat.WebM or
                OutputFormat.Mp3 or OutputFormat.M4A,
            MediaCategory.Audio => format is OutputFormat.Mp3 or OutputFormat.M4A,
            MediaCategory.Image => format is OutputFormat.Jpeg or OutputFormat.Png or OutputFormat.WebP,
            _ => false
        };

    private static string BuildOutputFileName(string sourceFileName, ExportPreset preset)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var safeBaseName = SanitizeFileName(baseName);
        var safeSuffix = SanitizeFileName(preset.FileNameSuffix);
        return $"{safeBaseName}-{safeSuffix}.{GetExtension(preset.OutputFormat)}";
    }

    private static string SanitizeFileName(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (character < ' ' || "<>:\"/\\|?*".Contains(character))
            {
                output.Append('-');
            }
            else
            {
                output.Append(character);
            }
        }

        var sanitized = output.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? "media" : sanitized;
    }

    private static string GetExtension(OutputFormat format) =>
        format switch
        {
            OutputFormat.Mp4 => "mp4",
            OutputFormat.WebM => "webm",
            OutputFormat.Mp3 => "mp3",
            OutputFormat.M4A => "m4a",
            OutputFormat.Jpeg => "jpg",
            OutputFormat.Png => "png",
            OutputFormat.WebP => "webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    private static string BuildSettingsSummary(ExportPreset preset)
    {
        if (preset.ExtractAudioOnly)
        {
            return "Audio only · no video track";
        }

        var ratio = preset.AspectRatio switch
        {
            AspectRatioTarget.Portrait9By16 => "9:16 portrait",
            AspectRatioTarget.Square1By1 => "1:1 square",
            AspectRatioTarget.Landscape16By9 => "16:9 landscape",
            _ => "Original aspect ratio"
        };

        return preset.TargetWidth is > 0 && preset.TargetHeight is > 0
            ? $"{ratio} · up to {preset.TargetWidth} × {preset.TargetHeight}"
            : ratio;
    }
}
