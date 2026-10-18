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
        public void SortByChKeepsIncomingInvertFirst()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 10, 100, 99.9, 0.3),
                Seg(10, 20, 99.5, 99.4, 0.3)
            });
            var pts = AxisBuilder.FromChain(chain, step: 5.0);
            pts.Add(new AxisPt { Ch = 3.0, Invert = 99.97, Diam = 0.3 });
            AxisBuilder.SortByCh(pts);
            int jump = -1;
            for (int i = 1; i < pts.Count; i++)
            {
                if (System.Math.Abs(pts[i].Ch - pts[i - 1].Ch) < 1e-6
                    && System.Math.Abs(pts[i].Invert - pts[i - 1].Invert) > 0.01)
                {
                    jump = i;
                    break;
                }
            }
            Assert.True(jump > 0);
            Assert.Equal(10.0, pts[jump].Ch, 6);
            Assert.Equal(99.9, pts[jump - 1].Invert, 6);
            Assert.Equal(99.5, pts[jump].Invert, 6);
        }

        [Fact]
        public void IsDupVertexKeepsInvertStep()
        {
            Assert.True(AxisBuilder.IsDupVertex(27.096, 205.739, 27.096, 205.739));
            Assert.False(AxisBuilder.IsDupVertex(27.096, 204.630, 27.096, 204.407));
            Assert.False(AxisBuilder.IsDupVertex(29.096, 204.640, 27.096, 204.630));
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
