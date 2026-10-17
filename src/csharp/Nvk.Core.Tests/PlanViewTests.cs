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
        public void EmptyOnListKeepsOursOnly()
        {
            Assert.False(PlanView.IsOn("C-ROAD", null));
            Assert.False(PlanView.IsOn("C-ROAD", new string[0]));
            Assert.True(PlanView.IsOn("NVK-PLAN", null));
            Assert.True(PlanView.IsOn("NVK-PLAN", new string[0]));
            Assert.True(PlanView.IsOn("0", new[] { "NVK-PLAN" }));
            Assert.True(PlanView.IsOn("Defpoints", new string[0]));
            Assert.False(PlanView.IsOn("C-ROAD", new[] { "NVK-PLAN" }));
            Assert.True(PlanView.IsOn("NVK-PLAN", new[] { "nvk-plan" }));
            Assert.True(PlanView.IsOn("C-ROAD", new[] { "C-ROAD" }));
        }

        [Fact]
        public void AngleDegFromTwoPoints()
        {
            Assert.Equal(0.0, PlanView.AngleDeg(0, 0, 10, 0), 6);
            Assert.Equal(90.0, PlanView.AngleDeg(0, 0, 0, 5), 6);
            Assert.Equal(180.0, PlanView.AngleDeg(0, 0, -4, 0), 6);
            Assert.Equal(-90.0, PlanView.AngleDeg(0, 0, 0, -2), 6);
            Assert.Equal(45.0, PlanView.AngleDeg(1, 1, 3, 3), 6);
            Assert.Equal(0.0, PlanView.AngleDeg(2, 2, 2, 2), 6);
        }

        [Fact]
        public void TwistRadMakesPickedXHorizontal()
        {
            Assert.Equal(0.0, PlanView.TwistRad(0), 9);
            Assert.Equal(-System.Math.PI / 2.0, PlanView.TwistRad(90), 9);
            Assert.Equal(System.Math.PI / 4.0, PlanView.TwistRad(-45), 9);
        }

        [Fact]
        public void LeaderRotFollowsPlanX()
        {
            Assert.Equal(0.0, PlanView.LeaderRotRad(0), 9);
            Assert.Equal(System.Math.PI / 2.0, PlanView.LeaderRotRad(90), 9);
            Assert.Equal(-PlanView.TwistRad(30), PlanView.LeaderRotRad(30), 9);
        }

        [Fact]
        public void OffsetAlongPlanY()
        {
            double ox, oy;
            PlanView.Offset(10, 20, 0, 5, out ox, out oy);
            Assert.Equal(10.0, ox, 6);
            Assert.Equal(25.0, oy, 6);
            PlanView.Offset(0, 0, 90, 5, out ox, out oy);
            Assert.Equal(-5.0, ox, 6);
            Assert.Equal(0.0, oy, 6);
        }

        [Fact]
        public void PaperMmToModelAtPlanScale()
        {
            Assert.Equal(1.25, PlanView.PaperMmToModel(2.5, 500), 6);
            Assert.Equal(0.5, PlanView.PaperMmToModel(2.5, 200), 6);
            Assert.Equal(1.25, PlanView.PaperMmToModel(2.5, 0), 6);
        }

        [Fact]
        public void SampLayerOffOnPipePlan()
        {
            Assert.True(PlanView.IsSampLeader(PlanView.LayerSamp));
            Assert.False(PlanView.IsSampLeader(PlanView.LayerMh));
            Assert.False(PlanView.IsOn(PlanView.LayerSamp, null, true));
            Assert.True(PlanView.IsOn(PlanView.LayerMh, null, true));
            Assert.True(PlanView.IsOn(PlanView.LayerSamp, null, false));
            Assert.False(PlanView.IsOn(PlanView.LayerSamp, new[] { PlanView.LayerMh }, true));
        }

        [Fact]
        public void IsOursMatchesNvkPrefix()
        {
            Assert.True(PlanView.IsOurs("NVK-PLAN"));
            Assert.True(PlanView.IsOurs(PlanView.LayerSit));
            Assert.True(PlanView.IsOurs("nvk-ep-razr"));
            Assert.True(PlanView.IsOurs("0"));
            Assert.True(PlanView.IsOurs("Defpoints"));
            Assert.False(PlanView.IsOurs("C-ROAD"));
            Assert.False(PlanView.IsOurs(""));
        }
    }
}
