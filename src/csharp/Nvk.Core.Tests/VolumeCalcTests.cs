using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class VolumeCalcTests
    {
        static Chain OnePipe()
        {
            return Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 100, InvertEnd = 100, Diam = 0.3
                }
            });
        }

        static List<AxisPt> FlatSurf(double zSurf)
        {
            return new List<AxisPt>
            {
                new AxisPt { Ch = 0, X = 0, Y = 0, Invert = 100, Diam = 0.3, Surface = zSurf },
                new AxisPt { Ch = 10, X = 10, Y = 0, Invert = 100, Diam = 0.3, Surface = zSurf }
            };
        }

        [Fact]
        public void SandIsBeddingTimesLengthTimesWidth()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Pesok, cfg);
            Assert.Single(rows);
            double B = TrenchWidth.For(0.3, JointType.Socket); // 0.9
            Assert.Equal(0.10 * 10.0 * B, rows[0].Volume, 6);
        }

        [Fact]
        public void ProtectionSubtractsPipeBody()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Zashita, cfg);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            double expect = (0.3 + 0.30) * 10.0 * B - System.Math.PI * 0.15 * 0.15 * 10.0;
            Assert.Equal(expect, rows[0].Volume, 6);
        }

        [Fact]
        public void ExcavationIsDepthTimesLengthTimesWidth()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Razrabotka, cfg);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            // trench bottom = 99.9, surf=102, depth=2.1
            Assert.Equal(2.1 * 10.0 * B, rows[0].Volume, 5);
        }

        [Fact]
        public void VerticalSandAreaIsProfileBand()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Pesok, cfg);
            Assert.Equal(0.10 * 10.0, rows[0].Area, 6);
        }

        [Fact]
        public void SlopedExcavationUsesTrapezoidArea()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.5 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Razrabotka, cfg);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            double prism = 2.1 * 10.0 * B;
            Assert.True(rows[0].Volume > prism + 0.1);
        }

        [Fact]
        public void SlopedSandWithoutGeomsIsNonZero()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.5 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Pesok, cfg);
            Assert.Single(rows);
            Assert.True(rows[0].Volume > 0.5);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            double h = 0.10;
            double expect = SectionArea.Trap(B * 0.5, B * 0.5 + 0.5 * h, h) * 10.0;
            Assert.Equal(expect, rows[0].Volume, 5);
        }

        [Fact]
        public void SlopedSandUsesInnerWallCs()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.5 };
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 5.707, Y1 = 0,
                    InvertStart = 202.676, InvertEnd = 202.583,
                    Diam = 0.250, StartR = 0.5, EndR = 0.5
                }
            });
            var g0 = SectionGeom.From(chain.Locate(0.5), cfg, 202.523);
            var g1 = SectionGeom.From(chain.Locate(5.207), cfg, 202.523);
            var inside = SectionGeom.From(chain.Locate(0.050), cfg, 202.465);
            inside.InManhole = true;
            var axis = AxisBuilder.FromChain(chain, 2.0);
            foreach (var p in axis)
                p.Surface = 202.523;
            var rows = VolumeCalc.ForLevel(chain, axis, ProfileKind.Pesok, cfg,
                new List<SectionGeom> { inside, g0, g1 });
            Assert.Single(rows);
            double a0 = SectionArea.For(g0, ProfileKind.Pesok, 0.5).Value;
            double a1 = SectionArea.For(g1, ProfileKind.Pesok, 0.5).Value;
            Assert.Equal(0.090, a0, 3);
            double net = 5.707 - 1.0;
            Assert.Equal(0.5 * (a0 + a1) * net, rows[0].Volume, 4);
            var lv = new VolLevel { Kind = ProfileKind.Pesok, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.Contains("0.09", t);
            Assert.DoesNotContain("(0.00+0.00)", t);
        }

        [Fact]
        public void SlopedUsesAverageEndAreaFromGeoms()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.5 };
            var chain = OnePipe();
            var g0 = SectionGeom.From(chain.Locate(0.05), cfg, 102.0);
            var g1 = SectionGeom.From(chain.Locate(9.95), cfg, 102.0);
            var rows = VolumeCalc.ForLevel(chain, FlatSurf(102), ProfileKind.Razrabotka, cfg,
                new List<SectionGeom> { g0, g1 });
            double a0 = SectionArea.For(g0, ProfileKind.Razrabotka, 0.5).Value;
            double a1 = SectionArea.For(g1, ProfileKind.Razrabotka, 0.5).Value;
            double expect = a0 * 0.05 + 0.5 * (a0 + a1) * 9.9 + a1 * 0.05;
            Assert.Equal(expect, rows[0].Volume, 4);
        }

        [Fact]
        public void TrubaLengthIsNetWithoutManholes()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 100, InvertEnd = 100, Diam = 0.3,
                    StartR = 0.5, EndR = 0.5
                }
            });
            var rows = VolumeCalc.ForLevel(chain, FlatSurf(102), ProfileKind.Truba,
                new SectionConfig());
            Assert.Single(rows);
            Assert.Equal(9.0, rows[0].Length, 6);
            string t = VolumeCalc.FormatLevel(new VolLevel { Kind = ProfileKind.Truba, Rows = rows },
                new SectionConfig());
            Assert.Contains("∅300 - 9.00м", t);
        }

        [Fact]
        public void VerticalTextShowsProduct()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Pesok, cfg);
            var lv = new VolLevel { Kind = ProfileKind.Pesok, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.Contains("V=1.00*0.90=0.90 м³", t);
            Assert.Contains("ΣS*B", t);
        }

        [Fact]
        public void EarthBelowSandFirstPipeHasNoZashitaFormula()
        {
            var cfg = new SectionConfig
            {
                Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0,
                UsePipeWall = true, Walls = PipeWalls.KorsisSn8()
            };
            var chain = Chain.Sort(new System.Collections.Generic.List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 5.707, Y1 = 0,
                    InvertStart = 202.676, InvertEnd = 202.583, Diam = 0.250,
                    StartR = 0.5, EndR = 0.5
                }
            });
            var axis = new System.Collections.Generic.List<AxisPt>
            {
                new AxisPt { Ch = 0, X = 0, Y = 0, Invert = 202.676, Diam = 0.250, Surface = 202.480, EarthZ = 202.480, DesignZ = 203.500 },
                new AxisPt { Ch = 5.707, X = 5.707, Y = 0, Invert = 202.583, Diam = 0.250, Surface = 202.541, EarthZ = 202.541, DesignZ = 203.561 }
            };
            var rows = VolumeCalc.ForLevel(chain, axis, ProfileKind.Zashita, cfg);
            Assert.Single(rows);
            Assert.Equal(0.0, rows[0].Volume, 6);
            Assert.Equal(0.0, rows[0].PipeVol, 6);
            var lv = new VolLevel { Kind = ProfileKind.Zashita, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg) ?? "";
            Assert.DoesNotContain("-3,14", t);
            Assert.DoesNotContain("=-0.", t);
            Assert.DoesNotContain("V=0.00*", t);
        }

        [Fact]
        public void ZeroProtectionAreaSkipsPipeSubtraction()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(100.0), ProfileKind.Zashita, cfg);
            Assert.Single(rows);
            Assert.Equal(0.0, rows[0].Area, 6);
            Assert.Equal(0.0, rows[0].Volume, 6);
            Assert.Equal(0.0, rows[0].PipeVol, 6);
            var lv = new VolLevel { Kind = ProfileKind.Zashita, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.DoesNotContain("-3,14", t);
            Assert.DoesNotContain("=-0.", t);
            Assert.Contains("Итого: 0.00 м³", t);
        }

        [Fact]
        public void PartialProtectionSubtractsOnlyCoveredPipe()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(100.15), ProfileKind.Zashita, cfg);
            Assert.Single(rows);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            double hole = 0.5 * System.Math.PI * 0.15 * 0.15;
            double expect = 0.15 * 10.0 * B - hole * 10.0;
            Assert.Equal(expect, rows[0].Volume, 5);
            Assert.Equal(hole * 10.0, rows[0].PipeVol, 5);
            Assert.True(rows[0].PipeVol < System.Math.PI * 0.15 * 0.15 * 10.0 - 0.1);
            var lv = new VolLevel { Kind = ProfileKind.Zashita, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.DoesNotContain("-3,14*(0.150)²*10.00", t);
            Assert.Contains("V=1.50*0.90-0.35=1.00 м³", t);
        }

        [Fact]
        public void ProtectionTextShowsPipeSubtraction()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0 };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Zashita, cfg);
            var lv = new VolLevel { Kind = ProfileKind.Zashita, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.Contains("-3,14*(0.150)²*10.00", t);
            Assert.Contains("V=6.00*0.90", t);
        }

        [Fact]
        public void SlopedTextShowsAverageEndAreaTerms()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.5 };
            var chain = OnePipe();
            var g0 = SectionGeom.From(chain.Locate(0.05), cfg, 102.0);
            var g1 = SectionGeom.From(chain.Locate(9.95), cfg, 102.0);
            var rows = VolumeCalc.ForLevel(chain, FlatSurf(102), ProfileKind.Razrabotka, cfg,
                new List<SectionGeom> { g0, g1 });
            Assert.True(rows[0].Terms != null && rows[0].Terms.Count >= 2);
            var lv = new VolLevel { Kind = ProfileKind.Razrabotka, Rows = rows, TotalVolume = rows[0].Volume };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.Contains("/2*", t);
            Assert.Contains("Σ(S", t);
        }

        [Fact]
        public void ZasypkaSplitsSandAndSoilByZone()
        {
            var cfg = new SectionConfig
            {
                Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0,
                Zones = new List<RoadZone>
                {
                    new RoadZone { Type = "ASPH", Ch0 = 0, Ch1 = 5 },
                    new RoadZone { Type = "GAZN", Ch0 = 5, Ch1 = 10 }
                }
            };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Zasypka, cfg);
            Assert.Equal(2, rows.Count);
            Assert.Equal(BackfillKind.Sand, rows[0].Fill);
            Assert.Equal(BackfillKind.Soil, rows[1].Fill);
            Assert.Equal(5.0, rows[0].Length, 6);
            Assert.Equal(5.0, rows[1].Length, 6);
            var lv = new VolLevel { Kind = ProfileKind.Zasypka, Rows = rows };
            string t = VolumeCalc.FormatLevel(lv, cfg);
            Assert.Contains("Обратная засыпка песком", t);
            Assert.Contains("Обратная засыпка местным грунтом", t);
        }

        [Fact]
        public void ExcavationIgnoresFuturePavement()
        {
            var cfg = new SectionConfig
            {
                Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0,
                ThkAsph = 1.02, PavAll = true
            };
            cfg.ApplyPavement();
            var axis = new List<AxisPt>
            {
                new AxisPt { Ch = 0, X = 0, Y = 0, Invert = 100, Diam = 0.3, Surface = 102.0, DesignZ = 103.02, EarthZ = 102.0 },
                new AxisPt { Ch = 10, X = 10, Y = 0, Invert = 100, Diam = 0.3, Surface = 102.0, DesignZ = 103.02, EarthZ = 102.0 }
            };
            var rows = VolumeCalc.ForLevel(OnePipe(), axis, ProfileKind.Razrabotka, cfg);
            double B = TrenchWidth.For(0.3, JointType.Socket);
            Assert.Equal(2.1 * 10.0 * B, rows[0].Volume, 5);
        }

        [Fact]
        public void OneSideSlopeIsNotVerticalWalls()
        {
            var cfg = new SectionConfig { SlopeM = 0.0, SlopeL = 0.0, SlopeR = 0.5 };
            Assert.False(VolumeCalc.VerticalWalls(cfg));
        }

        [Fact]
        public void NaNSidesFollowSlopeM()
        {
            var cfg = new SectionConfig { SlopeM = 0.0 };
            Assert.True(VolumeCalc.VerticalWalls(cfg));
            cfg.SlopeM = 0.5;
            Assert.False(VolumeCalc.VerticalWalls(cfg));
        }

        [Fact]
        public void CustomBottomWidthIsVolumeB()
        {
            var cfg = new SectionConfig
            {
                Bedding = 0.10, ThkZash = 0.30, Joint = JointType.Socket, SlopeM = 0.0,
                TrenchLeft = 1.00, TrenchRight = 0.50
            };
            var rows = VolumeCalc.ForLevel(OnePipe(), FlatSurf(102), ProfileKind.Razrabotka, cfg);
            Assert.Equal(2.1 * 10.0 * 1.50, rows[0].Volume, 5);
            Assert.Equal(1.50, rows[0].Width, 6);
        }
    }

    public class PlanGeomTests
    {
        [Fact]
        public void AxisHasEnds()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 1, InvertEnd = 1, Diam = 0.3 }
            });
            var ax = PlanGeom.Axis(chain);
            Assert.Equal(2, ax.Count);
            Assert.Equal(0.0, ax[0].X, 6);
            Assert.Equal(10.0, ax[1].X, 6);
        }

        [Fact]
        public void TrenchClosedHasFourPlusSides()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 1, InvertEnd = 1, Diam = 0.3 }
            });
            var t = PlanGeom.TrenchClosed(chain, JointType.Socket);
            Assert.True(t.Count >= 4);
        }

        [Fact]
        public void TrenchOffsetMitersCornerNotPerSegmentShift()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 1, InvertEnd = 1, Diam = 0.3 },
                new PipeSeg { X0 = 10, Y0 = 0, X1 = 10, Y1 = 10, InvertStart = 1, InvertEnd = 1, Diam = 0.3 }
            });
            var t = PlanGeom.TrenchClosed(chain, JointType.Socket);
            // ось 3 вершины → 3 слева + 3 справа, не 8 от сдвига каждого сегмента
            Assert.Equal(6, t.Count);
            var left = PlanGeom.OffsetPolyVar(PlanGeom.Axis(chain),
                new List<double> { 0.45, 0.45 }, 1.0);
            Assert.Equal(3, left.Count);
            Assert.Equal(0.0, left[0].X, 5);
            Assert.Equal(0.45, left[0].Y, 5);
        }

        [Fact]
        public void TrenchOutlineDoesNotCrossAxisAtCorner()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 1, InvertEnd = 1, Diam = 0.3 },
                new PipeSeg { X0 = 10, Y0 = 0, X1 = 16, Y1 = -8, InvertStart = 1, InvertEnd = 1, Diam = 0.3 }
            });
            var axis = PlanGeom.Axis(chain);
            var t = PlanGeom.TrenchClosed(chain, JointType.Socket);
            Assert.True(t.Count >= 6);
            int n = t.Count;
            int crosses = 0;
            for (int i = 0; i < n; i++)
            {
                var a = t[i];
                var b = t[(i + 1) % n];
                for (int s = 0; s < axis.Count - 1; s++)
                {
                    if (ProperIntersect(a, b, axis[s], axis[s + 1]))
                        crosses++;
                }
            }
            Assert.Equal(0, crosses);
        }

        [Fact]
        public void AsymmetricPlanOffsetsDiffer()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 1, InvertEnd = 1, Diam = 0.3 }
            });
            var cfg = new SectionConfig { Joint = JointType.Socket, TrenchLeft = 1.00, TrenchRight = 0.40 };
            var t = PlanGeom.TrenchClosed(chain, cfg);
            Assert.True(t.Count >= 4);
            var left = PlanGeom.OffsetPolyVar(PlanGeom.Axis(chain), new List<double> { 1.00 }, 1.0);
            var right = PlanGeom.OffsetPolyVar(PlanGeom.Axis(chain), new List<double> { 0.40 }, -1.0);
            Assert.Equal(0.0, left[0].X, 5);
            Assert.Equal(1.00, left[0].Y, 5);
            Assert.Equal(0.0, right[0].X, 5);
            Assert.Equal(-0.40, right[0].Y, 5);
        }

        static bool ProperIntersect(PlanPt a, PlanPt b, PlanPt c, PlanPt d)
        {
            double d1 = Cross(b.X - a.X, b.Y - a.Y, c.X - a.X, c.Y - a.Y);
            double d2 = Cross(b.X - a.X, b.Y - a.Y, d.X - a.X, d.Y - a.Y);
            double d3 = Cross(d.X - c.X, d.Y - c.Y, a.X - c.X, a.Y - c.Y);
            double d4 = Cross(d.X - c.X, d.Y - c.Y, b.X - c.X, b.Y - c.Y);
            return d1 * d2 < -1e-12 && d3 * d4 < -1e-12;
        }

        static double Cross(double ax, double ay, double bx, double by)
        {
            return ax * by - ay * bx;
        }

        [Fact]
        public void SituationCorridorIsTwentyMetersWide()
        {
            var axis = new List<PlanPt>
            {
                new PlanPt(0, 0),
                new PlanPt(40, 0)
            };
            var sit = PlanGeom.SituationClosed(axis, PlanGeom.SitHalfM);
            Assert.True(sit.Count >= 4);
            Assert.Equal(10.0, sit[0].Y, 6);
            Assert.Equal(10.0, sit[1].Y, 6);
            Assert.Equal(-10.0, sit[2].Y, 6);
            Assert.Equal(-10.0, sit[3].Y, 6);
        }

        [Fact]
        public void SituationLeaderAtMidLeftOfAxis()
        {
            var axis = new List<PlanPt>
            {
                new PlanPt(0, 0),
                new PlanPt(20, 0),
                new PlanPt(40, 0)
            };
            PlanPt arrow, text;
            Assert.True(PlanGeom.SituationLeader(axis, PlanGeom.SitArrowM, PlanGeom.SitTextM, out arrow, out text));
            Assert.Equal(20.0, arrow.X, 6);
            Assert.Equal(6.0, arrow.Y, 6);
            Assert.Equal(20.0, text.X, 6);
            Assert.Equal(14.0, text.Y, 6);
        }

        [Fact]
        public void SituationHatchScaleMatchesPipeBuild()
        {
            Assert.Equal(5.0, PlanGeom.SituationHatchScale(0.0), 6);
            Assert.Equal(5.0, PlanGeom.SituationHatchScale(1.0), 6);
            Assert.Equal(2500.0, PlanGeom.SituationHatchScale(500.0), 6);
        }
    }
}
