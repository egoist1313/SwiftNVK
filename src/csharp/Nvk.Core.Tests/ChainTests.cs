using System;
using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class ChainTests
    {
        static PipeSeg Seg(string h, double x0, double y0, double x1, double y1, double si, double ei, double d)
        {
            return new PipeSeg
            {
                Handle = h,
                X0 = x0, Y0 = y0, X1 = x1, Y1 = y1,
                InvertStart = si, InvertEnd = ei, Diam = d
            };
        }

        [Fact]
        public void SortsFromHeadAndReversesMiddle()
        {
            // A 0→10, B 20→10 (наоборот), C 20→30. Голова A.
            var a = Seg("A", 0, 0, 10, 0, 100, 99.9, 0.5);
            var b = Seg("B", 20, 0, 10, 0, 99.8, 99.9, 0.5);
            var c = Seg("C", 20, 0, 30, 0, 99.8, 99.7, 0.6);
            // Голова A должна идти раньше B и C: у всех троих start не является чьим-то end.
            var chain = Chain.Sort(new List<PipeSeg> { a, b, c });
            Assert.Equal(3, chain.Segs.Count);
            Assert.Equal("A", chain.Segs[0].Handle);
            Assert.Equal("B", chain.Segs[1].Handle);
            Assert.Equal(10.0, chain.Segs[1].X0, 6);
            Assert.Equal(20.0, chain.Segs[1].X1, 6);
            Assert.Equal("C", chain.Segs[2].Handle);
            Assert.Equal(30.0, chain.Total, 6);
        }

        [Fact]
        public void NetLengthSubtractsManholes()
        {
            var s = Seg("A", 0, 0, 10, 0, 100, 99, 0.3);
            s.StartR = 0.5;
            s.EndR = 0.6;
            Assert.Equal(10.0, s.Length2d, 6);
            Assert.Equal(8.9, s.NetLength, 6);
        }

        [Fact]
        public void ShaftRZeroWhenNoDiameterEvenIfNamed()
        {
            var s = Seg("A", 0, 0, 10, 0, 100, 99, 0.2);
            s.StartName = "Выпуск";
            s.EndName = "64а";
            s.EndR = 0.6;
            Assert.Equal(0.0, s.ShaftR(true), 6);
            Assert.Equal(0.6, s.ShaftR(false), 6);
            Assert.True(s.ManholeR(true) > 0.05);
        }

        [Fact]
        public void InManholeIgnoresNamedZeroShaft()
        {
            var s = Seg("A", 0, 0, 10, 0, 100, 99, 0.2);
            s.StartName = "Выпуск";
            s.EndName = "64а";
            s.EndR = 0.6;
            var chain = Chain.Sort(new List<PipeSeg> { s });
            Assert.False(chain.InManhole(0.0));
            Assert.True(chain.InManhole(10.0));
            Assert.Equal(0.0, chain.ManholeRAt(0.0, false), 6);
            Assert.Equal(0.6, chain.ManholeRAt(10.0, false), 6);
        }

        [Fact]
        public void LocateMidAndEnd()
        {
            var a = Seg("A", 0, 0, 10, 0, 100, 99, 0.3);
            var chain = Chain.Sort(new List<PipeSeg> { a });
            var mid = chain.Locate(5.0);
            Assert.Equal(5.0, mid.X, 6);
            Assert.Equal(99.5, mid.Invert, 6);
            Assert.Equal(0.0, mid.Nx, 6);
            Assert.Equal(1.0, mid.Ny, 6);
            var end = chain.Locate(99.0);
            Assert.Equal(10.0, end.X, 6);
            Assert.Equal(99.0, end.Invert, 6);
        }

        [Fact]
        public void ProjectReturnsDistance()
        {
            var chain = Chain.Sort(new List<PipeSeg> { Seg("A", 0, 0, 10, 0, 100, 99, 0.3) });
            double d;
            double ch = chain.Project(5.0, 4.0, out d);
            Assert.Equal(5.0, ch, 6);
            Assert.Equal(4.0, d, 6);
            Assert.Equal(5.0, chain.Project(5.0, 4.0), 6);
        }

        [Fact]
        public void JoinsTwoPipesThroughNamedManhole()
        {
            // Civil: концы на стенке 64а, не в центре. 64а–Выпуск и 64а–64б*.
            var a = new PipeSeg
            {
                Handle = "A",
                X0 = 0, Y0 = 0, X1 = 7.237, Y1 = 0,
                InvertStart = 204.7, InvertEnd = 204.8,
                Diam = 0.110,
                StartName = "64а", EndName = "Выпуск ГП 12(4)",
                StartR = 0.6, EndR = 0
            };
            var b = new PipeSeg
            {
                Handle = "B",
                X0 = 1.0, Y0 = 0, X1 = 28.096, Y1 = 0,
                InvertStart = 204.5, InvertEnd = 204.2,
                Diam = 0.200,
                StartName = "64а", EndName = "64б*",
                StartR = 0.6, EndR = 0.5
            };
            var all = Chain.Components(new List<PipeSeg> { a, b });
            Assert.Single(all);
            Assert.Equal(2, all[0].Segs.Count);
            Assert.Equal(7.237 + 27.096, all[0].Total, 6);
        }

        [Fact]
        public void SameManholeNameFarApartStaysSplit()
        {
            var a = new PipeSeg
            {
                Handle = "A", X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.3,
                StartName = "12", StartR = 0.6
            };
            var b = new PipeSeg
            {
                Handle = "B", X0 = 100, Y0 = 0, X1 = 120, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.3,
                StartName = "12", StartR = 0.6
            };
            var all = Chain.Components(new List<PipeSeg> { a, b });
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void GrowsFromHeadWhenBothStartAtSameManhole()
        {
            var a = new PipeSeg
            {
                Handle = "A", X0 = 0, Y0 = 0, X1 = -10, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.11,
                StartName = "64а", EndName = "Выпуск", StartR = 0.6
            };
            var b = new PipeSeg
            {
                Handle = "B", X0 = 0.8, Y0 = 0, X1 = 20, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.2,
                StartName = "64а", EndName = "64б", StartR = 0.6
            };
            var chain = Chain.Sort(new List<PipeSeg> { a, b });
            Assert.Equal(2, chain.Segs.Count);
            string n0 = chain.Segs[0].StartName;
            string nLast = chain.Segs[chain.Segs.Count - 1].EndName;
            bool outToB = n0 == "Выпуск" && nLast == "64б";
            bool bToOut = n0 == "64б" && nLast == "Выпуск";
            Assert.True(outToB || bToOut);
        }

        [Fact]
        public void DifferentManholeNamesNearbyStaySplit()
        {
            var a = new PipeSeg
            {
                Handle = "A", X0 = -10, Y0 = 0, X1 = 0, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.3,
                EndName = "64а", EndR = 0.6
            };
            var b = new PipeSeg
            {
                Handle = "B", X0 = 1.0, Y0 = 0, X1 = 20, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.3,
                StartName = "64б", StartR = 0.6
            };
            var all = Chain.Components(new List<PipeSeg> { a, b });
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void JoinsByInferredRadiusWhenStartRZero()
        {
            var a = new PipeSeg
            {
                Handle = "A", X0 = 0, Y0 = 0, X1 = 8, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.11,
                StartName = "64а", EndName = "Выпуск"
            };
            var b = new PipeSeg
            {
                Handle = "B", X0 = 1.0, Y0 = 0, X1 = 28, Y1 = 0,
                InvertStart = 100, InvertEnd = 99, Diam = 0.2,
                StartName = "64а", EndName = "64б"
            };
            var all = Chain.Components(new List<PipeSeg> { a, b });
            Assert.Single(all);
            Assert.Equal(2, all[0].Segs.Count);
        }
    }

    public class StationCalcTests
    {
        static PipeSeg Seg(double x0, double x1, double d)
        {
            return new PipeSeg
            {
                Handle = d.ToString(),
                X0 = x0, Y0 = 0, X1 = x1, Y1 = 0,
                InvertStart = 100, InvertEnd = 100, Diam = d
            };
        }

        [Fact]
        public void AutoOneStationPerDiamGroup()
        {
            // 0..40 d=0.5, 40..91 d=0.6 → 20 и 65.5
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 40, 0.5),
                Seg(40, 91, 0.6)
            });
            var st = StationCalc.Auto(chain);
            Assert.Equal(2, st.Count);
            Assert.Equal(20.0, st[0], 6);
            Assert.Equal(65.5, st[1], 6);
        }

        [Fact]
        public void AutoVerticalStillOnePerDiam()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 40, 0.5),
                Seg(40, 91, 0.6)
            });
            var st = StationCalc.Auto(chain, 0.0, null);
            Assert.Equal(2, st.Count);
            Assert.Equal(20.0, st[0], 6);
            Assert.Equal(65.5, st[1], 6);
        }

        [Fact]
        public void AutoSlopedSitsOnManholeInnerWalls()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 5.707, Y1 = 0,
                    InvertStart = 100, InvertEnd = 100, Diam = 0.25,
                    StartR = 0.5, EndR = 0.5
                }
            });
            var st = StationCalc.Auto(chain, 0.5, null);
            Assert.Contains(st, s => Math.Abs(s - 0.5) < 0.02);
            Assert.Contains(st, s => Math.Abs(s - 5.207) < 0.02);
            Assert.DoesNotContain(st, s => s < 0.2);
            foreach (var s in st)
                Assert.False(chain.InManhole(s));
        }

        [Fact]
        public void InManholeIncludesCenterNotInnerWall()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 5.707, Y1 = 0,
                    InvertStart = 100, InvertEnd = 100, Diam = 0.25,
                    StartR = 0.5, EndR = 0.5
                }
            });
            Assert.True(chain.InManhole(0.0));
            Assert.True(chain.InManhole(0.05));
            Assert.False(chain.InManhole(0.5));
            Assert.False(chain.InManhole(2.0));
            Assert.True(chain.InManhole(5.657));
            Assert.True(chain.InManhole(5.707));
            Assert.False(chain.InManhole(5.207));
        }

        [Fact]
        public void ComponentsSplitsDisconnectedPipes()
        {
            var a = new PipeSeg { Handle = "A", X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 100, InvertEnd = 99, Diam = 0.3 };
            var b = new PipeSeg { Handle = "B", X0 = 100, Y0 = 0, X1 = 120, Y1 = 0, InvertStart = 100, InvertEnd = 99, Diam = 0.3 };
            var all = Chain.Components(new List<PipeSeg> { a, b });
            Assert.Equal(2, all.Count);
            Assert.Equal(10.0, all[0].Total, 6);
            Assert.Equal(20.0, all[1].Total, 6);
        }

        [Fact]
        public void AutoSlopedHasEndsOfEachDiamGroup()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 40, 0.5),
                Seg(40, 91, 0.6)
            });
            var st = StationCalc.Auto(chain, 0.5, null);
            Assert.True(st.Count >= 4);
            Assert.True(st[0] < 1.0);
            bool nearJoint = false;
            foreach (var s in st)
                if (s > 39.0 && s < 41.0) nearJoint = true;
            Assert.True(nearJoint);
            Assert.True(st[st.Count - 1] > 90.0);
        }

        [Fact]
        public void AutoSingleDiamIsMid()
        {
            var chain = Chain.Sort(new List<PipeSeg> { Seg(0, 40, 0.3), Seg(40, 80, 0.3) });
            var st = StationCalc.Auto(chain);
            Assert.Single(st);
            Assert.Equal(40.0, st[0], 6);
        }

        [Fact]
        public void ParseManual()
        {
            var chain = Chain.Sort(new List<PipeSeg> { Seg(0, 100, 0.3) });
            var st = StationCalc.Parse("10,50.5,200", chain);
            Assert.Equal(2, st.Count);
            Assert.Equal(10.0, st[0], 6);
            Assert.Equal(50.5, st[1], 6);
        }
    }

    public class SectionGeomTests
    {
        [Fact]
        public void SharedLinesAndNoDrawWhenSurfaceBelowZash()
        {
            var hit = new StationHit
            {
                Station = 12.5, X = 1, Y = 2, Invert = 100, Diam = 0.3,
                Nx = 0, Ny = 1, Slope = 0
            };
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket };
            var g = SectionGeom.From(hit, cfg, surfaceRaw: 90.0);
            Assert.Equal(g.Elev.TrenchBottom, g.Elev[LayerStack.Lower(ProfileKind.Pesok)]);
            Assert.Equal(g.Elev.SandTop, g.Elev[LayerStack.Lower(ProfileKind.Zashita)]);
            Assert.False(g.IsDrawn(ProfileKind.Zasypka));
            Assert.Equal(0.9, g.TrenchW, 6);
        }
    }
}
