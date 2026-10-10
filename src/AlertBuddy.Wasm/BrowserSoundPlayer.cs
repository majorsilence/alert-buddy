using System.Runtime.InteropServices.JavaScript;
using AlertBuddy.Shared.Platform;
using AlertBuddy.ViewModels.Services;

namespace AlertBuddy.Wasm
{
    /// <summary>
    /// The cues of <c>tools/SoundSynth</c> in the browser, through <c>HTMLAudioElement</c> (wwwroot/sound.js). Unlike the desktop it has a
    /// volume, so Practice is quiet here. The files are served from <c>sounds/</c> beside the page.
    /// </summary>
    internal sealed partial class BrowserSoundPlayer : ISoundPlayer
    {
        /// <inheritdoc/>
        public bool IsSupported => true;

        /// <inheritdoc/>
        public void Play (Cue cue, double volume = 1) => JsPlay (UrlFor (cue), volume);

        /// <inheritdoc/>
        public void StartLoop (Cue cue, double volume = 1) => JsStartLoop (UrlFor (cue), volume);

        /// <inheritdoc/>
        public void StopLoop () => JsStopLoop ();

        private static string UrlFor (Cue cue) => $"sounds/{SoundFiles.FileName (cue)}";

        [JSImport ("globalThis.alertBuddySound.play")]
        private static partial void JsPlay (string url, double volume);

        [JSImport ("globalThis.alertBuddySound.startLoop")]
        private static partial void JsStartLoop (string url, double volume);

        [JSImport ("globalThis.alertBuddySound.stopLoop")]
        private static partial void JsStopLoop ();
    }
}
