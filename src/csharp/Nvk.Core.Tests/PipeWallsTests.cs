using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class PipeWallsTests
    {
        [Fact]
        public void Korsis250Is17mm()
        {
            var t = PipeWalls.KorsisSn8();
            Assert.Equal(0.017, PipeWalls.MetersAt(0.250, t, true), 6);
            Assert.Equal(0.017, PipeWalls.MetersAt(0.216, t, true), 6);
        }

        [Fact]
        public void DisabledIsZero()
        {
            Assert.Equal(0.0, PipeWalls.MetersAt(0.250, PipeWalls.KorsisSn8(), false), 6);
        }

        [Fact]
        public void RoundtripFormat()
        {
            var s = PipeWalls.Format(PipeWalls.KorsisSn8());
            var p = PipeWalls.Parse(s);
            Assert.Equal(11, p.Count);
            Assert.Equal(0.017, PipeWalls.MetersAt(0.25, p, true), 6);
        }

        [Fact]
        public void LineClampWallShiftsSandAndPipe()
        {
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(100, 0.216, 0.10, 0.30, 102, 102, 0.017,
                out trench, out sand, out pipe, out zash, out earth, out design);
            Assert.Equal(99.983, sand, 6);
            Assert.Equal(99.883, trench, 6);
            Assert.Equal(100.233, pipe, 6);
            Assert.Equal(0.250, pipe - sand, 6);
            Assert.Equal(100.533, zash, 6);
        }

        [Fact]
        public void DeltaZashWithWallIsCatalogPlusBothWalls()
        {
            var cfg = new SectionConfig
            {
                Bedding = 0.10,
                ThkZash = 0.30,
                UsePipeWall = true,
                Walls = PipeWalls.KorsisSn8()
            };
            var p = new AxisPt { Invert = 100.0, Diam = 0.216, EarthZ = 102.0, Surface = 102.0 };
            double thk = ProfileRows.ZAt(HeightSrc.DeltaZash, p, cfg);
            Assert.Equal(0.30 + 2 * 0.017, thk, 6);
        }
    }
}
