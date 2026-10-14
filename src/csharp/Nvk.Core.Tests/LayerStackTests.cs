using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class LayerStackTests
    {
        static StationElevations Sample(double surface = 102.0)
        {
            // invert=100, diam=0.3, bedding=0.10, zash=0.30
            // TrenchBottom=99.90  SandTop=100  PipeTop=100.30  ZashTop=100.60
            return new StationElevations(
                station: 12.5,
                invert: 100.0,
                diam: 0.3,
                bedding: 0.10,
                thkZash: 0.30,
                surfaceRaw: surface);
        }

        [Fact]
        public void NamedLinesFromTwoSources()
        {
            var e = Sample();
            Assert.Equal(99.90, e.TrenchBottom, 6);
            Assert.Equal(100.00, e.SandTop, 6);
            Assert.Equal(100.30, e.PipeTop, 6);
            Assert.Equal(100.60, e.ZashTop, 6);
            Assert.Equal(102.00, e.Surface, 6);
        }

        [Fact]
        public void RazrabotkaBottomEqualsPesokBottom()
        {
            Assert.Equal(LayerStack.Lower(ProfileKind.Razrabotka), LayerStack.Lower(ProfileKind.Pesok));
            Assert.Equal(NamedLine.TrenchBottom, LayerStack.Lower(ProfileKind.Pesok));
        }

        [Fact]
        public void PesokTopEqualsZashitaBottom()
        {
            Assert.Equal(LayerStack.Upper(ProfileKind.Pesok), LayerStack.Lower(ProfileKind.Zashita));
            Assert.Equal(NamedLine.SandTop, LayerStack.Lower(ProfileKind.Zashita));
        }

        [Fact]
        public void ZashitaTopEqualsZasypkaBottom()
        {
            Assert.Equal(LayerStack.Upper(ProfileKind.Zashita), LayerStack.Lower(ProfileKind.Zasypka));
            Assert.Equal(NamedLine.ZashTop, LayerStack.Lower(ProfileKind.Zasypka));
        }

        [Fact]
        public void SharedNumbersMatchOnStation()
        {
            var e = Sample();
            Assert.Equal(e[LayerStack.Lower(ProfileKind.Razrabotka)], e[LayerStack.Lower(ProfileKind.Pesok)]);
            Assert.Equal(e[LayerStack.Upper(ProfileKind.Pesok)], e[LayerStack.Lower(ProfileKind.Zashita)]);
            Assert.Equal(e[LayerStack.Upper(ProfileKind.Zashita)], e[LayerStack.Lower(ProfileKind.Zasypka)]);
        }

        [Fact]
        public void PavementThicknessIsExact()
        {
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(202.63, 0.25, 0.10, 0.30, 204.50, 204.50 - 1.02,
                out trench, out sand, out pipe, out zash, out earth, out design);
            Assert.Equal(204.50, design, 6);
            Assert.Equal(203.48, earth, 6);
            Assert.Equal(1.02, design - earth, 6);
            Assert.Equal(203.18, zash, 6);
            Assert.True(zash <= earth + 1e-9);
            Assert.True(zash >= sand - 1e-9);
        }

        [Fact]
        public void ScatterDoesNotBreakZashBand()
        {
            Assert.Equal(100.0, LineClamp.AfterScatterZash(99.5, 100.0, 99.0), 6);
            Assert.Equal(100.0, LineClamp.AfterScatterZash(99.5, 100.0, 102.0), 6);
            Assert.Equal(101.5, LineClamp.AfterScatterZash(101.5, 100.0, 102.0), 6);
            Assert.Equal(102.0, LineClamp.AfterScatterZash(103.0, 100.0, 102.0), 6);
            Assert.Equal(100.0, LineClamp.AfterScatterTrench(100.2, 100.0), 6);
            Assert.Equal(101.0, LineClamp.AfterScatterBackfill(100.5, 101.0), 6);
            Assert.Equal(102.0, LineClamp.AfterScatterBackfill(102.0, 101.0), 6);
            Assert.Equal(99.9, LineClamp.AfterScatterEarth(99.9, 99.8), 6);
            Assert.Equal(99.8, LineClamp.AfterScatterEarth(99.5, 99.8), 6);
        }

        [Fact]
        public void ZashBetweenSandAndEarth()
        {
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(100, 0.3, 0.10, 0.30, 102, 102,
                out trench, out sand, out pipe, out zash, out earth, out design);
            Assert.True(zash >= sand - 1e-9);
            Assert.True(zash <= earth + 1e-9);
            Assert.Equal(100.60, zash, 6);
        }

        [Fact]
        public void ModelKeepsPavementWhenEarthBelowTrench()
        {
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(202.63, 0.25, 0.10, 0.30, 203.515, 203.515 - 1.02,
                out trench, out sand, out pipe, out zash, out earth, out design);
            Assert.Equal(1.02, design - earth, 6);
            Assert.True(earth < trench);
            Assert.Equal(sand, zash, 6);
        }

        [Fact]
        public void RoadTopKeepsDesignEvenIfEarthBelowTrench()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, ThkAsph = 1.02, PavAll = true };
            cfg.ApplyPavement();
            var p = new AxisPt
            {
                Invert = 202.63, Diam = 0.25,
                DesignZ = 203.515,
                EarthZ = 203.515 - 1.02,
                Surface = 203.515 - 1.02
            };
            double l0 = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
            double l6 = ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
            double l4 = p.NamedZ(NamedLine.ZashTop, cfg);
            Assert.Equal(203.515, l0, 6);
            Assert.Equal(202.495, l6, 6);
            Assert.Equal(1.02, l0 - l6, 6);
            double l2 = p.NamedZ(NamedLine.SandTop, cfg);
            if (l6 >= l2)
                Assert.True(l4 <= l6 + 1e-9);
            else
                Assert.Equal(l2, l4, 6);
        }

        [Fact]
        public void SurfaceNotBelowTrenchBottom()
        {
            var e = Sample(surface: 90.0);
            Assert.Equal(90.0, e.Surface, 6);
            Assert.False(e.HasBackfill);
            Assert.False(LayerStack.IsDrawn(ProfileKind.Zasypka, e));
        }

        [Fact]
        public void BackfillOnlyAboveZash()
        {
            var e = Sample(surface: 102.0);
            Assert.True(e.HasBackfill);
            Assert.Equal(1.4, LayerStack.Thickness(ProfileKind.Zasypka, e), 6);
        }

        [Fact]
        public void SandThicknessIsBedding()
        {
            var e = Sample();
            Assert.Equal(0.10, LayerStack.Thickness(ProfileKind.Pesok, e), 6);
        }
    }
}
