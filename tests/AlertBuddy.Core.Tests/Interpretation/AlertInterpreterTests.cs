using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Interpretation;
using AlertBuddy.Core.Ntfy;
using Xunit;

namespace AlertBuddy.Core.Tests.Interpretation
{
    public class AlertInterpreterTests
    {
        private static NtfyMessage Message (string title, string body = "", int priority = 3, string topic = "home-alerts", string id = "m1")
            => new (id, AppendixC.At (1790000000), topic, title, body, priority, []);

        private static InterpretedMessage Interpret (NtfyMessage message, InterpretationSettings? settings = null)
            => new AlertInterpreter (settings).Interpret (message);

        // ---- the fixture ----

        [Fact]
        public void TheAppendixCLifecycle_IsWarningThenAlarmThenAllClear ()
        {
            static InterpretedMessage Read (string line)
            {
                Assert.True (NtfyParser.TryParse (line, out var e));
                return Interpret (e!.Message!);
            }

            var warning = Read (AppendixC.Warning);
            var alarm = Read (AppendixC.Alarm);
            var clear = Read (AppendixC.Resolved);

            Assert.Equal ((InterpretedKind.Alert, AlertLevel.Warning, "Workshop", 41.2), (warning.Kind, warning.Level, warning.Source, warning.Temperature));
            Assert.Equal ((InterpretedKind.Alert, AlertLevel.Alarm, "Workshop", 50.6), (alarm.Kind, alarm.Level, alarm.Source, alarm.Temperature));
            Assert.Equal ((InterpretedKind.Clear, AlertLevel.Calm, "Workshop", 44.0), (clear.Kind, clear.Level, clear.Source, clear.Temperature));
        }

        // ---- priority ----

        [Theory]
        [InlineData (5, AlertLevel.Alarm)]
        [InlineData (4, AlertLevel.Warning)]
        [InlineData (3, AlertLevel.Calm)]
        [InlineData (2, AlertLevel.Calm)]
        [InlineData (1, AlertLevel.Calm)]
        public void Priority_MapsToALevel (int priority, AlertLevel expected)
            => Assert.Equal (expected, Interpret (Message ("Workshop: x", priority: priority)).Level);

        [Fact]
        public void ThePriorityThresholds_AreSettings ()
        {
            var strict = new InterpretationSettings { AlarmPriority = 4, WarningPriority = 3 };

            Assert.Equal (AlertLevel.Alarm, Interpret (Message ("Workshop: x", priority: 4), strict).Level);
            Assert.Equal (AlertLevel.Warning, Interpret (Message ("Workshop: x", priority: 3), strict).Level);
            Assert.Equal (AlertLevel.Calm, Interpret (Message ("Workshop: x", priority: 2), strict).Level);
        }

        // ---- title, emoji, source ----

        [Theory]
        [InlineData ("🔥 Workshop: too warm", "Workshop: too warm")]
        [InlineData ("⚠️ Workshop: too warm", "Workshop: too warm")]                     // the warning sign plus its variation selector
        [InlineData ("✅ 🌡️  Workshop: cool", "Workshop: cool")]                          // several, with extra spaces
        [InlineData ("👨‍👩‍👧 Sunny room: warm", "Sunny room: warm")]                         // a joined family sequence is one thing to strip
        [InlineData ("🇨🇦 Workshop: warm", "Workshop: warm")]                            // a flag is two regional indicators
        [InlineData ("👍🏽 Workshop: warm", "Workshop: warm")]                            // a skin tone modifier
        [InlineData ("  Workshop: warm", "Workshop: warm")]
        [InlineData ("Workshop: 🔥 warm", "Workshop: 🔥 warm")]                          // only the start is stripped
        [InlineData ("°C sensor: warm", "°C sensor: warm")]                              // a degree sign is a symbol, not an emoji
        [InlineData ("© Workshop: warm", "© Workshop: warm")]
        public void LeadingEmoji_AreStripped_OnlyAtTheStart_AndOnlyRealEmoji (string title, string expected)
            => Assert.Equal (expected, Interpret (Message (title)).Title);

        [Fact]
        public void StrippingEmoji_CanBeTurnedOff ()
            => Assert.Equal ("🔥 Workshop: warm", Interpret (Message ("🔥 Workshop: warm"), new InterpretationSettings { StripLeadingEmoji = false }).Title);

        [Theory]
        [InlineData ("Workshop: temperature warning", "Workshop")]
        [InlineData ("Music studio: temperature: warning", "Music studio")]        // the first separator
        [InlineData ("Workshop", "Workshop")]                                      // no separator: the whole title
        [InlineData (": leading separator", ": leading separator")]                // nothing before it: not a source
        [InlineData ("Workshop:no space", "Workshop:no space")]                    // the separator is ": " exactly
        [InlineData ("  Workshop : x", "Workshop")]                              // the source is trimmed, so the space before the colon goes
        public void Source_IsTheTextBeforeTheFirstSeparator (string title, string expected)
            => Assert.Equal (expected, Interpret (Message (title)).Source);

