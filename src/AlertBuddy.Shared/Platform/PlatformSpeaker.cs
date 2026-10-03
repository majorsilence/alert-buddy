using AlertBuddy.ViewModels.Services;
using Majorsilence.Forms.Essentials;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// Reads a line aloud with the platform's own voice: Android <c>TextToSpeech</c>, the iOS speech synthesiser, and <c>say</c>, <c>espeak</c>
    /// or Windows speech on a desktop (<c>Majorsilence.Forms.Essentials.Speech</c>). Where there is no voice, <see cref="IsSupported"/> is
    /// false and Settings does not offer it.
    /// </summary>
    public sealed class PlatformSpeaker : ISpeaker
    {
        /// <inheritdoc/>
        public bool IsSupported => Speech.IsSupported;

        /// <inheritdoc/>
        public void Speak (string text)
        {
            if (!IsSupported)
                return;

            // Fire and forget: the caller is the alert pipeline and must not wait for a sentence to finish. A failed line is not worth a crash.
            _ = Task.Run (async () => {
                try {
                    await Speech.SpeakAsync (text);
                } catch (Exception) {
                }
            });
        }
    }
}
