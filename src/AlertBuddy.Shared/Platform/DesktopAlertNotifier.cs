using AlertBuddy.Core.Alerts;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// No desktop system notification yet: the framework's <c>NotifyIcon.ShowBalloonTip</c> (register item F14's desktop half) is
    /// still a documented no-op, and the alarm takeover screen itself is the signal while the app is in the foreground, which is the
    /// only case this head runs today. Revisit once a real desktop toast lands upstream.
    /// </summary>
    public sealed class DesktopAlertNotifier : IAlertNotifier
    {
        /// <inheritdoc/>
        public void Show (Alert alert) { }

        /// <inheritdoc/>
        public void Clear (string alertId) { }
    }
}
