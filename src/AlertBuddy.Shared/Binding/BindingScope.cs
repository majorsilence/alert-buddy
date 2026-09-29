namespace AlertBuddy.Shared.Binding
{
    /// <summary>
    /// Collects a page's subscriptions (from <see cref="Observe"/>, <see cref="BindCommand"/> and anything else) and disposes them
    /// together when the page is left, so no view leaks a subscription (PLAN.md section 7.5). // TEMP-SHIM (F2)
    /// </summary>
    public sealed class BindingScope : IDisposable
    {
        private readonly List<IDisposable> owned = [];
        private bool disposed;

        /// <summary>Adds a disposable to be released with the rest of this scope. Returns it so a caller can chain.</summary>
        public T Add<T> (T disposable) where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull (disposable);

            if (disposed) {
                disposable.Dispose ();
                return disposable;
            }

            owned.Add (disposable);
            return disposable;
        }

        /// <summary>Releases everything collected so far. Safe to call more than once.</summary>
        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (var item in owned)
                item.Dispose ();
            owned.Clear ();
        }
    }
}
