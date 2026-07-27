#if ANDROID
using Android.Content;
using MediaForge.GenZ.Core.Contracts;
using MediaForge.GenZ.Core.Models;

namespace MediaForge.Universal.Platforms.Android.Services;

public sealed class AndroidOutputActions : IOutputOpener, IShareService
{
    private readonly Context _context = global::Android.App.Application.Context;

    public Task<ValidationResult> OpenAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RunAction(output, share: false));
    }

    public Task<ValidationResult> ShareAsync(
        MediaAsset output,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RunAction(output, share: true));
    }

    private ValidationResult RunAction(MediaAsset output, bool share)
    {
        try
        {
            var uri = ParseAndVerify(output);
            Intent intent;
            if (share)
            {
                intent = new Intent(Intent.ActionSend);
                intent.SetType(output.ContentType ?? "audio/mp4");
                intent.PutExtra(Intent.ExtraStream, uri);
                intent.ClipData = ClipData.NewRawUri("MediaForge output", uri);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent = Intent.CreateChooser(intent, "Share converted audio") ?? intent;
            }
            else
            {
                intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(uri, output.ContentType ?? "audio/mp4");
                intent.ClipData = ClipData.NewRawUri("MediaForge output", uri);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            }

            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
            return ValidationResult.Success;
        }
        catch (global::Android.Content.ActivityNotFoundException)
        {
            return ValidationResult.Failure(
                share
                    ? "No compatible share target is installed."
                    : "No compatible audio app is installed.");
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or IOException or
            UnauthorizedAccessException or Java.Lang.SecurityException)
        {
            return ValidationResult.Failure(
                $"The completed output is unavailable: {exception.Message}");
        }
    }

    private global::Android.Net.Uri ParseAndVerify(MediaAsset output)
    {
        var uri = global::Android.Net.Uri.Parse(output.Id);
        if (uri is null)
        {
            throw new FileNotFoundException("The saved output reference is invalid.");
        }

        if (uri.Scheme?.Equals("content", StringComparison.OrdinalIgnoreCase) is not true)
        {
            throw new FileNotFoundException(
                "Opening legacy app-private output is unavailable on this Android version.");
        }

        using var descriptor = _context.ContentResolver?.OpenAssetFileDescriptor(uri, "r") ??
            throw new FileNotFoundException("The saved output could not be opened.");
        if (descriptor.Length == 0)
        {
            throw new IOException("The saved output is empty.");
        }

        return uri;
    }
}
#endif
