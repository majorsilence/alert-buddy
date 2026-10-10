using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Settings;
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
        public async Task<IReadOnlyList<VoiceOption>> ListVoicesAsync ()
        {
            if (!IsSupported)
                return [];

            return (await Speech.GetVoicesAsync ())
                .Select (v => new VoiceOption (v.Id, v.Name, v.Locale, v.Gender switch { VoiceGender.Male => VoiceSex.Male, VoiceGender.Female => VoiceSex.Female, _ => VoiceSex.Unknown }, v.RequiresNetwork))
                .ToList ();
        }

        // With no installed voice picked, the platform's own voice is lowered or raised: a lower pitch (and a slightly slower, steadier pace)
        // reads as a man's voice. A picked voice is spoken as it is.
        private static float PitchFor (VoiceType voice) => voice switch {
            VoiceType.Deep => 0.6f,
            VoiceType.Light => 1.4f,
            _ => 1f,
        };

        private static float RateFor (VoiceType voice) => voice == VoiceType.Deep ? 0.9f : 1f;

        /// <inheritdoc/>
        public void Speak (string text, VoiceType voice = VoiceType.Standard, double volume = 1, string? voiceId = null)
        {
            if (!IsSupported)
                return;

            // Fire and forget: the caller is the alert pipeline and must not wait for a sentence to finish. A failed line is not worth a crash.
            _ = Task.Run (async () => {
                try {
                    await Speech.SpeakAsync (text, new SpeechOptions {
                        Pitch = voiceId is null ? PitchFor (voice) : 1f,
                        Rate = voiceId is null ? RateFor (voice) : 1f,
                        Volume = (float)Math.Clamp (volume, 0, 1),
                        Locale = Loc.IsFrench ? "fr-FR" : null,
                        Voice = voiceId,
                    });
                } catch (Exception) {
                }
            });
        }
    }
}
