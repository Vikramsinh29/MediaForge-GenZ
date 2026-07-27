#if ANDROID
using System.Runtime.InteropServices;
using System.Text;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidPocTranscoder : ITranscoder
{
    private const string NativeLibrary = "mediaforge_poc";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeProgressCallback(double fraction, long processedMilliseconds);

    [DllImport(NativeLibrary, EntryPoint = "mf_transcode_wav_to_m4a")]
    private static extern int TranscodeWavToM4A(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
        NativeProgressCallback progress,
        IntPtr cancellationFlag,
        StringBuilder error,
        int errorCapacity);

    public async Task<ConversionExecutionResult> ProcessAsync(
        ConversionJob job,
        Stream source,
        Stream temporaryOutput,
        IProgress<ConversionJobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(job.Plan.Source.DisplayName)
                .Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            job.Plan.OutputFormat != OutputFormat.M4A)
        {
            return new ConversionExecutionResult(
                job.Id,
                false,
                "The development adapter accepts only WAV input planned as M4A.");
        }

        var workDirectory = Path.Combine(
            global::Android.App.Application.Context.CacheDir!.AbsolutePath,
            "transcoding-poc",
            job.Id);
        Directory.CreateDirectory(workDirectory);
        var inputPath = Path.Combine(workDirectory, "input.wav");
        var outputPath = Path.Combine(workDirectory, "output.m4a");
        var cancellationFlag = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(cancellationFlag, 0);

        try
        {
            await using (var inputFile = new FileStream(
                             inputPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(inputFile, cancellationToken);
                await inputFile.FlushAsync(cancellationToken);
            }

            var nativeProgress = new NativeProgressCallback(
                (fraction, milliseconds) =>
                    progress?.Report(
                        new ConversionJobProgress(
                            job.Id,
                            ConversionJobState.Processing,
                            Math.Clamp(fraction, 0, 1),
                            TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)),
                            "Encoding AAC audio.")));
            var error = new StringBuilder(1024);
            using var registration = cancellationToken.Register(
                () => Marshal.WriteInt32(cancellationFlag, 1));

            var result = await Task.Run(
                () => TranscodeWavToM4A(
                    inputPath,
                    outputPath,
                    nativeProgress,
                    cancellationFlag,
                    error,
                    error.Capacity),
                CancellationToken.None);
            GC.KeepAlive(nativeProgress);

            if (result == -2 || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (result != 0)
            {
                return new ConversionExecutionResult(
                    job.Id,
                    false,
                    string.IsNullOrWhiteSpace(error.ToString())
                        ? $"Native conversion failed with code {result}."
                        : error.ToString());
            }

            var outputInfo = new FileInfo(outputPath);
            if (!outputInfo.Exists || outputInfo.Length == 0)
            {
                return new ConversionExecutionResult(
                    job.Id,
                    false,
                    "Native conversion produced no readable output.");
            }

            await using var outputFile = new FileStream(
                outputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await outputFile.CopyToAsync(temporaryOutput, cancellationToken);
            return new ConversionExecutionResult(job.Id, true);
        }
        catch (DllNotFoundException)
        {
            return new ConversionExecutionResult(
                job.Id,
                false,
                "The external development native library is not packaged.");
        }
        finally
        {
            Marshal.FreeHGlobal(cancellationFlag);
            TryDelete(inputPath);
            TryDelete(outputPath);
            try
            {
                Directory.Delete(workDirectory, false);
            }
            catch
            {
                // App-owned cache cleanup is best effort.
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // App-owned cache cleanup is best effort.
        }
    }
}
#endif
