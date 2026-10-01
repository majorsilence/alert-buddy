using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Android.Platform
{
    /// <summary>
    /// The app lifecycle and the back button as the app's own interface. It exists from the moment the app is built, whether or not a
    /// window does, so the head connects the activity's events to it when there is one.
    /// </summary>
    internal sealed class AndroidLifecycle : ILifecycle
    {
        public event Action? Resumed;

        public event Action? Paused;

        public event Func<bool>? BackPressed;

        public void RaiseResumed () => Resumed?.Invoke ();

        public void RaisePaused () => Paused?.Invoke ();

        /// <summary>True when something inside the app took the back press, so the app stays open.</summary>
        public bool RaiseBack ()
        {
            if (BackPressed is not { } handlers)
                return false;

            foreach (var handler in handlers.GetInvocationList ().Cast<Func<bool>> ())
                if (handler ())
                    return true;

            return false;
        }
    }
}
