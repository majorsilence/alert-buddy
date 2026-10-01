using System.ComponentModel;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace AlertBuddy.Shared.Binding
{
    /// <summary>
    /// Two-way bindings for the input controls, with no reflection and no expression trees (PLAN.md section 7.5). The framework's
    /// <c>Majorsilence.Forms.Mvvm</c> has <c>Observe</c> and <c>BindCommand</c> but no two-way text (majorsilence/Majorsilence.Forms#352).
    /// // TEMP-SHIM (F26)
    /// </summary>
    public static class FormBindings
    {
        /// <summary>Keeps <paramref name="box"/> and a string property of <paramref name="vm"/> equal in both directions.</summary>
        public static IDisposable BindText<TViewModel> (this TextBox box, TViewModel vm, string propertyName, Func<TViewModel, string> get, Action<TViewModel, string> set)
            where TViewModel : INotifyPropertyChanged
            => TwoWay (vm, propertyName, get, set, () => box.Text, text => box.Text = text, h => box.TextChanged += h, h => box.TextChanged -= h);

        /// <summary>Keeps <paramref name="box"/> and a bool property of <paramref name="vm"/> equal in both directions.</summary>
        public static IDisposable BindChecked<TViewModel> (this CheckBox box, TViewModel vm, string propertyName, Func<TViewModel, bool> get, Action<TViewModel, bool> set)
            where TViewModel : INotifyPropertyChanged
            => TwoWay (vm, propertyName, get, set, () => box.Checked, value => box.Checked = value, h => box.CheckedChanged += h, h => box.CheckedChanged -= h);

        /// <summary>Keeps a drop-down's selected position and an int property of <paramref name="vm"/> equal in both directions.</summary>
        public static IDisposable BindIndex<TViewModel> (this ComboBox box, TViewModel vm, string propertyName, Func<TViewModel, int> get, Action<TViewModel, int> set)
            where TViewModel : INotifyPropertyChanged
            => TwoWay (vm, propertyName, get, set, () => box.SelectedIndex, index => box.SelectedIndex = index, h => box.SelectedIndexChanged += h, h => box.SelectedIndexChanged -= h);

        /// <summary>Keeps a number box and an int property of <paramref name="vm"/> equal in both directions.</summary>
        public static IDisposable BindNumber<TViewModel> (this NumericUpDown box, TViewModel vm, string propertyName, Func<TViewModel, int> get, Action<TViewModel, int> set)
            where TViewModel : INotifyPropertyChanged
            => TwoWay (vm, propertyName, get, set, () => (int)box.Value, value => box.Value = Math.Clamp (value, (int)box.Minimum, (int)box.Maximum), h => box.ValueChanged += h, h => box.ValueChanged -= h);

        private static IDisposable TwoWay<TViewModel, TValue> (
            TViewModel vm, string propertyName, Func<TViewModel, TValue> get, Action<TViewModel, TValue> set,
            Func<TValue> read, Action<TValue> write, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
            where TViewModel : INotifyPropertyChanged
        {
            // Writing to the control raises its change event, which would write back to the view model: the guard stops that loop, and
            // only writing a value that differs keeps the caret where the person left it.
            var updating = false;

            void ToControl (TValue value)
            {
                if (updating || EqualityComparer<TValue>.Default.Equals (read (), value))
                    return;

                updating = true;
                try {
                    write (value);
                } finally {
                    updating = false;
                }
            }

            void ToViewModel (object? sender, EventArgs e)
            {
                if (updating)
                    return;

                updating = true;
                try {
                    set (vm, read ());
                } finally {
                    updating = false;
                }
            }

            var observing = vm.Observe (propertyName, get, ToControl);
            subscribe (ToViewModel);
            return new Undo (() => {
                unsubscribe (ToViewModel);
                observing.Dispose ();
            });
        }

        private sealed class Undo (Action undo) : IDisposable
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
