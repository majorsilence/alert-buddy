using AlertBuddy.Shared.Views;
using AlertBuddy.ViewModels.Copy;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public class LayoutTests
    {
        [Fact]
        public void TheHonestBanner_GetsRoomForEveryLineAtPhoneWidth ()
        {
            // On a 360-wide phone the banner is 312 wide, and this sentence needs three lines; at a fixed 52 high the third was cut off
            // ("... A grown-up can fix this in settings.") on the Android emulator.
            var text = Words.CannotListenInBackground ("Pip", "Notifications are turned off.");

            var height = FormColumn.ParagraphHeight (text, 312);

            Assert.True (height >= 3 * 26, $"{height} high is not enough for {text.Length} characters at 312 wide");
        }

        [Fact]
        public void ShortText_StillGetsOneLine ()
            => Assert.Equal (26, FormColumn.ParagraphHeight ("Hi", 312));
    }
}
