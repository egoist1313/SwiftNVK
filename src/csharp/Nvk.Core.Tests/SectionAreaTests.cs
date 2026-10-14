using System;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SectionAreaTests
    {
        static SectionGeom G()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            return SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket }, 102.0);
        }

        [Fact]
        public void VerticalL1IsWidthTimesDepth()
        {
            var g = G();
            double? a = SectionArea.For(g, ProfileKind.Razrabotka, 0.0);
            double B = g.TrenchW;
            double d = g.Elev.Surface - g.Elev.TrenchBottom;
            Assert.True(a.HasValue);
            Assert.Equal(B * d, a.Value, 5);
        }

        [Fact]
        public void EarthNotBelowTrenchZashNotAboveEarth()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var cfg = new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket };
            var g = SectionGeom.From(hit, cfg, 100.2, pav: 1.02);
            Assert.True(g.Elev.ZashTop <= g.Elev.Surface + 1e-9);
        }

        [Fact]
        public void ZashitaAreaZeroWhenL4OnL2()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3 }, 99.5);
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Zashita, 0.5).Value, 6);
        }

        [Fact]
        public void SlopeL1BiggerThanVertical()
        {
            var g = G();
            double v = SectionArea.For(g, ProfileKind.Razrabotka, 0.0).Value;
            double s = SectionArea.For(g, ProfileKind.Razrabotka, 0.5).Value;
            Assert.True(s > v);
        }

        [Fact]
        public void L1AreaStopsAtL6NotL0()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket }, 102.0, pav: 1.02);
            double a = SectionArea.For(g, ProfileKind.Razrabotka, 0.0).Value;
            double B = g.TrenchW;
            double toL6 = B * (g.Elev.Surface - g.Elev.TrenchBottom);
            double toL0 = B * (g.Elev.Surface + g.Pavement - g.Elev.TrenchBottom);
            Assert.Equal(toL6, a, 5);
            Assert.True(toL0 > toL6 + 0.5);
        }

        [Fact]
        public void AreaZeroInsideManhole()
        {
            var g = G();
            g.InManhole = true;
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Pesok, 0.5).Value, 6);
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Razrabotka, 0.5).Value, 6);
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Zashita, 0.5).Value, 6);
            Assert.Null(SectionArea.For(g, ProfileKind.Truba, 0.5));
        }

        [Fact]
        public void AsymmetricVerticalL1IsSumOfHalvesTimesDepth()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var cfg = new SectionConfig
            {
                Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket,
                TrenchLeft = 0.80, TrenchRight = 0.30
            };
            var g = SectionGeom.From(hit, cfg, 102.0);
            double a = SectionArea.For(g, ProfileKind.Razrabotka, 0.0).Value;
            double d = g.Elev.Surface - g.Elev.TrenchBottom;
            Assert.Equal(1.10 * d, a, 5);
        }

        [Fact]
        public void ZeroWidthKeepsCatalogHalf()
        {
            var g = G();
            Assert.Equal(TrenchWidth.For(0.3, JointType.Socket), g.TrenchW, 6);
            Assert.Equal(g.TrenchW * 0.5, g.TrenchLeft, 6);
            Assert.Equal(g.TrenchW * 0.5, g.TrenchRight, 6);
        }

        [Fact]
        public void CircleStripFullIsPiR2()
        {
            Assert.Equal(Math.PI, SectionArea.CircleStrip(0.0, 1.0, -1.0, 1.0), 8);
            Assert.Equal(0.0, SectionArea.CircleStrip(0.0, 1.0, 1.0, 2.0), 8);
            Assert.Equal(Math.PI * 0.5, SectionArea.CircleStrip(0.0, 1.0, -1.0, 0.0), 8);
        }

        [Fact]
        public void PipeInLayerHalfCircleAtCenter()
        {
            double hole = SectionArea.PipeInLayer(100.0, 0.3, 0.0, 100.0, 100.15);
            Assert.Equal(0.5 * Math.PI * 0.15 * 0.15, hole, 8);
        }

        [Fact]
        public void ZashitaSubtractsOnlyCoveredPipe()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket }, 100.15);
            double a = SectionArea.For(g, ProfileKind.Zashita, 0.0).Value;
            double band = g.TrenchW * (g.Elev.ZashTop - g.Elev.SandTop);
            double hole = SectionArea.PipeInLayer(100.0, 0.3, 0.0, g.Elev.SandTop, g.Elev.ZashTop);
            Assert.Equal(band - hole, a, 6);
            Assert.True(hole < Math.PI * 0.15 * 0.15 - 1e-6);
        }

        [Fact]
        public void L1AreaZeroWhenL6BelowL1()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3 }, 99.5, pav: 1.02);
            Assert.True(g.Elev.Surface < g.Elev.TrenchBottom);
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Razrabotka, 0.5).Value, 6);
        }
    }
}
