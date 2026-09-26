using AlertBuddy.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Xunit;

namespace AlertBuddy.ViewModels.Tests.Services
{
    public class NavigatorTests
    {
        private class Screen : ObservableObject, IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose () => Disposed = true;
        }

        private sealed class Home : Screen { }
        private sealed class Page : Screen { }
        private sealed class Other : Screen { }

        private sealed class Guarded : Screen, IHandlesBack
        {
            public bool Swallow { get; set; } = true;
            public bool HandleBack () => Swallow;
        }

        private static (Navigator Nav, Home Home, List<string> Log) Make ()
        {
            var nav = new Navigator ();
            var home = new Home ();
            var log = new List<string> ();
            nav.SetRoot (home);
            nav.CurrentChanged += () => log.Add (nav.Current.GetType ().Name);
            return (nav, home, log);
        }

        [Fact]
        public void BeforeARoot_ThereIsNoCurrentScreen ()
            => Assert.Throws<InvalidOperationException> (() => new Navigator ().Current);

        [Fact]
        public void TheRoot_IsCurrent_AndCannotBeLeft ()
        {
            var (nav, home, log) = Make ();

            nav.GoBack ();
            nav.GoHome ();

            Assert.Same (home, nav.Current);
            Assert.False (nav.CanGoBack);
            Assert.Empty (log);                                // nothing changed, so nothing was announced
        }

        [Fact]
        public void GoTo_BuildsAFreshScreenEachTime_FromItsRegisteredFactory ()
        {
            var (nav, _, log) = Make ();
            nav.Register (() => new Page ());

            nav.GoTo<Page> ();
            var first = nav.Current;
            nav.GoBack ();
            nav.GoTo<Page> ();

            Assert.NotSame (first, nav.Current);
            Assert.Equal (["Page", "Home", "Page"], log);
        }

        [Fact]
        public void GoTo_AnUnregisteredScreen_SaysWhichOne ()
        {
            var (nav, _, _) = Make ();

            var ex = Assert.Throws<InvalidOperationException> (() => nav.GoTo<Other> ());

            Assert.Contains ("Other", ex.Message);
        }

        [Fact]
        public void Show_PutsAScreenBuiltWithArgumentsOnTop ()
        {
            var (nav, _, _) = Make ();
            var page = new Page ();

            nav.Show (page);

            Assert.Same (page, nav.Current);
            Assert.True (nav.CanGoBack);
        }

        [Fact]
        public void GoBack_LeavesTheTopScreen_AndReleasesIt ()
        {
            var (nav, home, _) = Make ();
            var page = new Page ();
            nav.Show (page);

            nav.GoBack ();

            Assert.Same (home, nav.Current);
            Assert.True (page.Disposed);            // a screen that has been left lets go of its subscriptions
            Assert.False (home.Disposed);
        }

        [Fact]
        public void GoHome_LeavesEveryScreenAboveHome_AndReleasesThemAll ()
        {
            var (nav, home, log) = Make ();
            var a = new Page ();
            var b = new Page ();
            nav.Show (a);
            nav.Show (b);
            log.Clear ();

            nav.GoHome ();

            Assert.Same (home, nav.Current);
            Assert.True (a.Disposed && b.Disposed);
            Assert.Equal (["Home"], log);            // announced once, not once per screen
        }

        [Fact]
        public void TheBackButton_StepsBack_AndOnlyLeavesTheAppAtHome ()
        {
            var (nav, _, _) = Make ();
            nav.Show (new Page ());

            Assert.True (nav.HandleBack ());         // handled: the app stays open
            Assert.False (nav.HandleBack ());        // at Home nothing is left to step back to: the platform may leave the app
        }

        [Fact]
        public void AScreenThatClaimsBack_KeepsTheUser_UntilItLetsGo ()
        {
            var (nav, _, _) = Make ();
            var guarded = new Guarded ();
            nav.Show (guarded);

            Assert.True (nav.HandleBack ());
            Assert.Same (guarded, nav.Current);       // swallowed: still there

            guarded.Swallow = false;
            Assert.True (nav.HandleBack ());
            Assert.IsType<Home> (nav.Current);
        }

        [Fact]
        public void SettingANewRoot_ReplacesTheStack ()
        {
            var (nav, _, _) = Make ();
            nav.Show (new Page ());
            var fresh = new Home ();

            nav.SetRoot (fresh);

            Assert.Same (fresh, nav.Current);
            Assert.False (nav.CanGoBack);
        }
    }
}
