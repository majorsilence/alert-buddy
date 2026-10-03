using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Controls;
using AlertBuddy.Shared.Platform;
using AlertBuddy.Shared.Theme;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public class MotionTests
    {
        [Theory]
        [InlineData (true, false, true)]    // the grown-up chose Calmer; the system does not ask for it
        [InlineData (false, true, false)]   // the grown-up chose Full; that beats the system
        [InlineData (null, true, true)]     // follow the device
        [InlineData (null, false, false)]
        public void AnExplicitChoiceWins_OtherwiseTheSystemDecides (bool? setting, bool system, bool expected)
            => Assert.Equal (expected, AlertMotion.Resolve (setting, system));

        [Fact]
        public void TheBeacon_FollowsTheSavedSetting_AndSavingChangesIt ()
        {
            var settings = new SettingsService (new MemorySettingsStore (new AppSettings { ReduceMotion = true }));
            AlertMotion.Follow (settings);
            using var beacon = new BeaconBuddy ();

            Assert.True (beacon.ReduceMotion);

            settings.Save (settings.Current with { ReduceMotion = false });
            Assert.False (beacon.ReduceMotion);

            beacon.ReduceMotion = true;     // an explicit setting on the control beats the global one
            Assert.True (beacon.ReduceMotion);
        }
    }
}
