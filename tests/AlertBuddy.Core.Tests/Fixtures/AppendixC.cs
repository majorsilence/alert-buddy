namespace AlertBuddy.Core.Tests
{
    /// <summary>The fictional fixture of PLAN.md Appendix C: a room called Workshop warms, alarms, and is cool again. Invented data only.</summary>
    internal static class AppendixC
    {
        public const string Open = """{"id":"aB3dEf","time":1790000000,"event":"open","topic":"home-alerts"}""";
        public const string Keepalive = """{"id":"aB3dEg","time":1790000045,"event":"keepalive","topic":"home-alerts"}""";
        public const string Warning = """{"id":"aB3dEh","time":1790000100,"event":"message","topic":"home-alerts","priority":4,"title":"Workshop: temperature warning","message":"Workshop is at 41.2 °C"}""";
        public const string Alarm = """{"id":"aB3dEi","time":1790000400,"event":"message","topic":"home-alerts","priority":5,"title":"Workshop: temperature alarm","message":"Workshop is at 50.6 °C"}""";
        public const string Resolved = """{"id":"aB3dEj","time":1790000900,"event":"message","topic":"home-alerts","title":"Workshop: temperature alarm (resolved)","message":"Workshop is at 44.0 °C"}""";

        /// <summary>Unix seconds of the first message, as a DateTimeOffset, for building expectations.</summary>
        public static DateTimeOffset At (long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds (unixSeconds);
    }
}
