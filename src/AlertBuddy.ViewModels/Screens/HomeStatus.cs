using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Store;
using AlertBuddy.ViewModels.Copy;

namespace AlertBuddy.ViewModels.Screens
{
    /// <summary>What the beacon is doing (PLAN.md section 8.6).</summary>
    public enum BeaconMood
    {
        /// <summary>No connection, or nothing happening in the night: dim, eyes closed, slow breathing.</summary>
        Asleep,

        /// <summary>Connected and all quiet: the lamp soft, eyes open, glancing around.</summary>
        Watching,

        /// <summary>A warning is open: amber, a sweat drop.</summary>
        Warning,

        /// <summary>An alarm nobody has responded to: red, spinning, eyes wide.</summary>
        Alarm,

        /// <summary>An alarm the child has told a grown-up about: calm, a slow amber pulse.</summary>
        Reassured,

        /// <summary>The all clear, for a few seconds: mint, a happy squint.</summary>
        AllClear,
    }

    /// <summary>The beacon's mood and the one sentence its speech bubble says.</summary>
    public sealed record HomeStatus (BeaconMood Mood, string Text);

    /// <summary>An all clear being celebrated: which source, and whether it was about heat (which decides the words).</summary>
    public sealed record AllClearInfo (string Source, bool HeatRelated);

    /// <summary>
    /// Works out what Home says, from what is true. Pure: the same inputs give the same answer, so every state is tested by calling it
    /// (PLAN.md section 10) and Practice mode reuses it unchanged.
    /// </summary>
    public static class HomeStatusCalculator
    {
        /// <summary>How long the beacon celebrates an all clear before going back to watching.</summary>
        public static readonly TimeSpan AllClearFor = TimeSpan.FromSeconds (4);

        /// <summary>Computes the status.</summary>
        /// <param name="snapshot">The open alerts and the connection.</param>
        /// <param name="buddy">The buddy's name.</param>
        /// <param name="allClear">The alert that has just cleared, while the celebration lasts; null otherwise.</param>
        /// <param name="isNight">Whether it is quiet hours, when an idle buddy sleeps.</param>
        public static HomeStatus Compute (HubSnapshot snapshot, string buddy, AllClearInfo? allClear, bool isNight)
        {
            ArgumentNullException.ThrowIfNull (snapshot);

            // An alarm outranks everything, including a lost connection: a stale alarm is still worth showing, and hiding it because the
            // network dropped would be the wrong way to be wrong.
            var alarms = snapshot.Active.Where (a => a.Level == AlertLevel.Alarm).ToList ();
            if (alarms.Count > 0) {
                var pending = alarms.Any (a => a.Status == AlertStatus.Active);
                return pending
                    ? new HomeStatus (BeaconMood.Alarm, Words.TellAGrownUpNow)
                    : new HomeStatus (BeaconMood.Reassured, Words.ThankYou);
            }

            var warnings = snapshot.Active.Where (a => a.Level == AlertLevel.Warning).ToList ();
            if (warnings.Count > 0) {
                // "Getting warm" is only true of an alert that carried a temperature; anything else is simply "needs a look".
                var heat = warnings.All (a => a.Temperature is not null);
                return new HomeStatus (BeaconMood.Warning, (warnings.Count, heat) switch {
                    (1, true) => Words.GettingWarm (warnings[0].Source),
                    (1, false) => Words.NeedsALook (warnings[0].Source),
                    (_, true) => Words.SeveralWarm (warnings.Count),
                    _ => Words.SeveralNeedALook (warnings.Count),
                });
            }

            if (allClear is not null)
                return new HomeStatus (BeaconMood.AllClear, allClear.HeatRelated ? Words.AllClear (allClear.Source) : Words.AllClearNeutral (allClear.Source));

            if (snapshot.Connection.State != ConnectionState.Live)
                return new HomeStatus (BeaconMood.Asleep, Words.CannotHear (buddy));

            return isNight
                ? new HomeStatus (BeaconMood.Asleep, Words.AllQuiet (buddy))
                : new HomeStatus (BeaconMood.Watching, Words.AllQuiet (buddy));
        }

        /// <summary>The small connection line under the buddy, one sentence per state.</summary>
        public static string ConnectionSentence (ConnectionInfo connection, DateTimeOffset now, bool configured)
        {
            ArgumentNullException.ThrowIfNull (connection);

            if (!configured)
                return Words.NotSetUp;

            return connection.State switch {
                ConnectionState.Live => Words.Listening (connection.LastHeard is { } heard ? now - heard : null),
                ConnectionState.Connecting => Words.Connecting,
                ConnectionState.AuthFailed => Words.SignInRefused,
                ConnectionState.Misconfigured => connection.Problem switch {
                    ConnectionProblem.TopicNotFound => Words.TopicNotFound,
                    ConnectionProblem.Certificate => Words.CertificateProblem,
                    _ => Words.AddressProblem,
                },
                _ => Words.CannotReachTheHouse,     // reconnecting or offline
            };
        }
    }
}
