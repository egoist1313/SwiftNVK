using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class PlanViewTests
    {
        [Fact]
        public void RotatedSizeZeroIsAxisBox()
        {
            double w, h;
            PlanView.RotatedSize(0, 0, 10, 4, 0, out w, out h);
            Assert.Equal(10.0, w, 6);
            Assert.Equal(4.0, h, 6);
        }

        [Fact]
        public void RotatedSize45IsDiagonal()
        {
            double w, h;
            PlanView.RotatedSize(0, 0, 2, 2, 45, out w, out h);
            Assert.Equal(2.0 * System.Math.Sqrt(2.0), w, 6);
            Assert.Equal(2.0 * System.Math.Sqrt(2.0), h, 6);
        }

        [Fact]
        public void SplitAndJoinLayers()
        {
            var a = PlanView.SplitLayers("NVK-PLAN; 0 ;NVK-PLAN,C-ROAD");
            Assert.Equal(3, a.Count);
            Assert.Equal("NVK-PLAN", a[0]);
            Assert.Equal("0", a[1]);
            Assert.Equal("C-ROAD", a[2]);
            Assert.Equal("NVK-PLAN;0;C-ROAD", PlanView.JoinLayers(a));
        }

        [Fact]
        public void EmptyOnListKeepsAll()
        {
            Assert.True(PlanView.IsOn("C-ROAD", null));
            Assert.True(PlanView.IsOn("C-ROAD", new string[0]));
            Assert.True(PlanView.IsOn("0", new[] { "NVK-PLAN" }));
            Assert.False(PlanView.IsOn("C-ROAD", new[] { "NVK-PLAN" }));
            Assert.True(PlanView.IsOn("NVK-PLAN", new[] { "nvk-plan" }));
        }
    }
}
