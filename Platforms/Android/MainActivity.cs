using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MediaForge.Universal;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PickMediaRequest = 7401;
    private static TaskCompletionSource<IReadOnlyList<global::Android.Net.Uri>>? _pendingPicker;

    public static Task<IReadOnlyList<global::Android.Net.Uri>> PickMediaAsync()
    {
        if (Platform.CurrentActivity is not MainActivity activity)
            throw new InvalidOperationException("The Android activity is unavailable.");
        if (_pendingPicker is not null)
            throw new InvalidOperationException("A media picker is already open.");

        _pendingPicker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, true);
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "video/*", "audio/*", "image/*" });
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, PickMediaRequest);
        return _pendingPicker.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != PickMediaRequest || _pendingPicker is null) return;

        var completion = _pendingPicker;
        _pendingPicker = null;
        var uris = new List<global::Android.Net.Uri>();
        if (resultCode == Result.Ok && data is not null)
        {
            if (data.ClipData is { } clip)
            {
                for (var index = 0; index < clip.ItemCount; index++)
                    if (clip.GetItemAt(index)?.Uri is { } uri) uris.Add(uri);
            }
            else if (data.Data is { } uri)
            {
                uris.Add(uri);
            }
        }

        completion.TrySetResult(uris);
    }
}
