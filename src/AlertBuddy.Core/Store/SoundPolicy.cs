namespace AlertBuddy.Core.Store
{
    /// <summary>
    /// When the house sleeps, warnings and all clears are quiet. Default 20:00 to 07:00 (PLAN.md section 4.3). Alarms are never muted by
    /// quiet hours: that is what an alarm is for.
    /// </summary>
    public sealed record NightPolicy
    {
        /// <summary>Whether quiet hours apply at all.</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>When quiet hours begin, in local time.</summary>
        public TimeOnly Start { get; init; } = new (20, 0);

        /// <summary>When quiet hours end, in local time.</summary>
        public TimeOnly End { get; init; } = new (7, 0);

        /// <summary>Whether a local time falls in quiet hours. Handles the usual case where they run past midnight.</summary>
        public bool Covers (TimeOnly localTime)
            => Start <= End
                ? localTime >= Start && localTime < End
                : localTime >= Start || localTime < End;
    }

    /// <summary>Applies the night policy to the sound a change calls for.</summary>
    public static class SoundPolicy
    {
        /// <summary>The sound to actually play at <paramref name="now"/>, in the given time zone.</summary>
        public static AlertSound Apply (AlertSound sound, NightPolicy night, DateTimeOffset now, TimeZoneInfo zone)
        {
            ArgumentNullException.ThrowIfNull (night);
            ArgumentNullException.ThrowIfNull (zone);

            // The alarm and the test cheer are never muted. A warning's two notes and an all clear's chime are exactly what should
            // not wake a child at 3 in the morning; the notification still appears, only the sound is dropped.
            if (sound is not (AlertSound.Warning or AlertSound.AllClear) || !night.Enabled)
                return sound;

            var local = TimeOnly.FromDateTime (TimeZoneInfo.ConvertTime (now, zone).DateTime);
            return night.Covers (local) ? AlertSound.None : sound;
        }
    }
}
