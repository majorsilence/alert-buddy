using System.Globalization;
using AlertBuddy.Core.Alerts;
using AlertBuddy.Core.Ntfy;
using AlertBuddy.Core.Security;
using AlertBuddy.Core.Settings;
using AlertBuddy.ViewModels.Screens;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Screens
{
    public class AlertDetailTests
    {
        private static AlertDetailViewModel OpenFirstTicket (AppRig rig)
        {
            rig.Main.ActiveAlerts[0].OpenCommand.Execute (null);
            return rig.Current<AlertDetailViewModel> ();
        }

        [Fact]
        public async Task ATicket_OpensItsDetail_WithEverythingAboutTheAlert ()
        {
            await using var rig = new AppRig ();
            rig.GoLive ();
            rig.Warning ();

            var detail = OpenFirstTicket (rig);

            Assert.Equal ("Workshop", detail.Source);
            Assert.Equal ("Warning", detail.LevelWord);
            Assert.Equal ("Keep an eye on it.", detail.WhatToDo);
            Assert.Equal ("41 degrees", detail.TemperatureText);
            Assert.Equal (41.2, detail.Temperature);
            Assert.Equal ("Workshop is at 41.2 °C", detail.Body);
            Assert.Equal ("a few seconds ago", detail.TimeAgo);
            Assert.Equal (AppRig.Start.ToString ("HH:mm", CultureInfo.InvariantCulture), detail.ClockTime);
        }

        [Fact]
        public async Task AnAlarmsDetail_SaysToTellAGrownUp ()
        {
            await using var rig = new AppRig ();
            rig.Alarm ();
            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);

            var detail = OpenFirstTicket (rig);

            Assert.Equal (("Alarm", "Tell a grown-up now."), (detail.LevelWord, detail.WhatToDo));
        }

        [Fact]
        public async Task ANonHeatAlert_ShowsNoTemperature ()
        {
            await using var rig = new AppRig ();
            rig.Warning ("Front door", degrees: null);

            var detail = OpenFirstTicket (rig);

            Assert.Equal ("", detail.TemperatureText);
            Assert.Null (detail.Temperature);
        }

        [Fact]
        public async Task TheDetail_FollowsTheAlert_AsItWorsens_AndWhenItClears ()
        {
            await using var rig = new AppRig ();
            rig.Warning ();
            var detail = OpenFirstTicket (rig);
            var log = new PropertyLog (detail);

            rig.Alarm (degrees: 55.2);
            rig.Current<AlarmViewModel> ().ToldAGrownUpCommand.Execute (null);          // the takeover covers it; then it is answered
            Assert.Equal (("Alarm", "55 degrees"), (detail.LevelWord, detail.TemperatureText));
            Assert.Contains (nameof (AlertDetailViewModel.LevelWord), log.Names);

            rig.AllClear ();
            Assert.Equal (("All clear", ""), (detail.LevelWord, detail.WhatToDo));
        }

        [Fact]
        public async Task GotIt_IsForAGrownUp_AndOnlyWhileTheAlertIsOpenAndNotYetHandled ()
        {
            await using var rig = new AppRig ();
            rig.Warning ();
            var detail = OpenFirstTicket (rig);
            Assert.True (detail.GotItCommand.CanExecute (null));

            detail.GotItCommand.Execute (null);

            Assert.Equal (AlertStatus.Handled, detail.Status);
            Assert.False (detail.GotItCommand.CanExecute (null));           // already handled

            rig.AllClear ();
            Assert.False (detail.GotItCommand.CanExecute (null));           // resolved
        }

        [Fact]
        public async Task Back_ReturnsToWhereTheChildWas ()
        {
            await using var rig = new AppRig ();
            rig.Warning ();
            var detail = OpenFirstTicket (rig);

            detail.BackCommand.Execute (null);

            Assert.Same (rig.Main, rig.Navigator.Current);
        }
    }

    public class AlertBookTests
    {
        private static void PastAlert (AppRig rig, string source, TimeSpan startedAgo, TimeSpan clearedAgo)
        {
            rig.Send ($"{source}: warning", 4, "41 °C", origin: MessageOrigin.Backlog, age: startedAgo);
            rig.Send ($"{source}: cleared", 3, "30 °C", origin: MessageOrigin.Backlog, age: clearedAgo);
        }

        private static AlertBookViewModel OpenBook (AppRig rig)
        {
            rig.Main.OpenBookCommand.Execute (null);
            return rig.Current<AlertBookViewModel> ();
        }

        [Fact]
        public async Task WithNothingYet_ItInvitesInsteadOfShowingABlankPage ()
        {
            await using var rig = new AppRig ();

            var book = OpenBook (rig);

            Assert.True (book.IsEmpty);
            Assert.Equal ("No alerts yet. When something needs a look, it shows up here.", book.EmptyText);
            Assert.Empty (book.Groups);
        }

        [Fact]
        public async Task Alerts_AreGroupedByDay_NewestFirst ()
        {
            await using var rig = new AppRig ();
            PastAlert (rig, "Cellar", TimeSpan.FromHours (72), TimeSpan.FromHours (71));
            PastAlert (rig, "Studio", TimeSpan.FromHours (20), TimeSpan.FromHours (19));
            PastAlert (rig, "Workshop", TimeSpan.FromHours (2), TimeSpan.FromHours (1));

            var book = OpenBook (rig);

            var older = (AppRig.Start - TimeSpan.FromHours (72)).ToString ("dddd d MMMM", CultureInfo.InvariantCulture);
            Assert.Equal (["Today", "Yesterday", older], book.Groups.Select (g => g.Heading));
            Assert.Equal (["Workshop"], book.Groups[0].Items.Select (i => i.Source));
            Assert.False (book.IsEmpty);
        }

        [Fact]
        public async Task TheHeadings_AreSentenceCase_NotAllCaps ()
        {
            await using var rig = new AppRig ();
            PastAlert (rig, "Workshop", TimeSpan.FromHours (72), TimeSpan.FromHours (71));

            var heading = OpenBook (rig).Groups[0].Heading;

            Assert.NotEqual (heading.ToUpperInvariant (), heading);
        }

        [Fact]
        public async Task ResolvedAlertsSayAllClear_OpenOnesSayWhatToDo ()
        {
            await using var rig = new AppRig ();
            PastAlert (rig, "Studio", TimeSpan.FromHours (2), TimeSpan.FromHours (1));
            rig.Warning ("Workshop");

            var items = OpenBook (rig).Groups.SelectMany (g => g.Items).ToList ();

            Assert.Equal ("41 degrees. All clear.", items.Single (i => i.Source == "Studio").Sentence);
            Assert.Equal ("41 degrees. Keep an eye on it.", items.Single (i => i.Source == "Workshop").Sentence);
        }

        [Fact]
        public async Task TheBook_UpdatesWhileItIsOpen ()
        {
            await using var rig = new AppRig ();
            var book = OpenBook (rig);
            Assert.True (book.IsEmpty);

            rig.Warning ();

            Assert.False (book.IsEmpty);
            Assert.Equal ("Workshop", book.Groups[0].Items[0].Source);
        }

        [Fact]
        public async Task ATicketInTheBook_OpensItsDetail ()
        {
            await using var rig = new AppRig ();
            rig.Warning ();
            var book = OpenBook (rig);

            book.Groups[0].Items[0].OpenCommand.Execute (null);

            Assert.Equal ("Workshop", rig.Current<AlertDetailViewModel> ().Source);
        }

        [Fact]
        public async Task Clearing_NeedsTheGate_AndOnlyRemovesWhatHasEnded ()
        {
            await using var rig = new AppRig ();
            PastAlert (rig, "Studio", TimeSpan.FromHours (2), TimeSpan.FromHours (1));
            rig.Warning ("Workshop");
            var book = OpenBook (rig);

            book.ClearHistoryCommand.Execute (null);
            var gate = rig.Current<GateViewModel> ();
            Assert.Equal (2, rig.Hub.Snapshot.History.Count);               // nothing is cleared until the gate opens

            gate.HoldCompletedCommand.Execute (null);                        // no PIN is set, so the hold alone unlocks it

            Assert.Same (book, rig.Navigator.Current);
            Assert.Equal (["Workshop"], book.Groups.SelectMany (g => g.Items).Select (i => i.Source));   // what is still happening stays
        }

        [Fact]
        public async Task Clearing_WithAWrongPin_ClearsNothing ()
        {
            await using var rig = new AppRig (new AppSettings { FirstRunComplete = true, Pin = PinHasher.Create ("4821") });
            PastAlert (rig, "Studio", TimeSpan.FromHours (2), TimeSpan.FromHours (1));
            var book = OpenBook (rig);
            book.ClearHistoryCommand.Execute (null);
            var gate = rig.Current<GateViewModel> ();
            gate.HoldCompletedCommand.Execute (null);

            foreach (var digit in "0000")
                gate.PressDigitCommand.Execute (digit.ToString ());

            Assert.Single (rig.Hub.Snapshot.History);
        }

        [Fact]
        public async Task Back_ReturnsToHome ()
        {
            await using var rig = new AppRig ();
            var book = OpenBook (rig);

            book.BackCommand.Execute (null);

            Assert.Same (rig.Main, rig.Navigator.Current);
        }
    }
}
