using MediaForge.GenZ.Core.Models;

namespace MediaForge.GenZ.Core.Contracts;

public interface IConversionJobQueue
{
    IReadOnlyList<ConversionJob> GetSnapshot();

    ConversionJob Enqueue(ExportPlan plan);

    ValidationResult Transition(
        string jobId,
        ConversionJobState nextState,
        double? progress = null,
        string? statusMessage = null,
        string? errorMessage = null);

    ValidationResult Cancel(string jobId);
}
