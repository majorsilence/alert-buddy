using System.ComponentModel;

namespace AlertBuddy.Shared.Binding
{
    /// <summary>
    /// Wires a view model property to a control with no reflection and no expression trees (PLAN.md section 7.5). A minimal copy of the
    /// framework's future <c>Majorsilence.Forms.Mvvm</c> package (F2); delete once that ships.
    /// // TEMP-SHIM (F2)
    /// </summary>
    public static class ObserveExtensions
    {
        /// <summary>
        /// Pushes <paramref name="getter"/>'s current value through <paramref name="setter"/> now, and again every time
        /// <paramref name="vm"/> raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for <paramref name="propertyName"/>. The view
        /// model raises that event on the UI thread already (through <c>IUiDispatcher</c>), so this never dispatches itself. Dispose the
        /// result when the view is left.
        /// </summary>
        public static IDisposable Observe<TViewModel, TValue> (this TViewModel vm, string propertyName, Func<TViewModel, TValue> getter, Action<TValue> setter)
            where TViewModel : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (vm);
            ArgumentNullException.ThrowIfNull (propertyName);
            ArgumentNullException.ThrowIfNull (getter);
            ArgumentNullException.ThrowIfNull (setter);

            void Push () => setter (getter (vm));

            void OnPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                // A null or empty name (some sources raise it for "everything changed") is treated as a match.
                if (string.IsNullOrEmpty (e.PropertyName) || e.PropertyName == propertyName)
                    Push ();
            }

            vm.PropertyChanged += OnPropertyChanged;
            Push ();

            return new Unsubscribe (() => vm.PropertyChanged -= OnPropertyChanged);
        }

        private sealed class Unsubscribe (Action undo) : IDisposable
        {
            private bool disposed;

            public void Dispose ()
            {
                if (disposed)
                    return;

                disposed = true;
                undo ();
            }
        }
    }
}
