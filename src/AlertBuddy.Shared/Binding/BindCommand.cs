using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Binding
{
    /// <summary>
    /// Wires a <see cref="Control"/> to an <see cref="ICommand"/> with no <c>ButtonBase.Command</c> to set yet (F3): sets
    /// <see cref="Control.Enabled"/> from <see cref="ICommand.CanExecute"/>, follows <see cref="ICommand.CanExecuteChanged"/>, and calls
    /// <see cref="ICommand.Execute"/> on <see cref="Control.Click"/>. For an <see cref="IAsyncRelayCommand"/> it also disables the control
    /// while the command is running (PLAN.md section 7.5). // TEMP-SHIM (F2, F3)
    /// </summary>
    public static class BindCommandExtensions
    {
        /// <summary>Binds <paramref name="control"/>'s click to <paramref name="command"/>. Dispose the result when the view is left.</summary>
        public static IDisposable BindCommand (this Control control, ICommand command)
        {
            ArgumentNullException.ThrowIfNull (control);
            ArgumentNullException.ThrowIfNull (command);

            void UpdateEnabled () => control.Enabled = command.CanExecute (null) && !IsRunning (command);

            void OnCanExecuteChanged (object? sender, EventArgs e) => UpdateEnabled ();
            void OnClick (object? sender, EventArgs e)
            {
                if (command.CanExecute (null))
                    command.Execute (null);
            }
            void OnCommandPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is null or nameof (IAsyncRelayCommand.IsRunning))
                    UpdateEnabled ();
            }

            command.CanExecuteChanged += OnCanExecuteChanged;
            control.Click += OnClick;

            if (command is INotifyPropertyChanged notifying)
                notifying.PropertyChanged += OnCommandPropertyChanged;

            UpdateEnabled ();

            return new Unsubscribe (() => {
                command.CanExecuteChanged -= OnCanExecuteChanged;
                control.Click -= OnClick;
                if (command is INotifyPropertyChanged np)
                    np.PropertyChanged -= OnCommandPropertyChanged;
            });
        }

        private static bool IsRunning (ICommand command) => command is IAsyncRelayCommand { IsRunning: true };

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
