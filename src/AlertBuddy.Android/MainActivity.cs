using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace AlertBuddy.Android
{
    // The theme MUST descend from Theme.AppCompat (see Resources/values/styles.xml): the template's plain
    // framework theme crashes the first frame.
    // All the wiring lives in App.cs -- by the time this Activity is created, AvaloniaMainActivity's
    // base OnCreate already asks the Application for its MainViewFactory and uses the control it
    // returns as this Activity's content.
    [Activity (
        Label = "AlertBuddy",
        Theme = "@style/AlertBuddyTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
    }
}
