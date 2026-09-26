namespace AlertBuddy.Core.Alerts
{
    /// <summary>How urgent a message is. Priority 5 is an alarm, 4 a warning, anything lower is calm (PLAN.md section 2).</summary>
    public enum AlertLevel
    {
        /// <summary>Nothing to do. Also how "all clear" arrives.</summary>
        Calm = 0,

        /// <summary>Something is getting warm. Heads-up, no siren.</summary>
        Warning = 1,

        /// <summary>Tell a grown-up now.</summary>
        Alarm = 2,
    }

    /// <summary>Where an alert has got to. Acknowledging silences sound only; the alert stays until its source resolves.</summary>
    public enum AlertStatus
    {
        /// <summary>Raised and nobody has responded.</summary>
        Active = 0,

        /// <summary>The child said "I told a grown-up".</summary>
        Acknowledged = 1,

        /// <summary>A grown-up said "Got it".</summary>
        Handled = 2,

        /// <summary>The source sent an all clear.</summary>
        Resolved = 3,
    }
}
