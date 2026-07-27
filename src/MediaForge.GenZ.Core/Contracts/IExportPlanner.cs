using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IExportPlanner
{
    IReadOnlyList<ExportPreset> GetCompatiblePresets(MediaAsset source);

    ValidationResult Validate(MediaAsset source, ExportPreset preset);

    ExportPlan CreatePlan(MediaAsset source, ExportPreset preset);
}
