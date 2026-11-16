using System;
using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class WellPitGeomTests
    {
        static WellPitInput Well()
        {
            return new WellPitInput
            {
                Name = "К1",
                X = 100,
                Y = 200,
                Rim = 105,
                Sump = 100,
                Diam = 1.50,
                Height = 5.0,
                AxisX = 1,
                AxisY = 0
            };
        }

        static WellPitCfg Cfg()
        {
            return new WellPitCfg
            {
                TrenchGap = 0.50,
                SlopeM = 1.00,
                Bedding = 0.0
            };
        }

        [Fact]
        public void RadiusIsHalfDiameter()
        {
            Assert.Equal(0.75, WellPitGeom.Radius(Well()), 6);
        }

        [Fact]
        public void PitBottomIsSumpMinusBedding()
        {
            var w = Well();
            var c = Cfg();
            c.Bedding = 0.15;
            c.SlopeM = 0.0;
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            Assert.Equal(w.Sump - 0.15, s.Outer[0].Z, 6);
            Assert.Equal(w.Sump, s.Shaft[0].Z, 6);
        }

        [Fact]
        public void SlopeRaysRunFromSoleToBerm()
        {
            var s = WellPitGeom.Build(Well(), Cfg(), (x, y) => 105.0);
            Assert.True(s.SlopeRays != null && s.SlopeRays.Count >= 4);
            var ray = s.SlopeRays[0];
            Assert.Equal(2, ray.Length);
            Assert.Equal(100.0, ray[0].Z, 6);
            Assert.Equal(105.0, ray[1].Z, 3);
        }

        [Fact]
        public void PipeThroughJoinsAtWellAxisFromBerm()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipe = new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Invert1 = 99.4, Diam = 0.3 };
            List<WellPt> through, toWell;
            Assert.True(WellPitGeom.PipeLines(w, pipe, berm, 0.30, 0.30, out through, out toWell));
            Assert.Equal(0.0, through[2].X, 6);
            Assert.Equal(0.0, through[2].Y, 6);
            Assert.True(through[0].X > 4.5);
            bool onCircle = false;
            for (int i = 0; i < toWell.Count; i++)
            {
                double d = Math.Sqrt(toWell[i].X * toWell[i].X + toWell[i].Y * toWell[i].Y);
                if (Math.Abs(d - 0.5) < 0.02) onCircle = true;
            }
            Assert.True(onCircle);
            bool hitsCenter = false;
            for (int i = 0; i < toWell.Count; i++)
                if (Math.Abs(toWell[i].X) < 0.02 && Math.Abs(toWell[i].Y) < 0.02) hitsCenter = true;
            Assert.False(hitsCenter);
        }

        [Fact]
        public void WideTrenchCutsWellOutOfRectangle()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipe = new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Invert1 = 99.4, Diam = 0.3 };
            List<WellPt> through, toWell;
            Assert.True(WellPitGeom.PipeLines(w, pipe, berm, 0.80, 0.80, out through, out toWell));
            bool hitsCenter = false, hasTop = false, hasBot = false, arcInTrench = false;
            for (int i = 0; i < toWell.Count; i++)
            {
                if (Math.Abs(toWell[i].X) < 0.02 && Math.Abs(toWell[i].Y) < 0.02) hitsCenter = true;
                if (Math.Abs(toWell[i].X) < 0.05 && Math.Abs(toWell[i].Y - 0.5) < 0.05) hasTop = true;
                if (Math.Abs(toWell[i].X) < 0.05 && Math.Abs(toWell[i].Y + 0.5) < 0.05) hasBot = true;
                double d = Math.Sqrt(toWell[i].X * toWell[i].X + toWell[i].Y * toWell[i].Y);
                if (Math.Abs(d - 0.5) < 0.04 && toWell[i].X > 0.2) arcInTrench = true;
            }
            Assert.False(hitsCenter);
            Assert.True(hasTop);
            Assert.True(hasBot);
            Assert.True(arcInTrench);
        }

        [Fact]
        public void OverlappingWideTrenchesUnionDropsSharedEdge()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipes = new List<WellPipeSeg>
            {
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 },
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 0, Y1 = 20, Invert0 = 99.5, Diam = 0.3 }
            };
            var hw = new List<double> { 0.80, 0.80 };
            var loops = WellPitGeom.UnionTrenches(w, berm, pipes, hw, hw);
            Assert.True(loops.Count >= 1);
            bool sharedAxis = false;
            bool hasWellArc = false;
            bool hitsCenter = false;
            int onInternal = 0;
            for (int k = 0; k < loops.Count; k++)
            {
                var pts = loops[k].Pts;
                int nSeg = loops[k].Closed ? pts.Count : pts.Count - 1;
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    if (Math.Abs(a.X) < 0.02 && Math.Abs(a.Y) < 0.02) hitsCenter = true;
                    double da = Math.Sqrt(a.X * a.X + a.Y * a.Y);
                    if (Math.Abs(da - 0.5) < 0.06) hasWellArc = true;
                    if (OnXY(a, 0, 0.4, 0.08)) onInternal++;
                    if (i >= nSeg) continue;
                    var b = pts[(i + 1) % pts.Count];
                    if (CollinearThroughOrigin(a, b)) sharedAxis = true;
                }
            }
            Assert.False(hitsCenter);
            Assert.True(hasWellArc);
            Assert.False(sharedAxis);
            Assert.True(onInternal == 0);
        }

        [Fact]
        public void OppositeWideTrenchesMakeWellHoleAndLongSides()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipes = new List<WellPipeSeg>
            {
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 },
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = -20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 }
            };
            var hw = new List<double> { 0.80, 0.80 };
            var loops = WellPitGeom.UnionTrenches(w, berm, pipes, hw, hw);
            bool hole = false, hitsCenter = false, hasTop = false, hasBot = false, span = false;
            for (int k = 0; k < loops.Count; k++)
            {
                var pts = loops[k].Pts;
                double minD = 1e9, maxD = -1e9, minX = 1e9, maxX = -1e9;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    if (Math.Abs(p.X) < 0.02 && Math.Abs(p.Y) < 0.02) hitsCenter = true;
                    double d = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                    if (d < minD) minD = d;
                    if (d > maxD) maxD = d;
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (Math.Abs(p.Y - 0.8) < 0.12 && Math.Abs(p.X) > 4) hasTop = true;
                    if (Math.Abs(p.Y + 0.8) < 0.12 && Math.Abs(p.X) > 4) hasBot = true;
                }
                if (loops[k].Closed && pts.Count >= 20 && minD > 0.4 && maxD < 0.6) hole = true;
                if (maxX - minX > 8) span = true;
            }
            Assert.False(hitsCenter);
            Assert.True(hole);
            Assert.True(hasTop);
            Assert.True(hasBot);
            Assert.True(span);
        }

        [Fact]
        public void NarrowTrenchesDoNotInventCenterJoin()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipes = new List<WellPipeSeg>
            {
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 },
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 0, Y1 = 20, Invert0 = 99.5, Diam = 0.3 }
            };
            var hw = new List<double> { 0.30, 0.30 };
            var loops = WellPitGeom.UnionTrenches(w, berm, pipes, hw, hw);
            Assert.True(loops.Count >= 2);
            bool hitsCenter = false;
            for (int k = 0; k < loops.Count; k++)
            {
                var pts = loops[k].Pts;
                for (int i = 0; i < pts.Count; i++)
                    if (Math.Abs(pts[i].X) < 0.02 && Math.Abs(pts[i].Y) < 0.02) hitsCenter = true;
            }
            Assert.False(hitsCenter);
        }

        [Fact]
        public void TrenchRectStopsAtBerm()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipe = new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 };
            List<WellPt> rect;
            Assert.True(WellPitGeom.TrenchRect(w, pipe, berm, 0.30, 0.30, out rect));
            double maxX = -1e9;
            for (int i = 0; i < rect.Count; i++)
                if (rect[i].X > maxX) maxX = rect[i].X;
            Assert.True(maxX < 5.2);
            Assert.True(maxX > 4.5);
        }

        [Fact]
        public void RedUnionGoesThroughWellWithoutHole()
        {
            var w = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var berm = SquareBerm(5);
            var pipes = new List<WellPipeSeg>
            {
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 },
                new WellPipeSeg { X0 = 0, Y0 = 0, X1 = -20, Y1 = 0, Invert0 = 99.5, Diam = 0.3 }
            };
            var hw = new List<double> { 0.80, 0.80 };
            var blue = WellPitGeom.UnionTrenches(w, berm, pipes, hw, hw);
            var rects = new List<List<WellPt>>();
            for (int i = 0; i < pipes.Count; i++)
            {
                List<WellPt> r;
                Assert.True(WellPitGeom.TrenchRect(w, pipes[i], berm, 0.80, 0.80, out r));
                rects.Add(r);
            }
            var red = WellPitGeom.UnionRects(rects, 99.5);
            bool blueHole = false, redHole = false;
            for (int k = 0; k < blue.Count; k++)
            {
                if (!blue[k].Closed || blue[k].Pts.Count < 20) continue;
                double minD = 1e9, maxD = -1e9;
                for (int i = 0; i < blue[k].Pts.Count; i++)
                {
                    double d = Math.Sqrt(blue[k].Pts[i].X * blue[k].Pts[i].X + blue[k].Pts[i].Y * blue[k].Pts[i].Y);
                    if (d < minD) minD = d;
                    if (d > maxD) maxD = d;
                }
                if (minD > 0.4 && maxD < 0.6) blueHole = true;
            }
            for (int k = 0; k < red.Count; k++)
            {
                if (!red[k].Closed || red[k].Pts.Count < 20) continue;
                double minD = 1e9, maxD = -1e9;
                for (int i = 0; i < red[k].Pts.Count; i++)
                {
                    double d = Math.Sqrt(red[k].Pts[i].X * red[k].Pts[i].X + red[k].Pts[i].Y * red[k].Pts[i].Y);
                    if (d < minD) minD = d;
                    if (d > maxD) maxD = d;
                }
                if (minD > 0.4 && maxD < 0.6) redHole = true;
            }
            Assert.True(blueHole);
            Assert.False(redHole);
            Assert.True(WellPitGeom.PointInPoly(rects[0], 0, 0) || red.Count >= 1);
        }

        [Fact]
        public void OverlappingPitsUnionToOneBottom()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 0.0, Bedding = 0.0 };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 1.2, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            Assert.True(WellPitGeom.PolysOverlap(sa.Outer, sb.Outer));
            var u = WellPitGeom.UnionPits(new List<WellPitShape> { sa, sb }, cfg, tin, null, 105);
            Assert.NotNull(u);
            Assert.NotNull(u.Outer);
            double minX = 1e9, maxX = -1e9;
            for (int i = 0; i < u.Outer.Count; i++)
            {
                if (u.Outer[i].X < minX) minX = u.Outer[i].X;
                if (u.Outer[i].X > maxX) maxX = u.Outer[i].X;
            }
            Assert.True(maxX - minX > 2.5);
            Assert.True(u.Bottoms != null && u.Bottoms.Count == 1);
        }

        [Fact]
        public void OverlappingPitsHaveStepAtMid()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 0.0, Bedding = 0.0 };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 1.2, Y = 0, Diam = 1.0, Sump = 99, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            var u = WellPitGeom.UnionPits(new List<WellPitShape> { sa, sb }, cfg, tin, null, 105);
            Assert.NotNull(u);
            bool hasHi = false, hasLo = false, hasVert = false;
            var bot = u.Bottoms != null && u.Bottoms.Count > 0 ? u.Bottoms[0] : u.Outer;
            for (int i = 0; i < bot.Count; i++)
            {
                if (Math.Abs(bot[i].Z - 100) < 0.02) hasHi = true;
                if (Math.Abs(bot[i].Z - 99) < 0.02) hasLo = true;
                var q = bot[(i + 1) % bot.Count];
                double dxy = Math.Sqrt((q.X - bot[i].X) * (q.X - bot[i].X) + (q.Y - bot[i].Y) * (q.Y - bot[i].Y));
                if (dxy < 0.02 && Math.Abs(q.Z - bot[i].Z) > 0.5) hasVert = true;
            }
            Assert.True(hasHi);
            Assert.True(hasLo);
            Assert.True(hasVert || (u.Steps != null && u.Steps.Count >= 1));
            if (u.Steps != null && u.Steps.Count > 0)
            {
                double mx = 0.5 * (u.Steps[0][0].X + u.Steps[0][1].X);
                Assert.True(Math.Abs(mx - 0.6) < 0.35);
            }
        }

        [Fact]
        public void NearbyDisjointKeepTwoBottoms()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 0.50, Bedding = 0.0 };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 2.5, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            Assert.False(WellPitGeom.PolysOverlap(sa.Outer, sb.Outer));
            Assert.True(WellPitGeom.PolysOverlap(sa.Berm, sb.Berm));
            var u = WellPitGeom.UnionPits(new List<WellPitShape> { sa, sb }, cfg, tin, null, 105);
            Assert.NotNull(u);
            Assert.True(u.Bottoms != null && u.Bottoms.Count == 2);
            Assert.True(u.Berm != null && u.Berm.Count >= 4);
        }

        [Fact]
        public void CorridorSharesSlopesKeepsTwoBottoms()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 0.0, Bedding = 0.0 };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 4, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            List<WellPt> corridor;
            Assert.True(WellPitGeom.TrenchCorridor(
                a, new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 4, Y1 = 0, Invert0 = 100, Diam = 0.3 },
                0.80, 0.80, out corridor));
            var u = WellPitGeom.UnionPits(
                new List<WellPitShape> { sa, sb },
                new List<List<WellPt>> { corridor },
                cfg, tin, null, 105);
            Assert.NotNull(u);
            Assert.True(u.Bottoms != null && u.Bottoms.Count == 2);
            Assert.True(u.Berm != null && u.Berm.Count >= 4);
        }

        [Fact]
        public void DisjointSlopesMeetAtValley()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 1.0, Bedding = 0.0, CornerFan = true };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 2.5, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            Assert.False(WellPitGeom.PolysOverlap(sa.Outer, sb.Outer));
            Assert.True(WellPitGeom.PolysOverlap(sa.Berm, sb.Berm));
            var u = WellPitGeom.UnionPits(new List<WellPitShape> { sa, sb }, cfg, tin, null, 105);
            Assert.NotNull(u);
            Assert.True(u.Bottoms != null && u.Bottoms.Count == 2);
            Assert.True(u.SlopeRays != null);
            bool valley = false;
            for (int i = 0; i < u.SlopeRays.Count; i++)
            {
                if (u.SlopeRays[i] == null || u.SlopeRays[i].Length < 2) continue;
                var p = u.SlopeRays[i][0];
                var g = u.SlopeRays[i][1];
                if (Math.Abs(p.X - 1.0) > 0.08 || Math.Abs(p.Y) > 0.8) continue;
                Assert.True(g.X <= 1.45, "inner slope crossed the neighbor pad");
                if (g.X > 1.05 && g.X < 1.45 && g.Z < 104.0) valley = true;
            }
            Assert.True(valley);
            double minX = 1e9, maxX = -1e9;
            var berm = u.Berm;
            for (int i = 0; i < berm.Count; i++)
            {
                if (berm[i].X < minX) minX = berm[i].X;
                if (berm[i].X > maxX) maxX = berm[i].X;
            }
            Assert.True(minX < -0.5);
            Assert.True(maxX > 3.0);
        }

        [Fact]
        public void CorridorDoesNotTreatShoulderAsCorner()
        {
            Func<double, double, double> tin = (x, y) => 105.0;
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 4, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            List<WellPt> corridor;
            Assert.True(WellPitGeom.TrenchCorridor(
                a, new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 4, Y1 = 0, Invert0 = 100, Diam = 0.3 },
                0.80, 0.80, out corridor));
            bool fanOk = NoShoulderRays(a, b, corridor, tin, true);
            bool rectOk = NoShoulderRays(a, b, corridor, tin, false);
            Assert.True(fanOk);
            Assert.True(rectOk);
        }

        static bool NoShoulderRays(WellPitInput a, WellPitInput b, List<WellPt> corridor,
            Func<double, double, double> tin, bool fan)
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 1.0, Bedding = 0.0, CornerFan = fan };
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            var u = WellPitGeom.UnionPits(
                new List<WellPitShape> { sa, sb },
                new List<List<WellPt>> { corridor },
                cfg, tin, null, 105);
            if (u == null || u.SlopeRays == null) return false;
            bool hasOuter = false;
            for (int i = 0; i < u.SlopeRays.Count; i++)
            {
                if (u.SlopeRays[i] == null || u.SlopeRays[i].Length < 2) continue;
                var p = u.SlopeRays[i][0];
                var g = u.SlopeRays[i][1];
                double dx = Math.Abs(g.X - p.X);
                double dy = Math.Abs(g.Y - p.Y);
                bool diag = dx > 1.0 && dy > 1.0;
                if (!diag) continue;
                if (IsShoulder(p.X, p.Y, 1.0, 0.80) || IsShoulder(p.X, p.Y, 3.0, 0.80))
                    return false;
                if (IsShoulder(p.X, p.Y, 1.0, 1.0) || IsShoulder(p.X, p.Y, 3.0, 1.0))
                    return false;
                if (IsShoulder(p.X, p.Y, -1.0, 1.0) || IsShoulder(p.X, p.Y, 5.0, 1.0)
                    || IsShoulder(p.X, p.Y, -1.0, -1.0) || IsShoulder(p.X, p.Y, 5.0, -1.0))
                    hasOuter = true;
            }
            return hasOuter;
        }

        static bool IsShoulder(double x, double y, double sx, double sy)
        {
            return Math.Abs(x - sx) < 0.08 && Math.Abs(Math.Abs(y) - sy) < 0.08;
        }

        [Fact]
        public void TwoWellsOnPipeStayParallelNoCross()
        {
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100 };
            var b = new WellPitInput { X = 4, Y = 0, Diam = 1.0, Sump = 100 };
            var pa = new WellPipeSeg { X0 = 0, Y0 = 0, X1 = 4, Y1 = 0, Invert0 = 99.5, Diam = 0.3, FarR = 0.5 };
            var pb = new WellPipeSeg { X0 = 4, Y0 = 0, X1 = 0, Y1 = 0, Invert0 = 99.5, Diam = 0.3, FarR = 0.5 };
            List<WellPt> ra, rb;
            Assert.True(WellPitGeom.TrenchRect(a, pa, SquareBermAt(0, 0, 3), 0.80, 0.80, out ra));
            Assert.True(WellPitGeom.TrenchRect(b, pb, SquareBermAt(4, 0, 3), 0.80, 0.80, out rb));
            var loops = WellPitGeom.UnionTrenchesAll(
                new List<List<WellPt>> { ra, rb },
                new[] { 0.0, 4.0 }, new[] { 0.0, 0.0 }, new[] { 0.5, 0.5 }, 99.5);
            Assert.True(loops.Count >= 1);
            bool arcA = false, arcB = false, longTop = false, longBot = false, cross = false, hitsCenter = false;
            double topMin = 1e9, topMax = -1e9, botMin = 1e9, botMax = -1e9;
            for (int k = 0; k < loops.Count; k++)
            {
                var pts = loops[k].Pts;
                int nSeg = loops[k].Closed ? pts.Count : pts.Count - 1;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    if (Math.Abs(p.X) < 0.02 && Math.Abs(p.Y) < 0.02) hitsCenter = true;
                    if (Math.Abs(p.X - 4) < 0.02 && Math.Abs(p.Y) < 0.02) hitsCenter = true;
                    double da = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                    double db = Math.Sqrt((p.X - 4) * (p.X - 4) + p.Y * p.Y);
                    if (Math.Abs(da - 0.5) < 0.08 && p.X > 0.05) arcA = true;
                    if (Math.Abs(db - 0.5) < 0.08 && p.X < 3.95) arcB = true;
                    if (Math.Abs(p.Y - 0.8) < 0.15)
                    {
                        if (p.X < topMin) topMin = p.X;
                        if (p.X > topMax) topMax = p.X;
                    }
                    if (Math.Abs(p.Y + 0.8) < 0.15)
                    {
                        if (p.X < botMin) botMin = p.X;
                        if (p.X > botMax) botMax = p.X;
                    }
                    if (i >= nSeg) continue;
                    var q = pts[(i + 1) % pts.Count];
                    double mx = 0.5 * (p.X + q.X);
                    if (mx > 1.0 && mx < 3.0 && p.Y * q.Y < -0.2)
                        cross = true;
                }
            }
            if (topMax - topMin > 2) longTop = true;
            if (botMax - botMin > 2) longBot = true;
            Assert.False(hitsCenter);
            Assert.False(cross);
            Assert.True(arcA);
            Assert.True(arcB);
            Assert.True(longTop);
            Assert.True(longBot);
        }

        static bool OnXY(WellPt p, double x, double y, double e)
        {
            return Math.Abs(p.X - x) < e && Math.Abs(p.Y - y) < e;
        }

        static bool CollinearThroughOrigin(WellPt a, WellPt b)
        {
            if (Math.Abs(a.X) < 0.05 && Math.Abs(b.X) < 0.05
                && a.Y * b.Y < -0.05)
                return true;
            if (Math.Abs(a.Y) < 0.05 && Math.Abs(b.Y) < 0.05
                && a.X * b.X < -0.05)
                return true;
            return false;
        }

        static List<WellPt> SquareBerm(double h)
        {
            return SquareBermAt(0, 0, h);
        }

        static List<WellPt> SquareBermAt(double cx, double cy, double h)
        {
            return new List<WellPt>
            {
                new WellPt(cx - h, cy - h, 105),
                new WellPt(cx + h, cy - h, 105),
                new WellPt(cx + h, cy + h, 105),
                new WellPt(cx - h, cy + h, 105)
            };
        }

        [Fact]
        public void BottomHalfIsRadiusPlusOffset()
        {
            var w = new WellPitInput { Diam = 1.00 };
            var c = new WellPitCfg { TrenchGap = 0.30 };
            Assert.Equal(0.80, WellPitGeom.BottomHalf(w, c), 6);
        }

        [Fact]
        public void SandHalfIsRadiusPlusGap()
        {
            var w = new WellPitInput { Diam = 1.00 };
            var c = new WellPitCfg { TrenchGap = 0.50, SandGap = 0.20 };
            Assert.Equal(0.70, WellPitGeom.SandHalf(w, c), 6);
        }

        [Fact]
        public void SandHalfClampedToPit()
        {
            var w = new WellPitInput { Diam = 1.00 };
            var c = new WellPitCfg { TrenchGap = 0.15, SandGap = 0.40 };
            Assert.Equal(0.65, WellPitGeom.SandHalf(w, c), 6);
            Assert.Equal(WellPitGeom.BottomHalf(w, c), WellPitGeom.SandHalf(w, c), 6);
        }

        [Fact]
        public void SandRectSideIsDiameterPlusTwiceGap()
        {
            var w = Well();
            w.AxisX = 1;
            w.AxisY = 0;
            var c = new WellPitCfg { TrenchGap = 0.50, SandGap = 0.20 };
            var r = WellPitGeom.SandRect(w, c);
            Assert.Equal(4, r.Count);
            double half = WellPitGeom.SandHalf(w, c);
            Assert.Equal(w.Diam + 2.0 * c.SandGap, 2.0 * half, 6);
            Assert.Equal(w.Sump, r[0].Z, 6);
            double minX = r[0].X, maxX = r[0].X, minY = r[0].Y, maxY = r[0].Y;
            for (int i = 1; i < r.Count; i++)
            {
                if (r[i].X < minX) minX = r[i].X;
                if (r[i].X > maxX) maxX = r[i].X;
                if (r[i].Y < minY) minY = r[i].Y;
                if (r[i].Y > maxY) maxY = r[i].Y;
            }
            Assert.Equal(2.0 * half, maxX - minX, 6);
            Assert.Equal(2.0 * half, maxY - minY, 6);
            Assert.Equal(w.X, 0.5 * (minX + maxX), 6);
            Assert.Equal(w.Y, 0.5 * (minY + maxY), 6);
        }

        [Fact]
        public void SandRectFollowsPitAxis()
        {
            var w = Well();
            w.AxisX = 0;
            w.AxisY = 1;
            var c = new WellPitCfg { TrenchGap = 0.50, SandGap = 0.20 };
            var r = WellPitGeom.SandRect(w, c);
            double half = WellPitGeom.SandHalf(w, c);
            var pit = WellPitGeom.Build(w, c, (x, y) => w.Rim);
            Assert.Equal(4, r.Count);
            Assert.Equal(w.X - half, r[0].X, 6);
            Assert.Equal(w.Y + half, r[0].Y, 6);
            Assert.Equal(0.0, pit.Ax, 6);
            Assert.Equal(1.0, pit.Ay, 6);
        }

        [Fact]
        public void AlongHalfIsRadiusPlusTrenchGap()
        {
            Assert.Equal(1.25, WellPitGeom.AlongHalf(Well(), Cfg()), 6);
        }

        [Fact]
        public void AcrossHalfEqualsAlongHalf()
        {
            Assert.Equal(1.25, WellPitGeom.AcrossHalf(Well(), Cfg()), 6);
            Assert.Equal(
                WellPitGeom.AlongHalf(Well(), Cfg()),
                WellPitGeom.AcrossHalf(Well(), Cfg()),
                6);
        }

        [Fact]
        public void AxisUnitNormalizes()
        {
            var w = Well();
            w.AxisX = 3;
            w.AxisY = 4;
            double ax, ay;
            WellPitGeom.AxisUnit(w, out ax, out ay);
            Assert.Equal(0.6, ax, 6);
            Assert.Equal(0.8, ay, 6);
        }

        [Fact]
        public void CombineDirsFlipsOppositeThroughWell()
        {
            double ax, ay;
            WellPitGeom.CombineDirs(
                new[] { 10.0, -8.0 },
                new[] { 0.0, 0.0 },
                out ax, out ay);
            Assert.Equal(1.0, ax, 6);
            Assert.Equal(0.0, ay, 6);
        }

        [Fact]
        public void CombineDirsFollowsLongestPipe()
        {
            double ax, ay;
            WellPitGeom.CombineDirs(
                new[] { 1.0, 20.0 },
                new[] { 0.0, 20.0 },
                out ax, out ay);
            Assert.True(ax > 0.4);
            Assert.True(ay > 0.4);
        }

        [Fact]
        public void AxisFromOutgoingIgnoresTeeBranch()
        {
            double ax, ay;
            WellPitGeom.AxisFromOutgoing(
                new[] { 20.0, -18.0, 0.0 },
                new[] { 0.0, 0.0, 8.0 },
                out ax, out ay);
            Assert.True(Math.Abs(ax) > 0.98);
            Assert.True(Math.Abs(ay) < 0.15);
        }

        [Fact]
        public void AxisFromOutgoingBendFollowsLongestPipe()
        {
            double ax, ay;
            WellPitGeom.AxisFromOutgoing(
                new[] { 12.0, 0.0 },
                new[] { 0.0, 5.0 },
                out ax, out ay);
            Assert.Equal(1.0, ax, 6);
            Assert.Equal(0.0, ay, 6);
        }

        [Fact]
        public void AxisFromRectRecoversBuildAxis()
        {
            var w = Well();
            w.AxisX = 0;
            w.AxisY = 1;
            var s = WellPitGeom.Build(w, Cfg(), (x, y) => 105.0);
            var xs = new double[4];
            var ys = new double[4];
            for (int i = 0; i < 4; i++)
            {
                xs[i] = s.Outer[i].X;
                ys[i] = s.Outer[i].Y;
            }
            double ax, ay;
            WellPitGeom.AxisFromRect(xs, ys, out ax, out ay);
            Assert.True(Math.Abs(ax) < 0.05);
            Assert.True(Math.Abs(ay) > 0.98);
        }

        [Fact]
        public void AxisFromOutgoingSinglePipe()
        {
            double ax, ay;
            WellPitGeom.AxisFromOutgoing(new[] { 0.0 }, new[] { 12.0 }, out ax, out ay);
            Assert.Equal(0.0, ax, 6);
            Assert.Equal(1.0, ay, 6);
        }

        [Fact]
        public void TangentFromNormalIsPipeDirection()
        {
            double ax, ay;
            WellPitGeom.TangentFromNormal(-0.8, 0.6, out ax, out ay);
            Assert.Equal(0.6, ax, 6);
            Assert.Equal(0.8, ay, 6);
        }

        [Fact]
        public void SquareFollowsNorthAxis()
        {
            var w = Well();
            w.AxisX = 0;
            w.AxisY = 1;
            var c = Cfg();
            c.TrenchGap = 0.30;
            c.SlopeM = 0;
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            Assert.Equal(0.0, s.Ax, 6);
            Assert.Equal(1.0, s.Ay, 6);
            Assert.Equal(s.Outer[0].Y, s.Outer[1].Y, 6);
            Assert.True(s.Outer[0].Y > w.Y);
        }

        [Fact]
        public void PerpIsLeftOfAxis()
        {
            double px, py;
            WellPitGeom.PerpOf(1, 0, out px, out py);
            Assert.Equal(0.0, px, 6);
            Assert.Equal(1.0, py, 6);
        }

        [Fact]
        public void BermOffsetIsSlopeTimesDepth()
        {
            Assert.Equal(5.0, WellPitGeom.BermOffset(1.0, 105, 100), 6);
            Assert.Equal(0.0, WellPitGeom.BermOffset(0.0, 105, 100), 6);
            Assert.Equal(0.0, WellPitGeom.BermOffset(1.0, 99, 100), 6);
        }

        [Fact]
        public void RectRotatesWithAxis()
        {
            var pts = WellPitGeom.Rect(0, 0, 10, 0, 1, -1, 0, 2, 1);
            Assert.Equal(4, pts.Count);
            Assert.Equal(-1.0, pts[0].X, 6);
            Assert.Equal(2.0, pts[0].Y, 6);
            Assert.Equal(1.0, pts[1].X, 6);
            Assert.Equal(2.0, pts[1].Y, 6);
        }

        [Fact]
        public void BuildSquareHasNoInnerFrame()
        {
            var s = WellPitGeom.Build(Well(), Cfg(), (x, y) => 105.0);
            Assert.False(s.HasInner);
            Assert.Equal(4, s.Outer.Count);
            Assert.True(s.Berm.Count >= 8);
            Assert.True(s.Faces != null && s.Faces.Count >= 8);
            Assert.Equal(4, s.Section.Count);
            Assert.Equal(1.25, s.AcrossHalf, 6);
            Assert.Equal(s.AlongHalf, s.AcrossHalf, 6);
        }

        [Fact]
        public void CornerBisectorIs45ForSquare()
        {
            double bx, by, run;
            WellPitGeom.CornerBisector(0, 1, 1, 0, out bx, out by, out run);
            Assert.Equal(Math.Sqrt(0.5), bx, 6);
            Assert.Equal(Math.Sqrt(0.5), by, 6);
            Assert.Equal(Math.Sqrt(2.0), run, 6);
        }

        [Fact]
        public void CornerRaysAre120FromSides()
        {
            double r1x, r1y, r2x, r2y;
            WellPitGeom.CornerRayDirs(0, 1, 1, 0, out r1x, out r1y, out r2x, out r2y);
            Assert.Equal(0.5, r1x, 6);
            Assert.Equal(Math.Sqrt(3.0) / 2.0, r1y, 6);
            Assert.Equal(Math.Sqrt(3.0) / 2.0, r2x, 6);
            Assert.Equal(0.5, r2y, 6);
        }

        [Fact]
        public void BermCornerIsOffsetSquareNotArc()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            w.Diam = 1.0;
            w.Sump = 100;
            w.Rim = 105;
            var c = new WellPitCfg { TrenchGap = 0.50, SlopeM = 1.0, Bedding = 0.0, CornerFan = false };
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            double half = s.AlongHalf;
            bool hit45 = false, hit30 = false, hit60 = false;
            for (int i = 0; i < s.Berm.Count; i++)
            {
                double dx = s.Berm[i].X - half;
                double dy = s.Berm[i].Y - half;
                double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (Math.Abs(s.Berm[i].X - (half + 5.0)) < 0.08
                    && Math.Abs(s.Berm[i].Y - (half + 5.0)) < 0.08)
                    hit45 = true;
                double run = Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(run - 5.0) > 0.08) continue;
                if (Math.Abs(ang - 30.0) < 3.0) hit30 = true;
                if (Math.Abs(ang - 60.0) < 3.0) hit60 = true;
            }
            Assert.True(hit45);
            Assert.False(hit30);
            Assert.False(hit60);
        }

        [Fact]
        public void CornerRaysRoundBermOnFlatTin()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            w.Diam = 1.0;
            w.Sump = 100;
            w.Rim = 105;
            var c = new WellPitCfg { TrenchGap = 0.50, SlopeM = 1.0, Bedding = 0.0, CornerFan = true };
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            double half = s.AlongHalf;
            var corner = new WellPt(half, half, 100);
            bool hit30 = false, hit60 = false;
            for (int i = 0; i < s.Berm.Count; i++)
            {
                double dx = s.Berm[i].X - corner.X;
                double dy = s.Berm[i].Y - corner.Y;
                double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                double run = Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(run - 5.0) > 0.08) continue;
                if (Math.Abs(ang - 30.0) < 3.0) hit30 = true;
                if (Math.Abs(ang - 60.0) < 3.0) hit60 = true;
            }
            Assert.True(hit30);
            Assert.True(hit60);
        }

        [Fact]
        public void GradeAlongFlatTinHitsSlope()
        {
            var p = WellPitGeom.GradeAlong(0, 0, 100, 1, 0, 1.0, (x, y) => 105.0, 105.0, 1.0);
            Assert.Equal(5.0, p.X, 3);
            Assert.Equal(0.0, p.Y, 6);
            Assert.Equal(105.0, p.Z, 3);
        }

        [Fact]
        public void VerticalWallsKeepBermOnOuter()
        {
            var c = Cfg();
            c.SlopeM = 0.0;
            var s = WellPitGeom.Build(Well(), c, (x, y) => 105.0);
            Assert.Equal(s.AlongHalf, s.BermAlong, 6);
            Assert.Equal(s.AcrossHalf, s.BermAcross, 6);
            Assert.Equal(s.Outer[0].X, s.Berm[0].X, 6);
            Assert.Equal(s.Outer[0].Y, s.Berm[0].Y, 6);
            Assert.Equal(105.0, s.Berm[0].Z, 6);
        }

        [Fact]
        public void SlopePushesBermOut()
        {
            var s = WellPitGeom.Build(Well(), Cfg(), (x, y) => 105.0);
            Assert.Equal(s.AlongHalf + 5.0, s.BermAlong, 2);
            Assert.Equal(s.AcrossHalf + 5.0, s.BermAcross, 2);
        }

        [Fact]
        public void SectionIsPerpTrapezoid()
        {
            var w = Well();
            var s = WellPitGeom.Build(w, Cfg(), (x, y) => 105.0);
            Assert.Equal(w.Y + s.BermAcross, s.Section[0].Y, 3);
            Assert.Equal(w.X, s.Section[0].X, 3);
            Assert.Equal(105.0, s.Section[0].Z, 3);
            Assert.Equal(w.Y + s.AcrossHalf, s.Section[1].Y, 6);
            Assert.Equal(100.0, s.Section[1].Z, 6);
            Assert.Equal(w.Y - s.AcrossHalf, s.Section[2].Y, 6);
            Assert.Equal(w.Y - s.BermAcross, s.Section[3].Y, 3);
        }

        [Fact]
        public void ShaftWidthIsDiameter()
        {
            var w = Well();
            var s = WellPitGeom.Build(w, Cfg(), (x, y) => 105.0);
            double dy = s.Shaft[0].Y - s.Shaft[1].Y;
            Assert.Equal(w.Diam, dy, 6);
            Assert.Equal(w.Sump, s.Shaft[0].Z, 6);
            Assert.Equal(w.Rim, s.Shaft[2].Z, 6);
        }

        [Fact]
        public void BermOnEdgeHitsTinRidgeBreak()
        {
            var a = new WellPt(0, 0, 100);
            var b = new WellPt(10, 0, 100);
            var tris = RidgeTris();
            Func<double, double, double> tin = (x, y) =>
            {
                if (x <= 3.1) return 105.0 + x * 3.0 / 3.1;
                return 108.0 + (x - 3.1) * (-3.0) / 6.9;
            };
            var berm = WellPitGeom.BermOnEdge(a, b, 100, 1.0, tin, tris);
            Assert.True(berm.Count >= 3);
            bool hit = false;
            for (int i = 0; i < berm.Count; i++)
            {
                if (Math.Abs(berm[i].X - 3.1) < 0.05 && Math.Abs(berm[i].Y - 8.0) < 0.15)
                    hit = true;
            }
            Assert.True(hit);
        }

        [Fact]
        public void UniformSamplesMissRidgeBreak()
        {
            var a = new WellPt(0, 0, 100);
            var b = new WellPt(10, 0, 100);
            Func<double, double, double> tin = (x, y) =>
            {
                if (x <= 3.1) return 105.0 + x * 3.0 / 3.1;
                return 108.0 + (x - 3.1) * (-3.0) / 6.9;
            };
            var berm = WellPitGeom.BermOnEdge(a, b, 100, 1.0, tin, null);
            bool hit = false;
            for (int i = 0; i < berm.Count; i++)
            {
                if (Math.Abs(berm[i].X - 3.1) < 0.05) hit = true;
            }
            Assert.False(hit);
        }

        [Fact]
        public void BuildBermIncludesTinBreak()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            w.Sump = 100;
            w.Rim = 105;
            w.Diam = 1.0;
            w.AxisX = 1;
            w.AxisY = 0;
            var c = new WellPitCfg { TrenchGap = 0.50, SlopeM = 1.0, Bedding = 0.0 };
            var tris = new List<TinTri>
            {
                new TinTri(-2, 1, 105, 0.3, 1, 108, 0.3, 20, 108),
                new TinTri(-2, 1, 105, 0.3, 20, 108, -2, 20, 105),
                new TinTri(0.3, 1, 108, 2, 1, 105, 2, 20, 105),
                new TinTri(0.3, 1, 108, 2, 20, 105, 0.3, 20, 108)
            };
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0, tris);
            bool hit = false;
            for (int i = 0; i < s.Berm.Count; i++)
            {
                if (Math.Abs(s.Berm[i].X - 0.3) < 0.08 && Math.Abs(s.Berm[i].Y - 9.0) < 0.2)
                    hit = true;
            }
            Assert.True(hit);
        }

        static List<TinTri> RidgeTris()
        {
            return new List<TinTri>
            {
                new TinTri(0, 0, 105, 3.1, 0, 108, 3.1, 20, 108),
                new TinTri(0, 0, 105, 3.1, 20, 108, 0, 20, 105),
                new TinTri(3.1, 0, 108, 10, 0, 105, 10, 20, 105),
                new TinTri(3.1, 0, 108, 10, 20, 105, 3.1, 20, 108)
            };
        }

        [Fact]
        public void SectionFlatSitsAlongAxis()
        {
            var w = Well();
            var s = WellPitGeom.Build(w, Cfg(), (x, y) => 105.0);
            var flat = WellPitGeom.SectionFlat(w, s, 1.0, 2.0);
            Assert.Equal(4, flat.Count);
            Assert.True(flat[0].X > w.X);
            Assert.Equal(0.0, flat[0].Z, 6);
        }

        [Fact]
        public void TickStepAt500IsOneAndHalfMeters()
        {
            Assert.Equal(1.5, WellPitGeom.TickStepM(500, 3.0), 6);
            Assert.Equal(0.6, WellPitGeom.TickStepM(200, 3.0), 6);
            Assert.Equal(3.0, WellPitGeom.TickStepM(1000, 3.0), 6);
        }

        [Fact]
        public void SlopeTicksStartOnBermGoInward()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            var c = Cfg();
            c.SlopeScaleN = 500;
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            Assert.True(s.SlopeTicks != null && s.SlopeTicks.Count >= 8);
            for (int i = 0; i < s.SlopeTicks.Count; i++)
            {
                var t = s.SlopeTicks[i];
                Assert.Equal(2, t.Length);
                Assert.Equal(105.0, t[0].Z, 1);
                Assert.True(t[1].Z < t[0].Z - 0.2);
                double d0 = Math.Sqrt(t[0].X * t[0].X + t[0].Y * t[0].Y);
                double d1 = Math.Sqrt(t[1].X * t[1].X + t[1].Y * t[1].Y);
                Assert.True(d1 < d0 - 0.05);
                Assert.False(WellPitGeom.PointInPoly(s.Outer, t[0].X, t[0].Y));
                Assert.False(WellPitGeom.PointInPoly(s.Outer, t[1].X, t[1].Y));
            }
        }

        [Fact]
        public void SlopeTicksAlternateLongShort()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            var c = Cfg();
            c.SlopeScaleN = 500;
            var s = WellPitGeom.Build(w, c, (x, y) => 105.0);
            var t = s.SlopeTicks;
            Assert.True(t != null && t.Count >= 4);
            double longL = DistXy(t[0][0], t[0][1]);
            double shortL = DistXy(t[1][0], t[1][1]);
            Assert.True(longL > shortL * 2.0);
            Assert.True(shortL > 0.2);
            double long2 = DistXy(t[2][0], t[2][1]);
            Assert.True(Math.Abs(long2 - longL) < longL * 0.35);
        }

        [Fact]
        public void FinerScaleMakesDenserTicks()
        {
            var w = Well();
            w.X = 0;
            w.Y = 0;
            var c200 = Cfg();
            c200.SlopeScaleN = 200;
            var c1000 = Cfg();
            c1000.SlopeScaleN = 1000;
            var a = WellPitGeom.Build(w, c200, (x, y) => 105.0);
            var b = WellPitGeom.Build(w, c1000, (x, y) => 105.0);
            Assert.True(a.SlopeTicks.Count > b.SlopeTicks.Count);
        }

        [Fact]
        public void VerticalSlopeHasNoTicks()
        {
            var c = Cfg();
            c.SlopeM = 0.0;
            var s = WellPitGeom.Build(Well(), c, (x, y) => 105.0);
            Assert.True(s.SlopeTicks == null || s.SlopeTicks.Count == 0);
        }

        [Fact]
        public void JoinOnBermOverlapConnectsBottomsByExtremePoints()
        {
            var cfg = new WellPitCfg { TrenchGap = 0.50, SlopeM = 0.50, Bedding = 0.0, JoinOnBermOverlap = true };
            var a = new WellPitInput { X = 0, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            var b = new WellPitInput { X = 2.5, Y = 0, Diam = 1.0, Sump = 100, Rim = 105, AxisX = 1, AxisY = 0 };
            Func<double, double, double> tin = (x, y) => 105.0;
            var sa = WellPitGeom.Build(a, cfg, tin);
            var sb = WellPitGeom.Build(b, cfg, tin);
            Assert.False(WellPitGeom.PolysOverlap(sa.Outer, sb.Outer));
            Assert.True(WellPitGeom.PolysOverlap(sa.Berm, sb.Berm));
            var u = WellPitGeom.UnionPits(new List<WellPitShape> { sa, sb }, cfg, tin, null, 105);
            Assert.NotNull(u);
            Assert.True(u.Bottoms != null && u.Bottoms.Count == 1);
            double minX = 1e9, maxX = -1e9;
            var bot = u.Bottoms[0];
            for (int i = 0; i < bot.Count; i++)
            {
                if (bot[i].X < minX) minX = bot[i].X;
                if (bot[i].X > maxX) maxX = bot[i].X;
            }
            Assert.True(maxX - minX > 3.0);
        }

        [Fact]
        public void ConnectByExtremePointsIsHullOfTwoSquares()
        {
            var a = new List<WellPt>
            {
                new WellPt(-1, -1, 100), new WellPt(1, -1, 100),
                new WellPt(1, 1, 100), new WellPt(-1, 1, 100)
            };
            var b = new List<WellPt>
            {
                new WellPt(1.5, -1, 100), new WellPt(3.5, -1, 100),
                new WellPt(3.5, 1, 100), new WellPt(1.5, 1, 100)
            };
            var h = WellPitGeom.ConnectByExtremePoints(new List<List<WellPt>> { a, b });
            Assert.True(h.Count >= 4);
            double minX = 1e9, maxX = -1e9;
            for (int i = 0; i < h.Count; i++)
            {
                if (h[i].X < minX) minX = h[i].X;
                if (h[i].X > maxX) maxX = h[i].X;
            }
            Assert.True(minX < -0.9);
            Assert.True(maxX > 3.4);
        }

        [Fact]
        public void OpenFenceStopsBermAndSlope()
        {
            var cfg = Cfg();
            cfg.SlopeM = 1.0;
            cfg.Fence = new List<WellPt> { new WellPt(2.0, -20.0, 0), new WellPt(2.0, 20.0, 0) };
            cfg.FenceClosed = false;
            var w = Well();
            w.X = 0;
            w.Y = 0;
            var s = WellPitGeom.Build(w, cfg, (x, y) => 105.0);
            Assert.True(s.Berm != null && s.Berm.Count >= 4);
            for (int i = 0; i < s.Berm.Count; i++)
                Assert.True(s.Berm[i].X < 2.08, "berm crossed the fence");
            if (s.SlopeRays != null)
            {
                for (int i = 0; i < s.SlopeRays.Count; i++)
                {
                    if (s.SlopeRays[i] == null || s.SlopeRays[i].Length < 2) continue;
                    Assert.True(s.SlopeRays[i][1].X < 2.08, "slope crossed the fence");
                }
            }
        }

        [Fact]
        public void ClosedFenceKeepsBermInside()
        {
            var cfg = Cfg();
            cfg.SlopeM = 1.0;
            cfg.Fence = new List<WellPt>
            {
                new WellPt(-3, -3, 0), new WellPt(3, -3, 0),
                new WellPt(3, 3, 0), new WellPt(-3, 3, 0)
            };
            cfg.FenceClosed = true;
            var w = Well();
            w.X = 0;
            w.Y = 0;
            var s = WellPitGeom.Build(w, cfg, (x, y) => 105.0);
            Assert.True(s.Berm != null && s.Berm.Count >= 4);
            for (int i = 0; i < s.Berm.Count; i++)
            {
                Assert.True(s.Berm[i].X > -3.08 && s.Berm[i].X < 3.08);
                Assert.True(s.Berm[i].Y > -3.08 && s.Berm[i].Y < 3.08);
            }
        }

        static double DistXy(WellPt a, WellPt b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
