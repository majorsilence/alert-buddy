using AlertBuddy.Core.Ntfy;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class NtfyParserTests
    {
        private static NtfyEvent Parse (string line)
        {
            Assert.True (NtfyParser.TryParse (line, out var result), "the line should parse");
            return Assert.IsType<NtfyEvent> (result);
        }

        [Fact]
        public void OpenAndKeepalive_AreRecognisedWithoutAMessage ()
        {
            Assert.Equal (NtfyEventKind.Open, Parse (AppendixC.Open).Kind);
            Assert.Null (Parse (AppendixC.Open).Message);
            Assert.Equal (NtfyEventKind.Keepalive, Parse (AppendixC.Keepalive).Kind);
        }

        [Fact]
        public void Message_CarriesEveryFieldTheAppReads ()
        {
            var message = Assert.IsType<NtfyMessage> (Parse (AppendixC.Warning).Message);

            Assert.Equal ("aB3dEh", message.Id);
            Assert.Equal (AppendixC.At (1790000100), message.Time);
            Assert.Equal ("home-alerts", message.Topic);
            Assert.Equal ("Workshop: temperature warning", message.Title);
            Assert.Equal ("Workshop is at 41.2 °C", message.Message);
            Assert.Equal (4, message.Priority);
            Assert.Empty (message.Tags);
        }

        [Fact]
        public void MissingPriority_MeansTheDefaultOfThree ()
        {
            // ntfy omits the field when it is 3; reading that as 0 would make every ordinary message look calmer than a real "low".
            Assert.Equal (3, Assert.IsType<NtfyMessage> (Parse (AppendixC.Resolved).Message).Priority);
        }

        [Theory]
        [InlineData (0, 1)]
        [InlineData (-4, 1)]
        [InlineData (9, 5)]
        [InlineData (5, 5)]
        public void OutOfRangePriority_IsClampedNotTrusted (int wire, int expected)
        {
            var line = $$"""{"id":"x1","time":1,"event":"message","topic":"t","priority":{{wire}},"message":"m"}""";

            Assert.Equal (expected, Assert.IsType<NtfyMessage> (Parse (line).Message).Priority);
        }

        [Fact]
        public void Tags_AreRead ()
        {
            var line = """{"id":"x1","time":1,"event":"message","topic":"t","tags":["warning","house"],"message":"m"}""";

            Assert.Equal (["warning", "house"], Assert.IsType<NtfyMessage> (Parse (line).Message).Tags);
        }

        [Fact]
        public void UnknownEvents_AreOtherAndUnknownFieldsAreIgnored ()
        {
            Assert.Equal (NtfyEventKind.Other, Parse ("""{"id":"x","time":1,"event":"message_delete","topic":"t"}""").Kind);

            var withExtras = """{"id":"x1","time":1,"event":"message","topic":"t","message":"m","expires":99,"attachment":{"name":"a"},"actions":[]}""";
            Assert.Equal ("m", Assert.IsType<NtfyMessage> (Parse (withExtras).Message).Message);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("   ")]
        [InlineData ("not json")]
        [InlineData ("{\"id\":\"x\",\"time\":")]               // a line the connection dropped in the middle of
        [InlineData ("{}")]                                    // valid JSON with no event
        [InlineData ("[1,2,3]")]
        [InlineData ("""{"time":1,"event":"message","topic":"t","message":"m"}""")]   // a message with no id cannot be resumed from
        public void NotAnEvent_IsSkippedNeverThrown (string line)
        {
            Assert.False (NtfyParser.TryParse (line, out var result));
            Assert.Null (result);
        }

        [Fact]
        public void Unicode_SurvivesIncludingEmojiInTheTitle ()
        {
            var line = "{\"id\":\"x1\",\"time\":1,\"event\":\"message\",\"topic\":\"t\",\"title\":\"\\ud83d\\udd25 Sunny room: warm\",\"message\":\"Sunny room is at 30 \\u00b0C\"}";

            var message = Assert.IsType<NtfyMessage> (Parse (line).Message);

            Assert.Equal ("🔥 Sunny room: warm", message.Title);
            Assert.Equal ("Sunny room is at 30 °C", message.Message);
        }
    }
}
