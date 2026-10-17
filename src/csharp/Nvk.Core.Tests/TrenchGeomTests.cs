using System;
using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class TrenchGeomTests
    {
        static SectionGeom G()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            return SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket }, 102.0);
        }

        [Fact]
        public void XAtBottomIsHalfWidth()
        {
            var g = G();
            Assert.Equal(g.TrenchW * 0.5, TrenchGeom.XAt(g, 0.5, g.Elev.TrenchBottom), 6);
        }

        [Fact]
        public void XGrowsWithSlope()
        {
            var g = G();
            double xBot = TrenchGeom.XAt(g, 0.5, g.Elev.TrenchBottom);
            double xTop = TrenchGeom.XTop(g, 0.5);
            Assert.True(xTop > xBot);
            Assert.Equal(xBot + 0.5 * (g.Elev.Surface - g.Elev.TrenchBottom), xTop, 6);
        }

        [Fact]
        public void OshTwoMetersBelowFloor()
        {
            var g = G();
            Assert.Equal(97.0, TrenchGeom.Osh(g.Elev), 6);
        }

        [Fact]
        public void EarthOutlineWithoutSamplesIsTrapezoid()
        {
            var g = G();
            var poly = TrenchGeom.EarthOutline(g, 0.5);
            Assert.Equal(4, poly.Count);
            Assert.Equal(-TrenchGeom.XTop(g, 0.5), poly[0].X, 6);
            Assert.Equal(g.Elev.Surface, poly[0].Z, 6);
            Assert.Equal(-TrenchGeom.HalfBottom(g), poly[1].X, 6);
            Assert.Equal(g.Elev.TrenchBottom, poly[1].Z, 6);
        }

        [Fact]
        public void EarthOutlineVerticalWhenMZero()
        {
            var g = G();
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 103),
                new SurfPt(0, 102),
                new SurfPt(3, 101)
            };
            var poly = TrenchGeom.EarthOutline(g, 0.0);
            double b = TrenchGeom.HalfBottom(g);
            Assert.Equal(4, poly.Count);
            Assert.Equal(-b, poly[0].X, 6);
            Assert.Equal(-b, poly[1].X, 6);
            Assert.Equal(b, poly[2].X, 6);
            Assert.Equal(b, poly[3].X, 6);
        }

        [Fact]
        public void BackfillDrawPolyInsertsTinMids()
        {
            var g = G();
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 103),
                new SurfPt(-0.2, 102.4),
                new SurfPt(0.2, 102.2),
                new SurfPt(3, 101)
            };
            var box = TrenchGeom.BackfillOutline(g, 0.0);
            var draw = TrenchGeom.BackfillDrawPoly(g, 0.0);
            Assert.Equal(4, box.Count);
            Assert.True(draw.Count > box.Count);
            Assert.Contains(draw, p => Math.Abs(p.X + 0.2) < 1e-9);
            Assert.Contains(draw, p => Math.Abs(p.X - 0.2) < 1e-9);
        }

        [Fact]
        public void EarthOutlineBrovkaUsesSurfAtXTop()
        {
            var g = G();
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 103),
                new SurfPt(0, 102),
                new SurfPt(3, 101)
            };
            var poly = TrenchGeom.EarthOutline(g, 0.5);
            double xTop = TrenchGeom.XTop(g, 0.5);
            Assert.Equal(-xTop, poly[0].X, 6);
            Assert.Equal(TrenchGeom.SurfZAt(g, -xTop), poly[0].Z, 6);
            Assert.Equal(xTop, poly[3].X, 6);
        }

        [Fact]
        public void GapSurfaceOnlyOnL2L3L4()
        {
            Assert.False(TrenchGeom.GapSurface(1));
            Assert.True(TrenchGeom.GapSurface(2));
            Assert.True(TrenchGeom.GapSurface(3));
            Assert.True(TrenchGeom.GapSurface(4));
            Assert.False(TrenchGeom.GapSurface(5));
        }

        [Fact]
        public void SurfaceWingsGapAtTrenchWall()
        {
            var g = G();
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 103),
                new SurfPt(0, 102),
                new SurfPt(3, 101)
            };
            List<CsPt> left, right;
            TrenchGeom.SurfaceWings(g, false, 0.5, out left, out right);
            double xTop = TrenchGeom.XTop(g, 0.5);
            Assert.True(left.Count >= 2);
            Assert.True(right.Count >= 2);
            Assert.Equal(-xTop, left[left.Count - 1].X, 6);
            Assert.Equal(xTop, right[0].X, 6);
            foreach (var p in left) Assert.True(p.X <= -xTop + 1e-9);
            foreach (var p in right) Assert.True(p.X >= xTop - 1e-9);
        }

        [Fact]
        public void SurfaceLineRawIsL6PlusPavement()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3 }, 102.0, pav: 1.02);
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 102),
                new SurfPt(0, 102),
                new SurfPt(3, 102)
            };
            var l6 = TrenchGeom.SurfaceLine(g, false);
            var l0 = TrenchGeom.SurfaceLine(g, true);
            Assert.Equal(3, l6.Count);
            Assert.Equal(102.0, l6[1].Z, 6);
            Assert.Equal(103.02, l0[1].Z, 6);
            Assert.Equal(1.02, l0[1].Z - l6[1].Z, 6);
        }

        [Fact]
        public void PavementPolyEmptyWithoutThickness()
        {
            var g = G();
            Assert.Empty(TrenchGeom.PavementPoly(g));
        }

        [Fact]
        public void ZashOutlineEmptyWhenL6BelowL2()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3 }, 99.5, pav: 1.02);
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 99.5),
                new SurfPt(0, 99.5),
                new SurfPt(3, 99.5)
            };
            Assert.True(g.Elev.Surface < g.Elev.SandTop);
            Assert.Equal(g.Elev.SandTop, g.Elev.ZashTop, 6);
            Assert.Empty(TrenchGeom.ZashOutline(g, 0.5));
            Assert.Equal(g.Elev.SandTop, TrenchGeom.ZashZAt(g, 0.0), 6);
        }

        [Fact]
        public void ZashOutlineTopNotAboveLocalL6()
        {
            var g = G();
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 100.2),
                new SurfPt(0, 102.0),
                new SurfPt(3, 100.2)
            };
            var poly = TrenchGeom.ZashOutline(g, 0.5);
            Assert.True(poly.Count >= 3);
            double sand = g.Elev.SandTop;
            foreach (var p in poly)
            {
                if (Math.Abs(p.Z - sand) < 1e-9) continue;
                double earth = TrenchGeom.SurfZAt(g, p.X);
                Assert.True(p.Z <= earth + 1e-6);
                Assert.True(p.Z >= sand - 1e-9);
            }
        }

        [Fact]
        public void AsymmetricBottomUsesLeftAndRight()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var cfg = new SectionConfig
            {
                Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket,
                TrenchLeft = 0.80, TrenchRight = 0.30
            };
            var g = SectionGeom.From(hit, cfg, 102.0);
            Assert.Equal(0.80, g.TrenchLeft, 6);
            Assert.Equal(0.30, g.TrenchRight, 6);
            Assert.Equal(1.10, g.TrenchW, 6);
            var poly = TrenchGeom.EarthOutline(g, 0.0);
            Assert.Equal(-0.80, poly[1].X, 6);
            Assert.Equal(0.30, poly[2].X, 6);
            Assert.Equal(-0.80, poly[0].X, 6);
            Assert.Equal(0.30, poly[3].X, 6);
        }

        [Fact]
        public void OneSideSlopeKeepsOtherVertical()
        {
            var g = G();
            g.TrenchLeft = 0.45;
            g.TrenchRight = 0.45;
            var poly = TrenchGeom.EarthOutline(g, 0.0, 0.5);
            double h = g.Elev.Surface - g.Elev.TrenchBottom;
            Assert.Equal(-0.45, poly[0].X, 6);
            Assert.Equal(0.45 + 0.5 * h, poly[3].X, 6);
        }
    }
}
