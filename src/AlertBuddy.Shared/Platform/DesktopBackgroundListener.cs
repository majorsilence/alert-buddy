using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Desktop has nothing to report and is simply "fine" (PLAN.md's own words for this case): there is no OS-level background
    /// restriction to work around the way Android's is, so the listener already started by <c>AlertListener</c> keeps running for as
    /// long as the process does.
    /// </summary>
    public sealed class DesktopBackgroundListener : IBackgroundListener
    {
        /// <inheritdoc/>
        public bool CanListenInBackground => true;

        /// <inheritdoc/>
        public string? WhyNot => null;

        /// <inheritdoc/>
        public void Start () { }

        /// <inheritdoc/>
        public void Stop () { }
    }
}
