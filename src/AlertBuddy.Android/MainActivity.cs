using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia.Android;
using Majorsilence.Forms.Backends;

namespace AlertBuddy.Android
{
    // The theme MUST descend from Theme.AppCompat (see Resources/values/styles.xml): the template's plain
    // framework theme crashes the first frame.
    // The window itself is built in App.cs: by the time this Activity is created, AvaloniaMainActivity's
    // base OnCreate already asks the Application for its MainViewFactory. What is here is the forwarding the
    // framework asks of a host: the back press, the live Activity and the launch intent (F11, F14).
    [Activity (
        Label = "AlertBuddy",
        Theme = "@style/AlertBuddyTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
        protected override void OnCreate (Bundle? savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            BackRequested += (_, e) => e.Handled = AvaloniaPlatformBackend.RaiseBackRequested ();
            AvaloniaPlatformBackend.RegisterAndroidActivity (this);
            AvaloniaPlatformBackend.ReportAndroidIntent (Intent);

            ListenerService.Start (this);
        }

        protected override void OnNewIntent (Intent? intent)
        {
            base.OnNewIntent (intent);
            AvaloniaPlatformBackend.ReportAndroidIntent (intent);
        }

        public override void OnRequestPermissionsResult (int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
            AvaloniaPlatformBackend.ReportNotificationPermissionResult ();

            // The system's own question does not bring the app back from the background, so tell the wizard to read the answer.
            AppHost.Lifecycle.RaiseResumed ();
        }
    }
}
