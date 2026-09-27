using AlertBuddy.Core.Ntfy;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class NtfyCredentialsTests
    {
        [Fact]
        public void Basic_BuildsTheStandardHeader ()
            => Assert.Equal ("Basic YWxpY2U6czNjcmV0", NtfyCredentials.Basic ("alice", "s3cret").AuthorizationHeaderValue);   // base64 of alice:s3cret

        [Fact]
        public void Basic_EncodesNonAsciiAsUtf8 ()
            => Assert.Equal ("Basic " + Convert.ToBase64String ("zoë:pässword"u8.ToArray ()), NtfyCredentials.Basic ("zoë", "pässword").AuthorizationHeaderValue);

        [Fact]
        public void Bearer_BuildsTheStandardHeader ()
            => Assert.Equal ("Bearer tk_example", NtfyCredentials.Bearer ("tk_example").AuthorizationHeaderValue);

        [Fact]
        public void None_HasNoHeader ()
            => Assert.Null (NtfyCredentials.None.AuthorizationHeaderValue);

        [Fact]
        public void ToString_NeverShowsTheSecret_InAnyFormatting ()
        {
            var basic = NtfyCredentials.Basic ("alice", "hunter2");
            var bearer = NtfyCredentials.Bearer ("tk_verysecrettoken");

            foreach (var text in new[] { basic.ToString (), $"{basic}", string.Format ("{0}", basic), bearer.ToString (), $"{bearer}" }) {
                Assert.DoesNotContain ("hunter2", text);
                Assert.DoesNotContain ("alice", text);
                Assert.DoesNotContain ("verysecret", text);
                Assert.DoesNotContain ("YWxpY2U", text);        // nor the base64 of it
                Assert.Contains ("redacted", text);
            }
        }

        [Fact]
        public void EmptyCredentials_AreRefused ()
        {
            Assert.Throws<ArgumentException> (() => NtfyCredentials.Basic ("", "pw"));
            Assert.Throws<ArgumentException> (() => NtfyCredentials.Bearer (""));
        }
    }

    public class BackoffTests
    {
        [Fact]
        public void TheSchedule_IsOneTwoFourEightSixteenThirtyTwoThenSixty ()
        {
            var seconds = Enumerable.Range (0, 10).Select (i => Backoff.Delay (i, 0.5).TotalSeconds).ToArray ();

            Assert.Equal ([1, 2, 4, 8, 16, 32, 60, 60, 60, 60], seconds);
        }

        [Theory]
        [InlineData (0, 0.8)]
        [InlineData (1, 1.2)]
        public void Jitter_IsPlusOrMinusTwentyPercent (double jitter, double factor)
        {
            Assert.Equal (16 * factor, Backoff.Delay (4, jitter).TotalSeconds, precision: 6);
            Assert.Equal (60 * factor, Backoff.Delay (99, jitter).TotalSeconds, precision: 6);
        }

        [Fact]
        public void EveryDelay_StaysWithinTwentyPercentOfItsBase ()
        {
            var bases = new[] { 1, 2, 4, 8, 16, 32, 60 };
            for (var attempt = 0; attempt < bases.Length; attempt++)
                for (var j = 0.0; j <= 1.0; j += 0.05) {
                    var s = Backoff.Delay (attempt, j).TotalSeconds;
                    Assert.InRange (s, bases[attempt] * 0.8 - 1e-9, bases[attempt] * 1.2 + 1e-9);
                }
        }

        [Fact]
        public void OutOfRangeInputs_AreClampedRatherThanThrown ()
        {
            Assert.Equal (Backoff.Delay (0, 0.5), Backoff.Delay (-5, 0.5));
            Assert.Equal (Backoff.Delay (0, 0.0), Backoff.Delay (0, -3.0));
            Assert.Equal (Backoff.Delay (0, 1.0), Backoff.Delay (0, 42.0));
        }

        [Fact]
        public void TheConstants_AreThePlansNumbers ()
        {
            Assert.Equal (TimeSpan.FromSeconds (110), Backoff.Watchdog);
            Assert.Equal (TimeSpan.FromSeconds (30), Backoff.HealthyAfter);
            Assert.Equal (TimeSpan.FromMinutes (5), Backoff.SlowRetry);
        }
    }
}
