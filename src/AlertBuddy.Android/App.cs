using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;
using AlertBuddy.Shared;
using AlertBuddy.Shared.Platform;
using AlertBuddy.Shared.Theme;
using AlertBuddy.ViewModels;

using MSForms = Majorsilence.Forms;

namespace AlertBuddy.Android
{
    // The Android Application that owns Avalonia's bootstrap. Avalonia's Android integration runs
    // AppBuilder.Configure<TApp>().UseAndroid()...SetupWithLifetime(...) from the base class before
    // MainActivity exists.
    [Application]
    public class MainApplication : AvaloniaAndroidApplication<AvaloniaApp>
    {
        public MainApplication (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
        {
        }
    }

    public sealed class AvaloniaApp : Avalonia.Application
    {
        // The app itself is built by AppHost, shared with the foreground service; this only puts a window on it.
        private static MainForm CreateMainForm ()
        {
            var app = AppHost.Get (global::Android.App.Application.Context);
            AlertBuddyTheme.Apply (app.Settings.Current.Look);

            var form = new MainForm (app);
            form.BackRequested += (_, e) => e.Cancel = AppHost.Lifecycle.RaiseBack ();
            MSForms.Application.Resumed += (_, _) => AppHost.Lifecycle.RaiseResumed ();
            MSForms.Application.Suspended += (_, _) => AppHost.Lifecycle.RaisePaused ();
            app.Start ();
            return form;
        }

        public override void OnFrameworkInitializationCompleted ()
        {
            if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime) {
                activityLifetime.MainViewFactory = () => {
                    // MainForm.Show() constructs the single-view host, which registers itself as
                    // ISingleViewApplicationLifetime.MainView -- read it back rather than reaching into
                    // the internal host type.
                    MSForms.Application.RunAndroid (CreateMainForm);
                    return ((ISingleViewApplicationLifetime) ApplicationLifetime!).MainView!;
                };
            }

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
