using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void MainForm_RendersHeadlessly_AndTheHelloLabelIsInk ()
        {
            var form = new MainForm ();

            var png = HeadlessRenderer.CapturePng (form, 400, 300);
            File.WriteAllBytes (Path.Combine (TestEnvironment.RenderDirectory, "smoke-mainform.png"), png);

            using var bitmap = SKBitmap.Decode (png);
            // More than one flat colour: the label's glyphs are darker than the form background. Deliberately loose:
            // it only proves the harness renders. Real assertions arrive with the real screens.
            var background = bitmap.GetPixel (bitmap.Width - 2, bitmap.Height - 2);
            var inked = false;
            for (var y = 0; y < bitmap.Height && !inked; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background) { inked = true; break; }

            Assert.True (inked, "the rendered form was a single flat colour");
        }
    }
}
