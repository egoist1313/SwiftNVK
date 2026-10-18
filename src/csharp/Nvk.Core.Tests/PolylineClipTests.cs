using System;
using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class PolylineClipTests
    {
        static PolylineClip.Pt P(double x, double y) { return new PolylineClip.Pt(x, y); }

        [Fact]
        public void InsertsVertexAtCrossing()
        {
            // L6 горизонталь 202.50; L1 спускается 202.60 → 202.40, пересечение x=5.
            var earth = new List<PolylineClip.Pt> { P(0, 202.50), P(10, 202.50) };
            var trench = new List<PolylineClip.Pt> { P(0, 202.60), P(10, 202.40) };
            var c = PolylineClip.KeepAbove(earth, trench);
            Assert.True(c.Count >= 3);
            Assert.Equal(0.0, c[0].X, 6);
            Assert.Equal(202.60, c[0].Y, 6);
            bool found = false;
            foreach (var p in c)
            {
                if (p.X > 4.5 && p.X < 5.5)
                {
                    found = true;
                    Assert.Equal(5.0, p.X, 5);
                    Assert.Equal(202.50, p.Y, 5);
                }
                Assert.True(p.Y >= PolylineClipY(trench, p.X) - 1e-6);
            }
            Assert.True(found);
            Assert.Equal(10.0, c[c.Count - 1].X, 6);
            Assert.Equal(202.50, c[c.Count - 1].Y, 6);
        }

        [Fact]
        public void CrossingBetweenTwoMetreSamples()
        {
            // Как на трассе: ПК 2 ниже L1, ПК 4 выше — излом не на ПК 2, а на пересечении.
            var earth = new List<PolylineClip.Pt> { P(2, 202.502), P(4, 202.523) };
            var trench = new List<PolylineClip.Pt> { P(0, 202.576), P(5.707, 202.483) };
            var c = PolylineClip.KeepAbove(earth, trench);
            Assert.True(c.Count >= 3);
            Assert.Equal(2.0, c[0].X, 6);
            double t0 = Ylin(0, 202.576, 5.707, 202.483, 2.0);
            Assert.Equal(t0, c[0].Y, 5);
            bool mid = false;
            for (int i = 1; i < c.Count - 1; i++)
            {
                if (c[i].X > 2.01 && c[i].X < 3.99)
                {
                    mid = true;
                    double t = Ylin(0, 202.576, 5.707, 202.483, c[i].X);
                    Assert.Equal(t, c[i].Y, 4);
                }
            }
            Assert.True(mid);
        }

        [Fact]
        public void CrossingOnSecondSegmentNotFirst()
        {
            // ПК 0 и 2 ниже L1, ПК 4 выше — точка должна быть между 2 и 4, не на 2.
            var earth = new List<PolylineClip.Pt>
            {
                P(0, 202.480), P(2, 202.502), P(4, 202.523)
            };
            var trench = new List<PolylineClip.Pt>
            {
                P(0, 202.576), P(5.707, 202.483)
            };
            var c = PolylineClip.KeepAbove(earth, trench);
            double t2 = Ylin(0, 202.576, 5.707, 202.483, 2.0);
            double t4 = Ylin(0, 202.576, 5.707, 202.483, 4.0);
            Assert.True(202.502 < t2);
            Assert.True(202.523 > t4);
            bool mid = false;
            foreach (var p in c)
            {
                if (p.X > 2.05 && p.X < 3.95)
                {
                    mid = true;
                    double t = Ylin(0, 202.576, 5.707, 202.483, p.X);
                    Assert.Equal(t, p.Y, 4);
                }
            }
            Assert.True(mid);
        }

        [Fact]
        public void ZashCeilingPinsToSandWhenEarthBelow()
        {
            // ASPH: L6=202.50 < L2=202.61; каталог L4=202.91 → L4 на L2.
            var sand = new List<PolylineClip.Pt> { P(0, 202.61), P(10, 202.61) };
            var earth = new List<PolylineClip.Pt> { P(0, 202.50), P(10, 202.50) };
            var zash = new List<PolylineClip.Pt> { P(0, 202.91), P(10, 202.91) };
            var ceil = PolylineClip.KeepAbove(earth, sand);
            var c = PolylineClip.KeepBelow(zash, ceil);
            Assert.True(c.Count >= 2);
            foreach (var p in c)
                Assert.Equal(202.61, p.Y, 5);
        }

        [Fact]
        public void ZashCeilingFollowsEarthWhenEarthAboveSand()
        {
            var sand = new List<PolylineClip.Pt> { P(0, 202.16), P(10, 202.16) };
            var earth = new List<PolylineClip.Pt> { P(0, 202.53), P(10, 202.53) };
            var zash = new List<PolylineClip.Pt> { P(0, 202.91), P(10, 202.91) };
            var ceil = PolylineClip.KeepAbove(earth, sand);
            var c = PolylineClip.KeepBelow(zash, ceil);
            Assert.True(c.Count >= 2);
            foreach (var p in c)
                Assert.Equal(202.53, p.Y, 5);
        }

        [Fact]
        public void KeepBelowCutsZashToEarthWithCrossing()
        {
            // L4 прямая 203.2; L6 волна 203.0 → 203.4, пересечение x=5.
            var zash = new List<PolylineClip.Pt> { P(0, 203.2), P(10, 203.2) };
            var earth = new List<PolylineClip.Pt> { P(0, 203.0), P(10, 203.4) };
            var c = PolylineClip.KeepBelow(zash, earth);
            Assert.True(c.Count >= 3);
            Assert.Equal(203.0, c[0].Y, 5);
            bool mid = false;
            foreach (var p in c)
            {
                double e = Ylin(0, 203.0, 10, 203.4, p.X);
                Assert.True(p.Y <= e + 1e-6);
                if (p.X > 4.5 && p.X < 5.5) mid = true;
            }
            Assert.True(mid);
        }

        [Fact]
        public void OverflowStepUpKeepsBedding()
        {
            // 64а: лоток прыгает вверх 0.22 м. L1 не срезать входящим L2.
            var sand = new List<PolylineClip.Pt>
            {
                P(0, 100.00), P(10, 100.00), P(10, 100.22), P(20, 100.22)
            };
            var trench = new List<PolylineClip.Pt>
            {
                P(0, 99.90), P(10, 99.90), P(10, 100.12), P(20, 100.12)
            };
            var c = PolylineClip.KeepBelow(trench, sand);
            Assert.True(c.Count >= 4);
            double yIn = double.NaN, yOut = double.NaN;
            for (int i = 0; i < c.Count; i++)
            {
                if (Math.Abs(c[i].X - 10.0) > 1e-9) continue;
                if (double.IsNaN(yIn)) yIn = c[i].Y;
                yOut = c[i].Y;
            }
            Assert.Equal(99.90, yIn, 5);
            Assert.Equal(100.12, yOut, 5);
            Assert.Equal(0.10, 100.00 - yIn, 5);
            Assert.Equal(0.10, 100.22 - yOut, 5);
            for (int i = 1; i < c.Count; i++)
            {
                if (c[i].X - c[i - 1].X < 0.5) continue;
                double span = c[i].X - c[i - 1].X;
                double dz = Math.Abs(c[i].Y - c[i - 1].Y);
                Assert.True(dz < 0.05, "L1 не должна диагональю закрывать перелив");
                Assert.True(span > 0.5);
            }
        }

        [Fact]
        public void OverflowStepDownKeepsBedding()
        {
            var sand = new List<PolylineClip.Pt>
            {
                P(0, 100.22), P(10, 100.22), P(10, 100.00), P(20, 100.00)
            };
            var trench = new List<PolylineClip.Pt>
            {
                P(0, 100.12), P(10, 100.12), P(10, 99.90), P(20, 99.90)
            };
            var c = PolylineClip.KeepBelow(trench, sand);
            double yIn = double.NaN, yOut = double.NaN;
            for (int i = 0; i < c.Count; i++)
            {
                if (Math.Abs(c[i].X - 10.0) > 1e-9) continue;
                if (double.IsNaN(yIn)) yIn = c[i].Y;
                yOut = c[i].Y;
            }
            Assert.Equal(100.12, yIn, 5);
            Assert.Equal(99.90, yOut, 5);
        }

        [Fact]
        public void YAtVerticalStepPicksIncomingOrOutgoing()
        {
            var sand = new List<PolylineClip.Pt>
            {
                P(0, 100.00), P(10, 100.00), P(10, 100.22), P(20, 100.22)
            };
            Assert.Equal(100.00, PolylineClip.YAt(sand, 10.0, false), 5);
            Assert.Equal(100.22, PolylineClip.YAt(sand, 10.0, true), 5);
            Assert.Equal(100.00, PolylineClip.YAt(sand, 5.0, false), 5);
            Assert.Equal(100.22, PolylineClip.YAt(sand, 15.0, false), 5);
        }

        static double PolylineClipY(List<PolylineClip.Pt> trench, double x)
        {
            return Ylin(trench[0].X, trench[0].Y, trench[1].X, trench[1].Y, x);
        }

        static double Ylin(double x0, double y0, double x1, double y1, double x)
        {
            return y0 + (x - x0) / (x1 - x0) * (y1 - y0);
        }
    }
}
