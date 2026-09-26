using AlertBuddy.Core.Ntfy;

namespace AlertBuddy.Core.Practice
{
    /// <summary>One beat of the pretend alert.</summary>
    /// <param name="Number">1 to 3. The steps are numbered because they are a sequence (PLAN.md section 8.4).</param>
    /// <param name="After">How long after the start it happens.</param>
    /// <param name="Caption">What the screen says it is.</param>
    /// <param name="Message">The pretend ntfy message that carries it.</param>
    public sealed record PracticeStep (int Number, TimeSpan After, string Caption, NtfyMessage Message);

    /// <summary>
    /// The scripted alerts of Practice mode (PLAN.md section 4.4): a warning, an alarm, an all clear, about 20 seconds, with a fictional
    /// source. They go through the same interpreter and store as real ones, so what a child practises is exactly what will happen. The same
    /// script drives the development loop and CI, so the app needs no real server to run.
    /// </summary>
    public static class PracticeAlertSource
    {
        /// <summary>The invented room every practice message is about.</summary>
        public const string Source = "Practice room";

        /// <summary>How long a run takes from the first step to the end.</summary>
        public static readonly TimeSpan Length = TimeSpan.FromSeconds (20);

        /// <summary>Builds the script. Ids are unique to <paramref name="now"/>, so a second run never looks like a duplicate of the first.</summary>
        public static IReadOnlyList<PracticeStep> Script (DateTimeOffset now)
        {
            var run = now.ToUnixTimeMilliseconds ();

            NtfyMessage Message (int n, int priority, string title, string body)
                => new ($"practice-{run}-{n}", now, "practice", title, body, priority, ["practice"]);

            return [
                new PracticeStep (1, TimeSpan.Zero, "A warning arrives.",
                    Message (1, 4, $"{Source}: temperature warning", $"{Source} is at 38 °C")),
                new PracticeStep (2, TimeSpan.FromSeconds (7), "An alarm arrives. Tell a grown-up.",
                    Message (2, 5, $"{Source}: temperature alarm", $"{Source} is at 46 °C")),
                new PracticeStep (3, TimeSpan.FromSeconds (14), "All clear.",
                    Message (3, 3, $"{Source}: temperature back to normal", $"{Source} is at 24 °C")),
            ];
        }
    }
}
