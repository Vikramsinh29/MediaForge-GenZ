using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.ViewModels;

public sealed record OutputFormatOption(OutputFormat Value, string Label);

public sealed record ExportQualityOption(ExportQuality Value, string Label);

public sealed record AspectRatioOption(AspectRatioTarget Value, string Label);
