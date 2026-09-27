using System.Runtime.CompilerServices;
using Majorsilence.Forms.Headless;
using Xunit;

// Theme, Application.OpenForms and similar are process-wide, so nothing here may run in parallel (the framework's own
// suite does the same).
[assembly: CollectionBehavior (DisableTestParallelization = true)]

namespace AlertBuddy.Shared.Tests
{
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
