using System.Text;
using Android.App;
using Android.Content;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// Saves and loads the settings file through Android's own file picker (the Storage Access Framework): the person says where, so no storage
    /// permission is asked for, and a file they keep (in Downloads, on a cloud drive) survives the app being removed, which nothing the app
    /// stores for itself does. <see cref="MainActivity"/> forwards the picker's answer to <see cref="OnActivityResult"/>.
    /// </summary>
    internal sealed class AndroidSettingsTransfer : ISettingsTransfer
    {
        private const int CreateRequest = 7101;
        private const int OpenRequest = 7102;
        private static TaskCompletionSource<global::Android.Net.Uri?>? pending;

        public bool IsSupported => true;

        public async Task<bool> SaveAsync (string suggestedName, string text)
        {
            var intent = new Intent (Intent.ActionCreateDocument)
                .AddCategory (Intent.CategoryOpenable)!
                .SetType ("application/json")!
                .PutExtra (Intent.ExtraTitle, suggestedName)!;

            if (await PickAsync (CreateRequest, intent) is not { } uri || MainActivity.Current?.ContentResolver is not { } resolver)
                return false;

            try {
                // "wt" truncates: saving over an older, longer copy must not leave its tail behind.
                await using var stream = resolver.OpenOutputStream (uri, "wt");
                if (stream is null)
                    return false;

                await stream.WriteAsync (Encoding.UTF8.GetBytes (text));
                return true;
            } catch (Exception) {
                return false;
            }
        }

        public async Task<string?> LoadAsync ()
        {
            var intent = new Intent (Intent.ActionOpenDocument)
                .AddCategory (Intent.CategoryOpenable)!
                .SetType ("*/*")!;

            if (await PickAsync (OpenRequest, intent) is not { } uri || MainActivity.Current?.ContentResolver is not { } resolver)
                return null;

            try {
                await using var stream = resolver.OpenInputStream (uri);
                if (stream is null)
                    return null;

                // A settings file is a few kilobytes; anything much bigger is not one.
                var buffer = new MemoryStream ();
                await stream.CopyToAsync (buffer);
                return buffer.Length > 256 * 1024 ? null : Encoding.UTF8.GetString (buffer.ToArray ());
            } catch (Exception) {
                return null;
            }
        }

        private static Task<global::Android.Net.Uri?> PickAsync (int requestCode, Intent intent)
        {
            var activity = MainActivity.Current;
            if (activity is null)
                return Task.FromResult<global::Android.Net.Uri?> (null);

            var source = new TaskCompletionSource<global::Android.Net.Uri?> ();
            pending?.TrySetResult (null);
            pending = source;
            activity.RunOnUiThread (() => {
#pragma warning disable CA1422, CS0618 // The activity-result callbacks are deprecated for new code but are what an AvaloniaMainActivity forwards.
                activity.StartActivityForResult (intent, requestCode);
#pragma warning restore CA1422, CS0618
            });
            return source.Task;
        }

        /// <summary>The picker's answer, from <see cref="MainActivity"/>.</summary>
        public static void OnActivityResult (int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode is not (CreateRequest or OpenRequest))
                return;

            var source = pending;
            pending = null;
            source?.TrySetResult (resultCode == Result.Ok ? data?.Data : null);
        }
    }
}
