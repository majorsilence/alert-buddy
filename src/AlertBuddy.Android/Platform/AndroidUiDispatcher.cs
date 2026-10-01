using Android.OS;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// Posts to the main looper, which is the UI thread whether or not an Activity exists. The foreground service builds the app with no
    /// window at all, so a dispatcher that needs a form (the desktop one) could not be used there.
    /// </summary>
    internal sealed class AndroidUiDispatcher : IUiDispatcher
    {
        private readonly Handler handler = new (Looper.MainLooper!);

        public void Post (Action action) => handler.Post (action);
    }
}
