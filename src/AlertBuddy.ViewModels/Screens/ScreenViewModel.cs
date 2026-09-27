using CommunityToolkit.Mvvm.ComponentModel;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>
    /// The base of a screen's view model. It owns what the screen subscribes to (hub events, timers) and lets go of all of it when the
    /// screen is left, so a screen that has gone away can never be updated by an event it should not hear.
    /// </summary>
    public abstract class ScreenViewModel : ObservableObject, IDisposable
    {
        private readonly List<IDisposable> owned = [];
        private bool disposed;

        /// <summary>Hands something to the screen to dispose when it is left.</summary>
        protected T Own<T> (T disposable) where T : IDisposable
        {
            owned.Add (disposable);
            return disposable;
        }

        /// <summary>Whether the screen has been left.</summary>
        protected bool IsDisposed => disposed;

        /// <summary>Releases everything the screen subscribed to. Safe to call twice.</summary>
        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (var item in owned)
                item.Dispose ();
            owned.Clear ();
            OnDisposed ();
        }

        /// <summary>For a screen that has more to release than what it <see cref="Own{T}"/>s.</summary>
        protected virtual void OnDisposed ()
        {
        }
    }
}
