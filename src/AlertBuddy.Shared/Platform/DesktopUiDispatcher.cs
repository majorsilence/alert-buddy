using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Runs work on the UI thread via the window's own <see cref="Form.BeginInvoke(Action)"/> (PLAN.md section 7.5). Takes a getter
    /// rather than the form itself: <c>AlertBuddyApp.Create</c> needs a dispatcher before the head has built its window, but nothing
    /// posts to it until after the window exists and the listener has started.
    /// </summary>
    public sealed class DesktopUiDispatcher (Func<Form> host) : IUiDispatcher
    {
        private readonly Func<Form> host = host ?? throw new ArgumentNullException (nameof (host));

        /// <inheritdoc/>
        public void Post (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            host ().BeginInvoke (action);
        }
    }
}
