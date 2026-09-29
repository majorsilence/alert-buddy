using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Desktop cannot vibrate: the framework's own <c>Haptics</c> (register item F13) is real only on Android and iOS, false everywhere
    /// else including Headless -- there is no desktop equivalent worth a "supported but does nothing" middle state.
    /// </summary>
    public sealed class DesktopHaptics : IHaptics
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public void Tap () { }

        /// <inheritdoc/>
        public void Alarm () { }

        /// <inheritdoc/>
        public void Stop () { }
    }
}
