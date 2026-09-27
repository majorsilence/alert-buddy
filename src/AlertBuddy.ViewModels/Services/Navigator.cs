using CommunityToolkit.Mvvm.ComponentModel;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// A stack of screens. Registration is by type and construction is by delegate, so nothing is found by reflection and it survives
    /// trimming and iOS AOT (PLAN.md section 7.5).
    /// </summary>
    public sealed class Navigator : INavigator
    {
        private readonly Dictionary<Type, Func<ObservableObject>> factories = new ();
        private readonly List<ObservableObject> stack = [];

        /// <summary>The screen being shown. Throws until <see cref="SetRoot"/> has been called.</summary>
        public ObservableObject Current => stack.Count > 0 ? stack[^1] : throw new InvalidOperationException ("The navigator has no root screen yet.");

        /// <inheritdoc />
        public event Action? CurrentChanged;

        /// <inheritdoc />
        public bool CanGoBack => stack.Count > 1;

        /// <summary>Sets Home, the screen at the bottom of the stack that back never leaves.</summary>
        public void SetRoot (ObservableObject root)
        {
            ArgumentNullException.ThrowIfNull (root);
            stack.Clear ();
            stack.Add (root);
            CurrentChanged?.Invoke ();
        }

        /// <summary>Says how to build a screen. Called once at start-up for every screen <see cref="GoTo{T}"/> may open.</summary>
        public void Register<T> (Func<T> factory) where T : ObservableObject
        {
            ArgumentNullException.ThrowIfNull (factory);
            factories[typeof (T)] = () => factory ();
        }

        /// <inheritdoc />
        public void GoTo<T> () where T : ObservableObject
        {
            if (!factories.TryGetValue (typeof (T), out var factory))
                throw new InvalidOperationException ($"No screen is registered for {typeof (T).Name}.");

            Show (factory ());
        }

        /// <inheritdoc />
        public void Show (ObservableObject viewModel)
        {
            ArgumentNullException.ThrowIfNull (viewModel);
            stack.Add (viewModel);
            CurrentChanged?.Invoke ();
        }

        /// <inheritdoc />
        public void GoBack ()
        {
            if (!CanGoBack)
                return;

            Leave (stack[^1]);
            stack.RemoveAt (stack.Count - 1);
            CurrentChanged?.Invoke ();
        }

        /// <inheritdoc />
        public void GoHome ()
        {
            if (!CanGoBack)
                return;

            while (stack.Count > 1) {
                Leave (stack[^1]);
                stack.RemoveAt (stack.Count - 1);
            }

            CurrentChanged?.Invoke ();
        }

        /// <summary>
        /// The Android back button (PLAN.md section 6.5): steps back one screen before it ever leaves the app. Returns true if it handled
        /// the press. At Home it returns false, so the platform's normal behaviour applies.
        /// </summary>
        public bool HandleBack ()
        {
            // A screen may claim the button (the alarm takeover swallows it).
            if (Current is IHandlesBack screen && screen.HandleBack ())
                return true;

            if (!CanGoBack)
                return false;

            GoBack ();
            return true;
        }

        // A screen that holds subscriptions releases them when it is left, so nothing leaks (the BindingScope of PLAN.md section 7.5).
        private static void Leave (ObservableObject screen) => (screen as IDisposable)?.Dispose ();
    }
}
