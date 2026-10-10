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
        /// <param name="onUnlocked">What to run once the PIN is right.</param>
        /// <param name="holdDone">True when the control that opened the gate was itself a press-and-hold, so the gate goes straight to the PIN pad rather than asking for a second hold.</param>
        GateViewModel Gate (Action onUnlocked, bool holdDone = false);
    }
}
