using AlertBuddy.Core.Security;
using Xunit;

namespace AlertBuddy.Core.Tests.Security
{
    public class PinHasherTests
    {
        [Theory]
        [InlineData ("0000", true)]
        [InlineData ("1234", true)]
        [InlineData ("9", false)]
        [InlineData ("123", false)]
        [InlineData ("12345", false)]
        [InlineData ("12a4", false)]
        [InlineData ("١٢٣٤", false)]      // Arabic-Indic digits are digits to char.IsDigit but not to a PIN pad
        [InlineData (" 123", false)]
        [InlineData ("", false)]
        [InlineData (null, false)]
        public void APin_IsExactlyFourAsciiDigits (string? pin, bool expected)
            => Assert.Equal (expected, PinHasher.IsValidPin (pin));

        [Fact]
        public void TheRightPin_Verifies_AndAWrongOneDoesNot ()
        {
            var credential = PinHasher.Create ("4821");

            Assert.True (PinHasher.Verify ("4821", credential));
            Assert.False (PinHasher.Verify ("4822", credential));
            Assert.False (PinHasher.Verify ("0000", credential));
        }

        [Fact]
        public void TheStoredForm_HoldsNoDigits_AndEachHashHasItsOwnSalt ()
        {
            var a = PinHasher.Create ("4821");
            var b = PinHasher.Create ("4821");

            Assert.NotEqual (a.Salt, b.Salt);
            Assert.NotEqual (a.Hash, b.Hash);          // the same PIN does not produce the same stored value twice
            Assert.True (PinHasher.Verify ("4821", a));
            Assert.True (PinHasher.Verify ("4821", b));
            Assert.Equal (32, Convert.FromBase64String (a.Hash).Length);
        }

        [Fact]
        public void APinThatIsNotFourDigits_CannotBeCreated ()
            => Assert.Throws<ArgumentException> (() => PinHasher.Create ("12"));

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("12")]
        [InlineData ("abcd")]
        public void Verify_TreatsABadPinAsAMismatch_NeverAnException (string? pin)
            => Assert.False (PinHasher.Verify (pin, PinHasher.Create ("4821")));

        [Fact]
        public void Verify_ToleratesADamagedOrMissingCredential ()
        {
            Assert.False (PinHasher.Verify ("4821", null));
            Assert.False (PinHasher.Verify ("4821", new PinCredential ("not base64 !!", "also not")));
            Assert.False (PinHasher.Verify ("4821", new PinCredential ("", "")));
        }
    }
}
