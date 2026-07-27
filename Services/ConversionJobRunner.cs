using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class ConversionJobRunner(
    IConversionJobQueue queue,
    IMediaImportService mediaImportService,
    ITranscoder transcoder,
    IOutputStorage outputStorage) : IConversionJobRunner
{
    public bool CanRun(ConversionJob job) =>
        job.State == ConversionJobState.Queued &&
        job.SourceReferenceState == MediaSourceReferenceState.Available &&
        !job.Plan.OverwriteOriginal &&
        job.Plan.OutputFormat == OutputFormat.M4A &&
        Path.GetExtension(job.Plan.Source.DisplayName)
            .Equals(".wav", StringComparison.OrdinalIgnoreCase);

    public async Task<ConversionExecutionResult> RunAsync(
        string jobId,
        IProgress<ConversionJobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var job = queue.GetSnapshot().FirstOrDefault(candidate => candidate.Id == jobId);
        if (job is null)
        {
            return new ConversionExecutionResult(jobId, false, "The queued plan was not found.");
        }

        if (!CanRun(job))
        {
            return new ConversionExecutionResult(
                jobId,
                false,
                "Development conversion supports only accessible WAV sources planned as M4A.");
        }

        TemporaryOutput? temporary = null;
        try
        {
            var preparing = await queue.TransitionAsync(
                job.Id,
                ConversionJobState.Preparing,
                statusMessage: "Preparing private temporary files.",
                cancellationToken: cancellationToken);
            if (!preparing.IsValid)
            {
                return new ConversionExecutionResult(
                    jobId,
                    false,
                    string.Join(" ", preparing.Errors));
            }

            temporary = await outputStorage.CreateTemporaryAsync(job, cancellationToken);
            await using var source = await mediaImportService.OpenReadAsync(
                job.Plan.Source,
                cancellationToken);
            await using var temporaryOutput = await outputStorage.OpenTemporaryWriteAsync(
                temporary,
                cancellationToken);

            var processing = await queue.TransitionAsync(
                job.Id,
                ConversionJobState.Processing,
                statusMessage: "Converting WAV to AAC/M4A on this device.",
                cancellationToken: cancellationToken);
            if (!processing.IsValid)
            {
                throw new InvalidOperationException(string.Join(" ", processing.Errors));
            }

            var nativeResult = await transcoder.ProcessAsync(
                job,
                source,
                temporaryOutput,
                progress,
                cancellationToken);
            if (!nativeResult.Success)
            {
                throw new InvalidOperationException(
                    nativeResult.ErrorMessage ?? "The native conversion failed.");
            }

            await temporaryOutput.FlushAsync(cancellationToken);
            var output = await outputStorage.FinalizeAtomicallyAsync(
                temporary,
                job.Plan,
                cancellationToken);
            temporary = null;

            ValidationResult completion;
            try
            {
                completion = await queue.TransitionAsync(
                    job.Id,
                    ConversionJobState.Completed,
                    progress: 1,
                    statusMessage: $"Completed: {output.DisplayName}",
                    completedOutput: output,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                await outputStorage.DiscardFinalizedAsync(output, CancellationToken.None);
                throw;
            }

            if (!completion.IsValid)
            {
                await outputStorage.DiscardFinalizedAsync(output, CancellationToken.None);
                throw new InvalidOperationException(string.Join(" ", completion.Errors));
            }

            progress?.Report(
                new ConversionJobProgress(
                    job.Id,
                    ConversionJobState.Completed,
                    1,
                    Message: "Output validated and saved."));
            return new ConversionExecutionResult(job.Id, true, Output: output);
        }
        catch (OperationCanceledException)
        {
            if (temporary is not null)
            {
                await outputStorage.DiscardTemporaryAsync(temporary, CancellationToken.None);
            }

            await queue.CancelAsync(job.Id, CancellationToken.None);
            return new ConversionExecutionResult(job.Id, false, "Conversion cancelled.");
        }
        catch (Exception exception)
        {
            if (temporary is not null)
            {
                await outputStorage.DiscardTemporaryAsync(temporary, CancellationToken.None);
            }

            var current = queue.GetSnapshot().FirstOrDefault(candidate => candidate.Id == job.Id);
            if (current?.State is ConversionJobState.Preparing or ConversionJobState.Processing)
            {
                await queue.TransitionAsync(
                    job.Id,
                    ConversionJobState.Failed,
                    statusMessage: "Development conversion failed.",
                    errorMessage: exception.Message,
                    cancellationToken: CancellationToken.None);
            }

            return new ConversionExecutionResult(job.Id, false, exception.Message);
        }
    }
}
