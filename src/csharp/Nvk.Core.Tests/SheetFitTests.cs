using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SheetFitTests
    {
        [Fact]
        public void InsidePrintableUnchanged()
        {
            var cx = new[] { 50.0 };
            var cy = new[] { 50.0 };
            var w = new[] { 20.0 };
            var h = new[] { 20.0 };
            double s = SheetFit.Fit(cx, cy, w, h, 0, 0, 100, 100);
            Assert.Equal(1.0, s, 6);
            Assert.Equal(50.0, cx[0], 6);
            Assert.Equal(50.0, cy[0], 6);
            Assert.Equal(20.0, w[0], 6);
        }

        [Fact]
        public void OverflowBelowMovesIntoPage()
        {
            var cx = new[] { 40.0 };
            var cy = new[] { -10.0 };
            var w = new[] { 20.0 };
            var h = new[] { 40.0 };
            double s = SheetFit.Fit(cx, cy, w, h, 0, 0, 80, 60);
            Assert.Equal(1.0, s, 6);
            Assert.True(cy[0] - h[0] * 0.5 >= -1e-6);
            Assert.True(cy[0] + h[0] * 0.5 <= 60.0 + 1e-6);
        }

        [Fact]
        public void TooTallScalesToPrintable()
        {
            var cx = new[] { 50.0, 50.0 };
            var cy = new[] { 400.0, 50.0 };
            var w = new[] { 80.0, 80.0 };
            var h = new[] { 200.0, 200.0 };
            double s = SheetFit.Fit(cx, cy, w, h, 0, 0, 200, 300);
            Assert.True(s < 1.0);
            Assert.True(s > 0.05);
            for (int i = 0; i < 2; i++)
            {
                Assert.True(cx[i] - w[i] * 0.5 >= -1e-6);
                Assert.True(cy[i] - h[i] * 0.5 >= -1e-6);
                Assert.True(cx[i] + w[i] * 0.5 <= 200.0 + 1e-6);
                Assert.True(cy[i] + h[i] * 0.5 <= 300.0 + 1e-6);
            }
        }
    }
}
