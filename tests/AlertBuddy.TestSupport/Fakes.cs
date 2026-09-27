using AlertBuddy.Core.Abstractions;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;

namespace AlertBuddy.TestSupport
{
    /// <summary>A clock the test moves by hand, backed by a <see cref="ManualTimeProvider"/> so the same object drives timers and delays.</summary>
    public sealed class TestClock : IClock
    {
        /// <summary>The provider, for code that takes a <see cref="TimeProvider"/>.</summary>
        public ManualTimeProvider Provider { get; }

        /// <summary>Starts at the given instant, by default the same fictional afternoon the tests use everywhere.</summary>
        public TestClock (DateTimeOffset? start = null)
            => Provider = new ManualTimeProvider (start ?? new DateTimeOffset (2026, 9, 26, 12, 0, 0, TimeSpan.Zero));

        /// <inheritdoc />
        public DateTimeOffset Now => Provider.GetUtcNow ();

        /// <summary>Moves time forward, running any timers that come due.</summary>
        public void Advance (TimeSpan by) => Provider.Advance (by);

        /// <summary>
        /// Waits until the code under test has stopped creating and cancelling timers, then returns how long until the soonest one is due.
        /// That delay IS what the code chose (a backoff, a watchdog), so a test asserts it directly. Throws if nothing is waiting.
        /// </summary>
        public async Task<TimeSpan> NextDelayAsync ()
        {
            await SettleAsync ().ConfigureAwait (false);
            var delays = Provider.PendingDelays;
            return delays.Count > 0 ? delays[0] : throw new InvalidOperationException ("Nothing is waiting on a timer.");
        }

        /// <summary>Waits for the code under test to settle, then advances exactly to its next timer.</summary>
        public async Task<TimeSpan> AdvanceToNextAsync ()
        {
            var delay = await NextDelayAsync ().ConfigureAwait (false);
            Advance (delay);
            return delay;
        }

        /// <summary>Waits until no timer has been created, changed or disposed for a short while: the async work has caught up.</summary>
        public async Task SettleAsync ()
        {
            var stableFor = 0;
            var last = Provider.Version;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds (10);

            while (stableFor < 4) {
                await Task.Delay (5).ConfigureAwait (false);
                var now = Provider.Version;
                stableFor = now == last ? stableFor + 1 : 0;
                last = now;

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException ("The code under test never settled.");
            }
        }
    }

    /// <summary>Secrets in memory. For tests only: nothing under <c>src/</c> references this project, so it cannot ship (PLAN.md section 7.4).</summary>
    public sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> values = new ();

        /// <inheritdoc />
        public string? Get (string key) => values.GetValueOrDefault (key);

        /// <inheritdoc />
        public void Set (string key, string value) => values[key] = value;

        /// <inheritdoc />
        public void Remove (string key) => values.Remove (key);

        /// <summary>Every key held, so a test can assert what was and was not stored.</summary>
        public IReadOnlyCollection<string> Keys => values.Keys;
    }

    /// <summary>Alert state in memory, with a switch to make saving fail.</summary>
    public sealed class InMemoryAlertStateStore : IAlertStateStore
    {
        /// <summary>The last state saved.</summary>
        public AlertStoreState? Saved { get; set; }

        /// <summary>How many times <see cref="Save"/> was called.</summary>
        public int SaveCount { get; private set; }

        /// <summary>When set, <see cref="Save"/> throws it.</summary>
        public Exception? ThrowOnSave { get; set; }

        /// <inheritdoc />
        public AlertStoreState? Load () => Saved;

        /// <inheritdoc />
        public void Save (AlertStoreState state)
        {
            SaveCount++;
            if (ThrowOnSave is not null)
                throw ThrowOnSave;
            Saved = state;
        }
    }

    /// <summary>Settings in memory.</summary>
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        /// <summary>The current settings.</summary>
        public AppSettings Current { get; set; } = new ();

        /// <summary>How many times <see cref="Save"/> was called.</summary>
        public int SaveCount { get; private set; }

        /// <inheritdoc />
        public AppSettings Load () => Current;

        /// <inheritdoc />
        public void Save (AppSettings settings)
        {
            SaveCount++;
            Current = settings;
        }
    }
}
