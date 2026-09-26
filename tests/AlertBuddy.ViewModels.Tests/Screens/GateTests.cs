using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using AlertBuddy.ViewModels.Services;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class GateLockTests
    {
        [Fact]
        public void FiveWrongPins_CloseTheGateFor30Seconds ()
        {
            var clock = new AlertBuddy.TestSupport.TestClock ();
            var gate = new GateLock (clock);

            for (var i = 0; i < 4; i++)
                Assert.False (gate.RegisterFailure ());
            Assert.False (gate.IsLocked);
            Assert.True (gate.RegisterFailure ());

            Assert.True (gate.IsLocked);
            clock.Advance (TimeSpan.FromSeconds (29));
            Assert.True (gate.IsLocked);
            clock.Advance (TimeSpan.FromSeconds (1));
            Assert.False (gate.IsLocked);
        }

        [Fact]
        public void TheRightPin_ClearsTheCount ()
        {
            var gate = new GateLock (new AlertBuddy.TestSupport.TestClock ());
            for (var i = 0; i < 4; i++)
                gate.RegisterFailure ();

            gate.RegisterSuccess ();

            Assert.False (gate.RegisterFailure ());     // the count started again from zero
        }

        [Fact]
        public void AfterALockout_TheCountStartsAgain ()
        {
            var clock = new AlertBuddy.TestSupport.TestClock ();
            var gate = new GateLock (clock);
            for (var i = 0; i < 5; i++)
                gate.RegisterFailure ();
            clock.Advance (TimeSpan.FromSeconds (31));

            for (var i = 0; i < 4; i++)
                Assert.False (gate.RegisterFailure ());
            Assert.True (gate.RegisterFailure ());
        }
    }

    public class GateTests
    {
        private static AppSettings WithPin () => new () { ServerUrl = "https://ntfy.example.com", Topic = "t", FirstRunComplete = true, Pin = PinHasher.Create ("4821") };

        private static GateViewModel OpenGate (AppRig rig)
        {
            rig.Main.OpenSettingsCommand.Execute (null);
            return rig.Current<GateViewModel> ();
        }

        private static void Enter (GateViewModel gate, string digits)
        {
            foreach (var digit in digits)
                gate.PressDigitCommand.Execute (digit.ToString ());
        }

        [Fact]
        public async Task ItStartsWithAHold_AndPinKeysDoNothingYet ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);

            Enter (gate, "4821");

            Assert.Equal ((GatePhase.Hold, 0, "Press and hold. This part is for grown-ups."), (gate.Phase, gate.EnteredCount, gate.Message));
            Assert.IsType<GateViewModel> (rig.Navigator.Current);
        }

        [Fact]
        public async Task TheHold_LeadsToThePinPad ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);

            gate.HoldCompletedCommand.Execute (null);

            Assert.Equal ((GatePhase.Pin, "Enter the PIN."), (gate.Phase, gate.Message));
        }

        [Fact]
        public async Task TheRightPin_ClosesTheGate_ThenOpensSettings_InThatOrder ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);

            Enter (gate, "4821");

            Assert.IsType<SettingsViewModel> (rig.Navigator.Current);
            rig.Navigator.GoBack ();
            Assert.Same (rig.Main, rig.Navigator.Current);          // the gate is gone: settings opened over Home, not over the gate
        }

        [Fact]
        public async Task AWrongPin_ClearsTheDots_AndSaysSo ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);

            Enter (gate, "1111");

            Assert.Equal ((GatePhase.Pin, 0, "That PIN didn't match. Try again."), (gate.Phase, gate.EnteredCount, gate.Message));
            Assert.IsType<GateViewModel> (rig.Navigator.Current);
        }

        [Fact]
        public async Task TheDots_CountTheDigits_AndBackspaceRemovesOne ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);
            var log = new PropertyLog (gate);

            Enter (gate, "48");
            Assert.Equal (2, gate.EnteredCount);
            gate.BackspaceCommand.Execute (null);

            Assert.Equal (1, gate.EnteredCount);
            Assert.Contains (nameof (GateViewModel.EnteredCount), log.Names);
        }

        [Theory]
        [InlineData ("a")]
        [InlineData ("12")]
        [InlineData ("")]
        [InlineData (" ")]
        [InlineData (null)]
        [InlineData ("٣")]
        public async Task AnythingButASingleDigit_IsIgnored (string? key)
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);

            gate.PressDigitCommand.Execute (key);

            Assert.Equal (0, gate.EnteredCount);
        }

        [Fact]
        public async Task FiveWrongPins_LockTheGate_AndEvenTheRightPinIsRefused ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);

            for (var i = 0; i < 5; i++)
                Enter (gate, "0000");

            Assert.Equal ((GatePhase.Locked, "Too many tries. Wait a little, then try again."), (gate.Phase, gate.Message));
            Enter (gate, "4821");
            Assert.IsType<GateViewModel> (rig.Navigator.Current);       // the right PIN does not open a locked gate
        }

        [Fact]
        public async Task TheLockout_SurvivesLeavingAndComingBack ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);
            for (var i = 0; i < 5; i++)
                Enter (gate, "0000");

            gate.CancelCommand.Execute (null);                         // press back and come in again, hoping the count reset
            var again = OpenGate (rig);

            Assert.Equal (GatePhase.Locked, again.Phase);
        }

        [Fact]
        public async Task After30Seconds_TheGateIsOfferedAgain_FromTheStart ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);
            gate.HoldCompletedCommand.Execute (null);
            for (var i = 0; i < 5; i++)
                Enter (gate, "0000");

            rig.Clock.Advance (TimeSpan.FromSeconds (30));

            Assert.Equal ((GatePhase.Hold, "Press and hold. This part is for grown-ups."), (gate.Phase, gate.Message));
        }

        [Fact]
        public async Task Cancel_ReturnsWithoutUnlocking ()
        {
            await using var rig = new AppRig (WithPin ());
            var gate = OpenGate (rig);

            gate.CancelCommand.Execute (null);

            Assert.Same (rig.Main, rig.Navigator.Current);
        }

        [Fact]
        public async Task WithNoPinYet_TheHoldAloneUnlocks_ExactlyOnce ()
        {
            await using var rig = new AppRig ();
            var gate = OpenGate (rig);

            gate.HoldCompletedCommand.Execute (null);
            gate.HoldCompletedCommand.Execute (null);

            Assert.IsType<SettingsViewModel> (rig.Navigator.Current);
            rig.Navigator.GoBack ();
            Assert.Same (rig.Main, rig.Navigator.Current);              // one settings screen, not two
        }
    }
}
