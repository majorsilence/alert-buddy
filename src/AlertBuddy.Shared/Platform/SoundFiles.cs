using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Which generated file (<c>tools/SoundSynth</c>, in <c>src/AlertBuddy.Android/Assets/sounds</c>) each cue plays. The one place the names
    /// live, so Android, the desktop and the browser agree.
    /// </summary>
    public static class SoundFiles
    {
        /// <summary>The file name without its extension.</summary>
        public static string For (Cue cue) => cue switch {
            Cue.Boop => "boop",
            Cue.Warning => "warning",
            Cue.Alarm => "alarm",
            Cue.AllClear => "allclear",
            Cue.Cheer => "cheer",
            Cue.Code3 => "code3",
            Cue.MarchTime => "marchtime",
            Cue.Continuous => "continuous",
            Cue.VoiceEvacuation => "voice",
            _ => "practice",
        };

        /// <summary>The file name with its extension.</summary>
        public static string FileName (Cue cue) => For (cue) + ".wav";
    }
}
