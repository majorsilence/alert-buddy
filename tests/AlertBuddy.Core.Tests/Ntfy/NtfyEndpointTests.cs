using AlertBuddy.Core.Ntfy;
using Xunit;

namespace AlertBuddy.Core.Tests.Ntfy
{
    public class NtfyEndpointTests
    {
        [Theory]
        [InlineData ("https://ntfy.example.com", "https://ntfy.example.com")]
        [InlineData ("  https://ntfy.example.com  ", "https://ntfy.example.com")]
        [InlineData ("https://ntfy.example.com/", "https://ntfy.example.com")]
        [InlineData ("https://ntfy.example.com/ntfy/", "https://ntfy.example.com/ntfy")]      // a server under a path keeps it
        [InlineData ("https://ntfy.example.com:8443", "https://ntfy.example.com:8443")]
        [InlineData ("HTTPS://NTFY.EXAMPLE.COM", "https://ntfy.example.com")]
        public void Https_IsAlwaysAllowed_AndNormalised (string address, string expected)
        {
            var check = NtfyEndpoint.Check (address);

            Assert.True (check.IsValid, check.Problem);
            Assert.False (check.Unencrypted);
            Assert.Equal (expected, check.BaseUri!.ToString ().TrimEnd ('/'));
        }

        [Theory]
        [InlineData ("http://localhost:8080")]
        [InlineData ("http://127.0.0.1")]
        [InlineData ("http://127.5.5.5")]                    // all of 127/8 is this machine
        [InlineData ("http://[::1]:8080")]
        [InlineData ("http://10.0.0.5")]
        [InlineData ("http://10.255.255.255")]
        [InlineData ("http://172.16.0.1")]                   // the bottom of 172.16.0.0/12
        [InlineData ("http://172.31.255.255")]               // the top of it
        [InlineData ("http://192.168.1.10:2586")]
        [InlineData ("http://ntfy.local")]
        [InlineData ("http://Server.LOCAL:8080")]
        public void Http_IsAllowedOnlyWhereNothingCrossesTheInternet_AndIsFlaggedUnencrypted (string address)
        {
            var check = NtfyEndpoint.Check (address);

            Assert.True (check.IsValid, check.Problem);
            Assert.True (check.Unencrypted);
        }

        [Theory]
        [InlineData ("http://ntfy.example.com")]
        [InlineData ("http://8.8.8.8")]
        [InlineData ("http://172.15.255.255")]               // just below the private block
        [InlineData ("http://172.32.0.1")]                   // just above it
        [InlineData ("http://192.169.0.1")]
        [InlineData ("http://192.167.255.255")]
        [InlineData ("http://11.0.0.1")]
        [InlineData ("http://9.255.255.255")]
        [InlineData ("http://local")]                        // not a ".local" name
        [InlineData ("http://local.example.com")]
        [InlineData ("http://notlocal")]
        [InlineData ("http://ntfy.local.example.com")]       // ".local" only counts as the last label
        [InlineData ("http://[2001:db8::1]")]
        // IPv6 addresses whose leading bytes read as private IPv4 ones: c0a8 is 192.168, 0a00 is 10.0, ac10 is 172.16. They are public IPv6.
        [InlineData ("http://[c0a8:1::1]")]
        [InlineData ("http://[0a00::1]")]
        [InlineData ("http://[ac10::1]")]
        public void Http_ToAPublicHost_IsRefused (string address)
        {
            var check = NtfyEndpoint.Check (address);

            Assert.False (check.IsValid);
            Assert.Contains ("https", check.Problem);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("   ")]
        [InlineData (null)]
        [InlineData ("ntfy.example.com")]                     // no scheme
        [InlineData ("https://")]
        [InlineData ("ftp://ntfy.example.com")]
        [InlineData ("https://user:pass@ntfy.example.com")]   // credentials in the address would end up in logs
        [InlineData ("https://user@ntfy.example.com")]
        [InlineData ("https://ntfy.example.com/?auth=abc")]
        [InlineData ("https://ntfy.example.com/#frag")]
        [InlineData ("not a url")]
        public void NotUsable_IsRefused_WithAProblemAGrownUpCanAct_On (string? address)
        {
            var check = NtfyEndpoint.Check (address);

            Assert.False (check.IsValid);
            Assert.Null (check.BaseUri);
            Assert.False (string.IsNullOrWhiteSpace (check.Problem));
        }

        [Fact]
        public void TheProblem_NeverEchoesTheCredentialsThatWereTyped ()
        {
            var check = NtfyEndpoint.Check ("https://alice:hunter2@ntfy.example.com");

            Assert.DoesNotContain ("hunter2", check.Problem);
            Assert.DoesNotContain ("alice", check.Problem);
        }

        [Theory]
        [InlineData ("home-alerts", true)]
        [InlineData ("a", true)]
        [InlineData ("Home_Alerts-2", true)]
        [InlineData ("", false)]
        [InlineData (null, false)]
        [InlineData ("has space", false)]
        [InlineData ("slash/topic", false)]
        [InlineData ("dots.not.allowed", false)]
        [InlineData ("ünïcode", false)]
        public void Topic_IsLettersDigitsUnderscoreHyphen (string? topic, bool expected)
            => Assert.Equal (expected, NtfyTopic.IsValid (topic));

        [Fact]
        public void Topic_IsAtMost64Characters ()
        {
            Assert.True (NtfyTopic.IsValid (new string ('a', 64)));
            Assert.False (NtfyTopic.IsValid (new string ('a', 65)));
        }
    }
}
