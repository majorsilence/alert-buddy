using System.Runtime.CompilerServices;
using Majorsilence.Forms.Headless;
using Xunit;
using Xunit.v3;

// Theme, Application.OpenForms and similar are process-wide, so nothing here may run in parallel (the framework's own
// suite does the same).
[assembly: CollectionBehavior (DisableTestParallelization = true)]

// The headless backend's UI thread is the thread that last called HeadlessRenderer.Use (framework 26.7.0 made that explicit). Tests run on
// whichever thread xUnit gives them, so each one has to claim it first; otherwise the view bindings see "not on the UI thread" and queue
// their updates for a pump nobody runs.
[assembly: AlertBuddy.Shared.Tests.ClaimUiThread]

namespace AlertBuddy.Shared.Tests
{
    /// <summary>Makes the thread a test runs on the headless backend's UI thread, before the test starts.</summary>
    [AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    internal sealed class ClaimUiThreadAttribute : BeforeAfterTestAttribute
    {
        public override void Before (System.Reflection.MethodInfo methodUnderTest, IXunitTest test) => HeadlessRenderer.Use ();
    }

    internal static class TestEnvironment
    {
        // Selects the Headless backend before any form is created, so every test renders without a display.
        [ModuleInitializer]
        internal static void UseHeadlessBackend () => HeadlessRenderer.Use ();

        /// <summary>Where design-review renders go. Git-ignored: goldens live under Golden/ and are committed on purpose.</summary>
        internal static string RenderDirectory {
            get {
                var dir = Directory.GetCurrentDirectory ();
                while (dir is not null && !File.Exists (Path.Combine (dir, "AlertBuddy.slnx")))
                    dir = Path.GetDirectoryName (dir);

                var render = Path.Combine (dir ?? Directory.GetCurrentDirectory (), "render-out");
                Directory.CreateDirectory (render);
                return render;
            }
        }
    }
}
