using AlertBuddy.Core.Abstractions;

namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// Counts wrong PINs across every visit to the gate. It lives outside any one screen: if the count were part of the gate screen, a child
    /// could reset it by pressing back and coming in again, and a four-digit PIN would fall in ten thousand tries with no delay.
    /// </summary>
    public sealed class GateLock (IClock clock)
    {
        /// <summary>Wrong PINs allowed before the gate closes for a while.</summary>
        public const int MaxAttempts = 5;

        /// <summary>How long it stays closed.</summary>
        public static readonly TimeSpan LockedFor = TimeSpan.FromSeconds (30);

        private readonly object gate = new ();
        private int failures;
        private DateTimeOffset lockedUntil;

        /// <summary>Whether the gate is closed right now.</summary>
        public bool IsLocked {
            get {
                lock (gate)
                    return clock.Now < lockedUntil;
            }
        }

        /// <summary>When the gate reopens, if it is closed.</summary>
        public DateTimeOffset LockedUntil {
            get {
                lock (gate)
                    return lockedUntil;
            }
        }

        /// <summary>Records a wrong PIN. Returns true if that closed the gate.</summary>
        public bool RegisterFailure ()
        {
            lock (gate) {
                if (++failures < MaxAttempts)
                    return false;

                failures = 0;
                lockedUntil = clock.Now + LockedFor;
                return true;
            }
        }

        /// <summary>Records the right PIN.</summary>
        public void RegisterSuccess ()
        {
            lock (gate)
                failures = 0;
        }
    }
}
