using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>Keeps the screen on via the framework's own <see cref="Application.KeepScreenAwake"/> (register item F12, real on Windows, macOS and Linux).</summary>
    public sealed class DesktopKeepAwake : IKeepAwake
    {
        /// <inheritdoc/>
        public bool Enabled {
            get => Application.KeepScreenAwake;
            set => Application.KeepScreenAwake = value;
        }
    }
}
