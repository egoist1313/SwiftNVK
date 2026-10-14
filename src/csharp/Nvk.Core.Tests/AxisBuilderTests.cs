using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class AxisBuilderTests
    {
        static PipeSeg Seg(double x0, double x1, double si, double ei, double d)
        {
            return new PipeSeg
            {
                Handle = "t",
                X0 = x0, Y0 = 0, X1 = x1, Y1 = 0,
                InvertStart = si, InvertEnd = ei, Diam = d
            };
        }

        [Fact]
        public void EndsAndStepAndInvertJump()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 10, 100, 99.9, 0.3),
                Seg(10, 20, 99.5, 99.4, 0.3)
            });
            var pts = AxisBuilder.FromChain(chain, step: 5.0);
            Assert.True(pts.Count >= 5);
            Assert.Equal(0.0, pts[0].Ch, 6);
            Assert.Equal(100.0, pts[0].Invert, 6);
            // скачок на ch=10: 99.9 затем 99.5
            int jumps = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                if (System.Math.Abs(pts[i].Ch - pts[i - 1].Ch) < 1e-6
                    && System.Math.Abs(pts[i].Invert - pts[i - 1].Invert) > 0.01)
                    jumps++;
            }
            Assert.Equal(1, jumps);
        }

        [Fact]
        public void NamedLinesMoveForward()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30 };
            var p = new AxisPt { Invert = 100, Diam = 0.3, Surface = 102 };
            Assert.Equal(99.90, p.NamedZ(NamedLine.TrenchBottom, cfg), 6);
            Assert.Equal(100.00, p.NamedZ(NamedLine.SandTop, cfg), 6);
            Assert.Equal(p.NamedZ(NamedLine.SandTop, cfg), p.NamedZ(NamedLine.TrenchBottom, cfg) + 0.10, 6);
            Assert.Equal(100.60, p.NamedZ(NamedLine.ZashTop, cfg), 6);
        }
    }
}
