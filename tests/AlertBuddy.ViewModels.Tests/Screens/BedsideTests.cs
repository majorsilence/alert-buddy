using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class BedsideTests
    {
        [Fact]
        public async Task BedsideMode_KeepsTheScreenAwake_UntilItIsSwitchedOff ()
        {
            await using var rig = new AppRig ();
            Assert.False (rig.KeepAwake.Enabled);

            rig.Main.ToggleBedsideCommand.Execute (null);
            Assert.True (rig.Main.IsBedside);
            Assert.True (rig.KeepAwake.Enabled);

            rig.Main.ToggleBedsideCommand.Execute (null);
            Assert.False (rig.Main.IsBedside);
            Assert.False (rig.KeepAwake.Enabled);
        }

        [Fact]
        public async Task LeavingHome_WhileInBedsideMode_LetsTheScreenSleepAgain ()
        {
            var rig = new AppRig ();
            rig.Main.ToggleBedsideCommand.Execute (null);
            Assert.True (rig.KeepAwake.Enabled);

            await rig.DisposeAsync ();

            Assert.False (rig.KeepAwake.Enabled);
        }
    }
}
