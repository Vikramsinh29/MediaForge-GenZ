using System.Text.Json;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Services;

public sealed class AppDataConversionQueueStore : IConversionQueueStore
{
    private const string FileName = "conversion-queue.v1.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ConversionQueueLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var path = GetQueuePath();
        if (!File.Exists(path))
        {
            return ConversionQueueLoadResult.Empty;
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<ConversionQueueDocument>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (document is null)
            {
                return new ConversionQueueLoadResult(
                    null,
                    "The saved queue was empty or invalid and was not restored.");
            }

            if (document.Version != ConversionQueueDocument.CurrentVersion)
            {
                return new ConversionQueueLoadResult(
                    null,
                    $"Queue format version {document.Version} is not supported. No saved plans were restored.");
            }

            return new ConversionQueueLoadResult(document);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new ConversionQueueLoadResult(
                null,
                "The saved queue could not be read. The app started with an empty queue.");
        }
    }

    public async Task SaveAsync(
        ConversionQueueDocument document,
        CancellationToken cancellationToken = default)
    {
        if (document.Version != ConversionQueueDocument.CurrentVersion)
        {
            throw new ArgumentException("Only the current queue format can be saved.", nameof(document));
        }

        var path = GetQueuePath();
        var temporaryPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetQueuePath() =>
        Path.Combine(FileSystem.AppDataDirectory, "queue", FileName);
}
