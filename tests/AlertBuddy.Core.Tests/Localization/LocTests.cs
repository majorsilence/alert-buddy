using System.Text.RegularExpressions;
using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Practice;
using Xunit;

namespace AlertBuddy.Core.Tests.Localization
{
    // The language is process-wide, so these run alone: nothing else may be reading words while one of them has French on.
    [CollectionDefinition ("Language", DisableParallelization = true)]
    public class LanguageCollection;

    [Collection ("Language")]
    public sealed class LocTests : IDisposable
    {
        public LocTests () => Loc.Use (AppLanguage.English);

        public void Dispose () => Loc.Use (AppLanguage.English);

        [Fact]
        public void English_ReturnsTheSentenceAsWritten ()
        {
            Assert.Equal ("Tell a grown-up now.", Loc.T ("Tell a grown-up now."));
            Assert.Equal ("Step 2 of 5", Loc.F ("Step {0} of {1}", 2, 5));
        }

        [Fact]
        public void French_LooksUpTheSentence_AndFillsItsValues ()
        {
            Loc.Use (AppLanguage.French);

            Assert.Equal ("Préviens un adulte tout de suite.", Loc.T ("Tell a grown-up now."));
            Assert.Equal ("Étape 2 sur 5", Loc.F ("Step {0} of {1}", 2, 5));
        }

        [Fact]
        public void ASentenceWithNoFrench_ShowsInEnglish_NotBlank ()
        {
            Loc.Use (AppLanguage.French);

            Assert.Equal ("Nobody has translated this yet.", Loc.T ("Nobody has translated this yet."));
        }

        [Theory]
        [InlineData ("fr", true)]
        [InlineData ("fr-CA", true)]
        [InlineData ("FR-fr", true)]
        [InlineData ("en-US", false)]
        [InlineData ("de-DE", false)]
        [InlineData ("", false)]
        [InlineData (null, false)]
        public void WhenLeftOnTheDeviceLanguage_OnlyAFrenchDeviceGetsFrench (string? device, bool french)
        {
            Loc.Use (AppLanguage.System, device);

            Assert.Equal (french, Loc.IsFrench);
        }

        [Fact]
        public void ChoosingALanguage_OverridesTheDevice ()
        {
            Loc.Use (AppLanguage.English, "fr-FR");
            Assert.False (Loc.IsFrench);

            Loc.Use (AppLanguage.French, "en-US");
            Assert.True (Loc.IsFrench);
        }

        [Fact]
        public void ChangingTheLanguage_Announces_ButChoosingTheSameOneDoesNot ()
        {
            var changes = 0;
            void Count () => changes++;
            Loc.Changed += Count;
            try {
                Loc.Use (AppLanguage.French);
                Loc.Use (AppLanguage.French);
                Loc.Use (AppLanguage.English);
            } finally {
                Loc.Changed -= Count;
            }

            Assert.Equal (2, changes);
        }

        [Fact]
        public void EverySentenceInTheSource_HasItsFrench ()
        {
            var missing = new List<string> ();
            foreach (var key in SentencesInTheSource ()) {
                if (!FrenchTable.Words.ContainsKey (key))
                    missing.Add (key);
            }

            Assert.True (missing.Count == 0, "No French for:\n" + string.Join ("\n", missing));
        }

        [Fact]
        public void EveryFrenchSentence_KeepsTheValuesOfItsEnglish ()
        {
            var placeholder = new Regex (@"\{\d+\}");
            foreach (var (english, french) in FrenchTable.Words) {
                var wanted = placeholder.Matches (english).Select (m => m.Value).Order ().ToList ();
                var got = placeholder.Matches (french).Select (m => m.Value).Order ().ToList ();
                Assert.True (wanted.SequenceEqual (got), $"\"{english}\": the French has {string.Join (",", got)}, not {string.Join (",", wanted)}");
            }
        }

        [Fact]
        public void ThePracticeScript_IsInFrench_AndStillReadsAsAWarningAnAlarmAndAnAllClear ()
        {
            Loc.Use (AppLanguage.French);

            var script = PracticeAlertSource.Script (DateTimeOffset.UnixEpoch);

            Assert.Equal ("Salle d’essai", PracticeAlertSource.Source);
            Assert.Equal ("Un avertissement arrive.", script[0].Caption);
            Assert.StartsWith ("Salle d’essai: ", script[1].Message.Title);      // the source still ends at the first ": ", as the reader splits it
            Assert.Equal ("Salle d’essai est à 46 °C", script[1].Message.Message);
        }

        // Every sentence passed to Loc.T or Loc.F as a literal, in every source file of the app.
        internal static IEnumerable<string> SentencesInTheSource ()
        {
            var root = AppContext.BaseDirectory;
            while (root is not null && !Directory.Exists (Path.Combine (root, "src")))
                root = Path.GetDirectoryName (root);
            Assert.NotNull (root);

            var call = new Regex (@"Loc\.[TF] \(""((?:[^""\\]|\\.)*)""");

            // Sentences that reach Loc.T through a list rather than a call of their own: a dropdown's options, the Practice sound buttons.
            var choice = new Regex (@"Choice \(nameof \([^)]*\)((?:, ""[^""]*"")+)\)");
            var optionList = new Regex (@"new\[\] \{ (""[^""]*""(?:, ""[^""]*"")*) \}");
            var soundButton = new Regex (@"\(PracticeSound\.\w+, ""([^""]*)"", ""([^""]*)""\)");
            var quoted = new Regex (@"""([^""]*)""");
            foreach (var file in Directory.EnumerateFiles (Path.Combine (root!, "src"), "*.cs", SearchOption.AllDirectories)) {
                if (file.Contains ($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains ($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;
                if (file.EndsWith ("Loc.cs") || file.EndsWith ("FrenchTable.cs"))
                    continue;

                var text = File.ReadAllText (file);
                foreach (Match m in choice.Matches (text))
                    foreach (Match q in quoted.Matches (m.Groups[1].Value))
                        yield return q.Groups[1].Value;
                if (file.EndsWith ("FirstRunView.cs"))
                    foreach (Match m in optionList.Matches (text))
                        foreach (Match q in quoted.Matches (m.Groups[1].Value))
                            yield return q.Groups[1].Value;
                foreach (Match m in soundButton.Matches (text)) {
                    yield return m.Groups[1].Value;
                    yield return m.Groups[2].Value;
                }

                foreach (Match m in call.Matches (text))
                    yield return m.Groups[1].Value.Replace ("\\\"", "\"");   // as the runtime sees it: \" in the source is "
            }
        }
    }
}
