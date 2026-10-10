using AlertBuddy.Core.Alerts;
using AlertBuddy.ViewModels.Screens;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// Builds the screens that need an argument: the detail of one alert, the takeover for one alarm, the gate in front of one action.
    /// Implemented once, in the composition root, so no view model needs to know how another is made.
    /// </summary>
    public interface IScreenFactory
    {
        /// <summary>The detail screen for an alert.</summary>
        AlertDetailViewModel Detail (Alert alert);

        /// <summary>The full-screen takeover for an alarm.</summary>
        AlarmViewModel Alarm (Alert alert);

        /// <summary>The grown-up gate, which runs <paramref name="onUnlocked"/> once the PIN is right.</summary>
        GateViewModel Gate (Action onUnlocked);
    }
}
