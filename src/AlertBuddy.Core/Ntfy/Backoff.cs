namespace AlertBuddy.Core.Ntfy
{
    /// <summary>The timing rules of the listener (PLAN.md section 5.2).</summary>
    public static class Backoff
    {
        private static readonly int[] Seconds = [1, 2, 4, 8, 16, 32, 60];

        /// <summary>If nothing at all, keepalives included, arrives for this long, the connection is dropped and re-made. The server sends one about every 45 seconds.</summary>
        public static readonly TimeSpan Watchdog = TimeSpan.FromSeconds (110);

        /// <summary>Streaming this long without a drop counts as healthy, and the backoff starts again from one second.</summary>
        public static readonly TimeSpan HealthyAfter = TimeSpan.FromSeconds (30);

        /// <summary>How often a refused sign-in or a missing topic is retried. Slow on purpose: hammering a server that said no helps nobody.</summary>
        public static readonly TimeSpan SlowRetry = TimeSpan.FromMinutes (5);

        /// <summary>
        /// The wait before reconnect attempt number <paramref name="attempt"/> (0 for the first): 1, 2, 4, 8, 16, 32, then 60 seconds, each
        /// varied by up to 20 percent either way so a fleet of apps does not reconnect in step after a server restart.
        /// </summary>
        /// <param name="attempt">How many attempts have already failed in a row.</param>
        /// <param name="jitter">A random number from 0 up to 1. 0.5 is exactly the base delay.</param>
        public static TimeSpan Delay (int attempt, double jitter)
        {
            var baseSeconds = Seconds[Math.Clamp (attempt, 0, Seconds.Length - 1)];
            var factor = 0.8 + 0.4 * Math.Clamp (jitter, 0.0, 1.0);
            return TimeSpan.FromSeconds (baseSeconds * factor);
        }
    }
}