        [Fact]
        public void TheSeparator_IsASetting ()
            => Assert.Equal ("Workshop", Interpret (Message ("Workshop - warm"), new InterpretationSettings { SourceSeparator = " - " }).Source);

        [Fact]
        public void NoTitle_FallsBackToTheTopic_SoACardIsNeverNameless ()
        {
            var message = Interpret (new NtfyMessage ("m1", AppendixC.At (1), "home-alerts", null, "hot", 5, []));

            Assert.Equal ("home-alerts", message.Title);
            Assert.Equal ("home-alerts", message.Source);
        }

        // ---- temperature ----

        [Theory]
        [InlineData ("Workshop is at 41.2 °C", 41.2)]
        [InlineData ("Workshop is at 41.2°C", 41.2)]
        [InlineData ("41C", 41.0)]
        [InlineData ("41 C", 41.0)]
        [InlineData ("41 degrees", 41.0)]
        [InlineData ("41 degree", 41.0)]
        [InlineData ("It is 41 Celsius", 41.0)]
        [InlineData ("-5 °C outside", -5.0)]
        [InlineData ("41,2 °C", 41.2)]                                               // a decimal comma
        [InlineData ("at 41.2 and 50.6 °C", 50.6)]                                   // the first number that HAS a unit
        [InlineData ("41.2 °C then 50.6 °C", 41.2)]                                  // ...and the first of those
        public void Temperature_IsTheFirstNumberWithAUnit (string body, double expected)
            => Assert.Equal (expected, Interpret (Message ("Workshop: x", body)).Temperature);

        [Theory]
        [InlineData ("")]
        [InlineData ("no numbers here")]
        [InlineData ("room 41")]                        // a number with no unit
        [InlineData ("41 Cats")]                        // a C that starts a word is not a unit
        [InlineData ("41 F")]
        [InlineData ("v2.5C-build")]                    // the number is part of a word
        public void NoTemperature_IsNull (string body)
            => Assert.Null (Interpret (Message ("Workshop: x", body)).Temperature);

        // ---- tests ----

        [Theory]
        [InlineData ("Test")]
        [InlineData ("Workshop: TEST alarm")]
        [InlineData ("this is a test message")]
        public void ATitleContainingTest_IsATestMessage_EvenAtAlarmPriority (string title)
            => Assert.Equal (InterpretedKind.Test, Interpret (Message (title, priority: 5)).Kind);

        [Theory]
        [InlineData ("Contest winner")]
        [InlineData ("Latest reading")]
        [InlineData ("Testing")]
        public void AWordThatMerelyContainsTest_IsNot (string title)
            => Assert.NotEqual (InterpretedKind.Test, Interpret (Message (title, priority: 5)).Kind);

        // ---- kind ----

        [Fact]
        public void Kind_FollowsTheLevel ()
        {
            Assert.Equal (InterpretedKind.Alert, Interpret (Message ("Workshop: x", priority: 5)).Kind);
            Assert.Equal (InterpretedKind.Alert, Interpret (Message ("Workshop: x", priority: 4)).Kind);
            Assert.Equal (InterpretedKind.Clear, Interpret (Message ("Workshop: x", priority: 3)).Kind);
        }

        // ---- bad patterns: user input must never break the listener ----

        [Fact]
        public void AnInvalidPattern_FallsBackToTheDefault_AndIsReported ()
        {
            var interpreter = new AlertInterpreter (new InterpretationSettings { TemperaturePattern = "(unclosed", TestTitlePattern = "[" });

            Assert.Equal (2, interpreter.Problems.Count);
            var message = interpreter.Interpret (Message ("Workshop: x", "41 °C"));
            Assert.Equal (41.0, message.Temperature);   // the default temperature pattern still works
        }

        [Fact]
        public async Task APatternThatBacksTrackForever_TimesOut_InsteadOfHangingTheListener ()
        {
            // (a+)+$ against a long run of a's ending in a character that forces exhaustive backtracking. Without a match timeout this
            // runs for longer than the age of the universe; with it, the result is simply "no temperature". It runs on another thread and
            // is raced against a deadline, so if the timeout is ever lost this test fails instead of hanging the whole run.
            var interpreter = new AlertInterpreter (new InterpretationSettings { TemperaturePattern = "(a+)+$" });
            var body = new string ('a', 60) + "!";

            var work = Task.Run (() => interpreter.Interpret (Message ("Workshop: x", body)));
            var finished = await Task.WhenAny (work, Task.Delay (TimeSpan.FromSeconds (5), TestContext.Current.CancellationToken));

            Assert.Same (work, finished);
            Assert.Null ((await work).Temperature);
        }

        [Fact]
        public void Defaults_Reset ()
            => Assert.Equal (new InterpretationSettings (), InterpretationSettings.Default);
    }
}
