using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IExportPlanner
{
    IReadOnlyList<ExportPreset> GetCompatiblePresets(MediaAsset source);

    IReadOnlyList<OutputFormat> GetCompatibleOutputFormats(MediaAsset source);

    IReadOnlyList<AspectRatioTarget> GetCompatibleAspectRatios(MediaAsset source);

    ValidationResult Validate(MediaAsset source, ExportPreset preset);

    ValidationResult Validate(MediaAsset source, ExportPreset preset, ExportSettings settings);

    ExportPlan CreatePlan(MediaAsset source, ExportPreset preset);

    ExportPlan CreatePlan(MediaAsset source, ExportPreset preset, ExportSettings settings);
}
