using AlertBuddy.Core.Localization;
using System.Globalization;
using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;
using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>One alert in full: source, level, when, temperature, the message, and what to do (PLAN.md section 9).</summary>
    public sealed partial class AlertDetailViewModel : ScreenViewModel
    {
        private readonly AlertEngine engine;
        private readonly AlertHub hub;
        private readonly INavigator navigator;
        private readonly IClock clock;
        private readonly TimeZoneInfo zone;
        private readonly string alertId;

        [ObservableProperty]
        private string source = "";

        [ObservableProperty]
        private string levelWord = "";

        [ObservableProperty]
        private string timeAgo = "";

        [ObservableProperty]
        private string clockTime = "";

        [ObservableProperty]
        private double? temperature;

        [ObservableProperty]
        private string body = "";

        [ObservableProperty]
        private string whatToDo = "";

        [ObservableProperty]
        private AlertLevel level;

        [ObservableProperty]
        private AlertStatus status;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor (nameof (GotItCommand))]
        private bool canBeHandled;

        /// <summary>Creates the detail screen for an alert.</summary>
        public AlertDetailViewModel (Alert alert, AlertEngine engine, AlertHub hub, INavigator navigator, IClock clock, IUiDispatcher dispatcher, TimeZoneInfo? zone = null)
        {
            ArgumentNullException.ThrowIfNull (alert);
            this.engine = engine ?? throw new ArgumentNullException (nameof (engine));
            this.hub = hub ?? throw new ArgumentNullException (nameof (hub));
            this.navigator = navigator ?? throw new ArgumentNullException (nameof (navigator));
            this.clock = clock ?? throw new ArgumentNullException (nameof (clock));
            this.zone = zone ?? TimeZoneInfo.Local;
            alertId = alert.Id;

            Own (new HubSubscription (hub, dispatcher, _ => Refresh ()));
            Apply (alert);
        }

        /// <summary>The temperature as a sentence ("41 degrees"), or empty.</summary>
        public string TemperatureText => Temperature is { } t ? Words.Degrees (t) : "";

        [RelayCommand]
        private void Back () => navigator.GoBack ();

        /// <summary>The grown-up's "Got it". The view holds the button before this runs.</summary>
        [RelayCommand (CanExecute = nameof (CanBeHandled))]
        private void GotIt () => engine.MarkHandled (alertId);

        private void Refresh ()
        {
            if (IsDisposed)
                return;

            var latest = hub.Snapshot.History.FirstOrDefault (a => a.Id == alertId);
            if (latest is not null)
                Apply (latest);
        }

        private void Apply (Alert alert)
        {
            Source = alert.Source;
            Level = alert.Level;
            Status = alert.Status;
            Temperature = alert.Temperature;
            OnPropertyChanged (nameof (TemperatureText));
            Body = alert.Body;

            LevelWord = alert.Status == AlertStatus.Resolved ? Loc.T ("All clear") : alert.Level == AlertLevel.Alarm ? Loc.T ("Alarm") : Loc.T ("Warning");
            WhatToDo = alert.Status == AlertStatus.Resolved ? "" : alert.Level == AlertLevel.Alarm ? Words.TellAGrownUpNow : Loc.T ("Keep an eye on it.");
            TimeAgo = Loc.F ("{0} ago", Words.TimeAgo (clock.Now - alert.Time));
            ClockTime = TimeZoneInfo.ConvertTime (alert.Time, zone).ToString ("HH:mm", CultureInfo.InvariantCulture);
            CanBeHandled = alert.IsOpen && alert.Status != AlertStatus.Handled;
        }
    }
}
