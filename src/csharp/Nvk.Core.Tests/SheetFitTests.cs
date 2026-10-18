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
        public void TooTallKeepsSizePinsTop()
        {
            var cx = new[] { 50.0, 50.0 };
            var cy = new[] { 400.0, 50.0 };
            var w = new[] { 80.0, 80.0 };
            var h = new[] { 200.0, 200.0 };
            double s = SheetFit.Fit(cx, cy, w, h, 0, 0, 200, 300);
            Assert.Equal(1.0, s, 6);
            Assert.Equal(80.0, w[0], 6);
            Assert.Equal(200.0, h[0], 6);
            Assert.Equal(80.0, w[1], 6);
            Assert.Equal(200.0, h[1], 6);
            double top = System.Math.Max(cy[0] + h[0] * 0.5, cy[1] + h[1] * 0.5);
            Assert.Equal(300.0, top, 6);
        }
    }
}
