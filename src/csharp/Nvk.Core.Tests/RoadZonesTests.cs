using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class RoadZonesTests
    {
        [Fact]
        public void ParseCyrillicAndLatin()
        {
            var z = RoadZones.Parse("А:0-2.088 Г:2.088-17 T:17-20", 20);
            Assert.Equal(3, z.Count);
            Assert.Equal("ASPH", z[0].Type);
            Assert.Equal("GAZN", z[1].Type);
            Assert.Equal("TROT", z[2].Type);
        }

        [Fact]
        public void ParseOther()
        {
            var z = RoadZones.Parse("П:0-5 P:5-10", 10);
            Assert.Equal(2, z.Count);
            Assert.Equal("OTHER", z[0].Type);
            var cfg = new SectionConfig { ThkOther = 0.35 };
            Assert.Equal(0.35, RoadZones.ThkAt(2, z, cfg), 6);
        }

        [Fact]
        public void OverlapsAreClipped()
        {
            var z = RoadZones.Parse("А:0-10 Г:5-15", 20);
            Assert.Equal(2, z.Count);
            Assert.Equal(0.0, z[0].Ch0, 6);
            Assert.Equal(10.0, z[0].Ch1, 6);
            Assert.Equal(10.0, z[1].Ch0, 6);
        }

        [Fact]
        public void EndAtStopsAtZoneBoundary()
        {
            var z = RoadZones.Parse("А:0-5 Г:5-12", 20);
            Assert.Equal(5.0, RoadZones.EndAt(1.0, z, 20), 6);
            Assert.Equal(12.0, RoadZones.EndAt(5.0, z, 20), 6);
            Assert.Equal(20.0, RoadZones.EndAt(15.0, z, 20), 6);
        }

        [Fact]
        public void ThkAtUsesCatalog()
        {
            var cfg = new SectionConfig { ThkAsph = 1.02, ThkGazn = 0.15, ThkTrot = 0.31 };
            var z = RoadZones.Parse("А:0-10 Г:10-20", 20);
            Assert.Equal(1.02, RoadZones.ThkAt(5, z, cfg), 6);
            Assert.Equal(0.15, RoadZones.ThkAt(12, z, cfg), 6);
            Assert.Equal(0.0, RoadZones.ThkAt(21, z, cfg), 6);
        }

        [Fact]
        public void SliceKeepsWholeStringForFirstRun()
        {
            Assert.Equal("А:0-10 Г:10-20", RoadZones.Slice("А:0-10 Г:10-20", 0));
            Assert.Equal("", RoadZones.Slice("А:0-10 Г:10-20", 1));
        }

        [Fact]
        public void SliceSplitsRunsByPipe()
        {
            var z0 = RoadZones.Parse("А:0-10 Г:10-20 | А:0-8", 20, 0);
            Assert.Equal(2, z0.Count);
            Assert.Equal("ASPH", z0[0].Type);
            Assert.Equal(10.0, z0[0].Ch1, 6);
            var z1 = RoadZones.Parse("А:0-10 Г:10-20 | А:0-8", 20, 1);
            Assert.Single(z1);
            Assert.Equal("ASPH", z1[0].Type);
            Assert.Equal(8.0, z1[0].Ch1, 6);
            Assert.Empty(RoadZones.Parse("А:0-10 | А:0-8", 20, 2));
        }

        [Fact]
        public void JoinRunsKeepsPipe()
        {
            Assert.Equal("А:0-10 | Г:0-8", RoadZones.JoinRuns(new[] { "А:0-10", "Г:0-8" }));
            Assert.Equal(" | А:0-8", RoadZones.JoinRuns(new[] { "", "А:0-8" }));
            Assert.Equal("", RoadZones.JoinRuns(new[] { "", "" }));
        }

        [Fact]
        public void FormatRoundtrip()
        {
            var z = RoadZones.Parse("A:0-5 G:5-10", 10);
            var s = RoadZones.Format(z);
            Assert.Contains("А:", s);
            Assert.Contains("Г:", s);
        }
    }

    public class ScatterTests
    {
        [Fact]
        public void GenerateCountAndRange()
        {
            var v = Scatter.Generate(20, 1);
            Assert.Equal(20, v.Count);
            Assert.All(v, x => Assert.InRange(x, -1.0, 1.0));
        }

        [Fact]
        public void InterpEnds()
        {
            var v = new System.Collections.Generic.List<double> { -1.0, 1.0 };
            Assert.Equal(-1.0, Scatter.At(0, 10, v), 6);
            Assert.Equal(1.0, Scatter.At(10, 10, v), 6);
            Assert.Equal(0.0, Scatter.At(5, 10, v), 6);
        }

        [Fact]
        public void AmpRespectsToggles()
        {
            var cfg = new SectionConfig { UseScatterRazr = true, ScatterRazr = 0.05, UseScatterTruba = false, ScatterTruba = 0.005 };
            Assert.Equal(0.05, Scatter.Amp(ProfileKind.Pesok, cfg), 6);
            Assert.Equal(0.0, Scatter.Amp(ProfileKind.Truba, cfg), 6);
        }

        [Fact]
        public void DifferentSeedsGiveDifferentSeries()
        {
            var a = Scatter.Generate(20, 1009);
            var b = Scatter.Generate(20, 2017);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void DefaultAmpByKind()
        {
            Assert.Equal(0.005, Scatter.DefaultAmp(ProfileKind.Truba), 6);
            Assert.Equal(0.05, Scatter.DefaultAmp(ProfileKind.Razrabotka), 6);
            Assert.Equal(0.05, Scatter.DefaultAmp(ProfileKind.Pesok), 6);
        }

        [Fact]
        public void SeriesOfFollowsLineNotSheet()
        {
            Assert.Equal(ProfileKind.Razrabotka, Scatter.SeriesOf(HeightSrc.TrenchBottom));
            Assert.Equal(ProfileKind.Pesok, Scatter.SeriesOf(HeightSrc.SandTop));
            Assert.Equal(ProfileKind.Truba, Scatter.SeriesOf(HeightSrc.Invert));
            Assert.Equal(ProfileKind.Truba, Scatter.SeriesOf(HeightSrc.PipeTop));
            Assert.Equal(ProfileKind.Zashita, Scatter.SeriesOf(HeightSrc.ZashTop));
            Assert.Equal(ProfileKind.Zasypka, Scatter.SeriesOf(HeightSrc.Surface));
        }

        [Fact]
        public void PlanTargetsCoverLength()
        {
            var t = Scatter.PlanTargets(120, 50, 7);
            Assert.Equal(3, t.Count);
            Assert.All(t, ch => Assert.InRange(ch, 0.0, 120.0));
        }

        [Fact]
        public void FromOffsetsRoundtripsMeters()
        {
            var dz = new System.Collections.Generic.List<double> { -0.04, 0.0, 0.06 };
            var vals = Scatter.FromOffsets(dz, 0.05);
            Assert.Equal(3, vals.Count);
            Assert.Equal(-0.8, vals[0], 6);
            Assert.Equal(0.0, vals[1], 6);
            Assert.Equal(1.2, vals[2], 6);
            Assert.Equal(-0.04, Scatter.Meters(0, 10, vals, 0.05), 6);
            Assert.Equal(0.06, Scatter.Meters(10, 10, vals, 0.05), 6);
        }
    }
}
