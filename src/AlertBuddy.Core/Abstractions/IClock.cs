namespace AlertBuddy.Core.Abstractions
{
    /// <summary>The current time, as a seam so silence windows, "time ago" text and night policy are tested without waiting.</summary>
    public interface IClock
    {
        /// <summary>The current instant. Local time is derived from it with <see cref="TimeZoneInfo.Local"/> where a view needs it.</summary>
        DateTimeOffset Now { get; }
    }

    /// <summary>An <see cref="IClock"/> over a <see cref="TimeProvider"/>, so one fake provider drives the clock and every timer.</summary>
    public sealed class TimeProviderClock : IClock
    {
        private readonly TimeProvider provider;

        public TimeProviderClock (TimeProvider? provider = null) => this.provider = provider ?? TimeProvider.System;

        public DateTimeOffset Now => provider.GetUtcNow ();
    }
}
