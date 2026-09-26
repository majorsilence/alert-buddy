namespace AlertBuddy.TestSupport
{
    /// <summary>
    /// A <see cref="TimeProvider"/> the test drives by hand, and can look inside. Unlike a general-purpose fake it exposes the timers that
    /// are waiting, so a test can assert "the loop scheduled a 4 second backoff" as a fact and then advance exactly that far, with no real
    /// sleeping and no guessing whether the code under test has reached its delay yet.
    /// </summary>
    public sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object gate = new ();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now;
        private long version;

        /// <summary>Starts at the given instant.</summary>
        public ManualTimeProvider (DateTimeOffset start) => now = start;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow ()
        {
            lock (gate)
                return now;
        }

        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        /// <inheritdoc />
        public override long GetTimestamp ()
        {
            lock (gate)
                return now.UtcTicks;
        }

        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        /// <inheritdoc />
        public override ITimer CreateTimer (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer (this, callback, state);
            lock (gate) {
                timers.Add (timer);
                version++;
            }

            timer.Change (dueTime, period);
            return timer;
        }

        /// <summary>How long from now each waiting timer is due, soonest first. A timer that is idle or disposed is not listed.</summary>
        public IReadOnlyList<TimeSpan> PendingDelays {
            get {
                lock (gate)
                    return timers.Where (t => t.Due is not null).Select (t => t.Due!.Value - now).Order ().ToList ();
            }
        }

        /// <summary>Changes whenever a timer is created, changed or disposed. A test waits for it to hold still to know the code under test has settled.</summary>
        public long Version {
            get {
                lock (gate)
                    return version;
            }
        }

        /// <summary>Moves time forward, firing every timer that comes due on the way, in order, each at its own due instant.</summary>
        public void Advance (TimeSpan by)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (by, TimeSpan.Zero);

            DateTimeOffset target;
            lock (gate)
                target = now + by;

            while (true) {
                ManualTimer? next;
                lock (gate) {
                    next = timers.Where (t => t.Due is not null && t.Due <= target).MinBy (t => t.Due);
                    if (next is null) {
                        now = target;
                        return;
                    }

                    if (next.Due > now)
                        now = next.Due!.Value;

                    next.Due = next.Period > TimeSpan.Zero ? now + next.Period : null;
                    version++;
                }

                // Outside the lock: the callback may create or change timers, and often runs the code under test inline.
                next.Fire ();
            }
        }

        private void Remove (ManualTimer timer)
        {
            lock (gate) {
                timers.Remove (timer);
                version++;
            }
        }

        private void Schedule (ManualTimer timer, TimeSpan dueTime, TimeSpan period)
        {
            lock (gate) {
                timer.Due = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
                timer.Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
                version++;
            }
        }

        private sealed class ManualTimer (ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;

            public DateTimeOffset? Due { get; set; }

            public TimeSpan Period { get; set; }

            public void Fire ()
            {
                if (!disposed)
                    callback (state);
            }

            public bool Change (TimeSpan dueTime, TimeSpan period)
            {
                if (disposed)
                    return false;

                owner.Schedule (this, dueTime, period);
                return true;
            }

            public void Dispose ()
            {
                if (disposed)
                    return;

                disposed = true;
                Due = null;
                owner.Remove (this);
            }

            public ValueTask DisposeAsync ()
            {
                Dispose ();
                return ValueTask.CompletedTask;
            }
        }
    }
}
