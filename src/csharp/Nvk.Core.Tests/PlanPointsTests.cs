using System;
using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class PlanPointsTests
    {
        static Chain PipeWithMh()
        {
            return Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 40, Y1 = 0,
                    InvertStart = 100, InvertEnd = 99.6, Diam = 0.3,
                    StartName = "К1", StartR = 0.8,
                    EndName = "К2", EndR = 0.8
                }
            });
        }

        [Fact]
        public void ManholesAreNameAndXyOnly()
        {
            var rows = PlanPoints.Manholes(PipeWithMh());
            Assert.Equal(2, rows.Count);
            Assert.Equal("К1", rows[0].Name);
            Assert.Equal(0.0, rows[0].X, 6);
            Assert.Equal(0.0, rows[0].Y, 6);
            Assert.Equal("К2", rows[1].Name);
            Assert.Equal(40.0, rows[1].X, 6);
        }

        [Fact]
        public void ManholesWalkInChainOrderForSpanDims()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 20, Y1 = 0,
                    InvertStart = 100, InvertEnd = 99.8, Diam = 0.3,
                    StartName = "К1", StartR = 0.8,
                    EndName = "К2", EndR = 0.8
                },
                new PipeSeg
                {
                    X0 = 20, Y0 = 0, X1 = 50, Y1 = 0,
                    InvertStart = 99.8, InvertEnd = 99.5, Diam = 0.3,
                    StartName = "К2", StartR = 0.8,
                    EndName = "К3", EndR = 0.8
                }
            });
            var rows = PlanPoints.Manholes(chain);
            Assert.Equal(3, rows.Count);
            Assert.Equal("К1", rows[0].Name);
            Assert.Equal("К2", rows[1].Name);
            Assert.Equal("К3", rows[2].Name);
            Assert.Equal(0.0, rows[0].X, 6);
            Assert.Equal(20.0, rows[1].X, 6);
            Assert.Equal(50.0, rows[2].X, 6);
        }

        [Fact]
        public void ManholesIncludeNamedZeroRadius()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 40, Y1 = 0,
                    InvertStart = 100, InvertEnd = 99.6, Diam = 0.3,
                    StartName = "Выпуск", StartR = 0,
                    EndName = "К2", EndR = 0.8
                }
            });
            var rows = PlanPoints.Manholes(chain);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Выпуск", rows[0].Name);
            Assert.Equal(0.0, rows[0].X, 6);
            Assert.Equal("К2", rows[1].Name);
            Assert.Equal(40.0, rows[1].X, 6);
        }

        [Fact]
        public void ManholesStripParenKeepsLetterPrefix()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 40, Y1 = 0,
                    InvertStart = 100, InvertEnd = 99.6, Diam = 0.3,
                    StartName = "ГП12(1)", StartR = 0.8,
                    EndName = "К2", EndR = 0.8
                }
            });
            var rows = PlanPoints.Manholes(chain, true);
            Assert.Equal(2, rows.Count);
            Assert.Equal("ГП12", rows[0].Name);
            Assert.Equal("К2", rows[1].Name);
            Assert.Equal("ГП12(1)", PlanPoints.Manholes(chain, false)[0].Name);
        }

        [Fact]
        public void ManholesSkipUnnamedZeroRadius()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 40, Y1 = 0,
                    InvertStart = 100, InvertEnd = 99.6, Diam = 0.3,
                    StartName = "", StartR = 0,
                    EndName = "К2", EndR = 0.8
                }
            });
            var rows = PlanPoints.Manholes(chain);
            Assert.Single(rows);
            Assert.Equal("К2", rows[0].Name);
        }

        [Fact]
        public void SamplesSkipNearManholeAndNumberFromT1()
        {
            var chain = PipeWithMh();
            var cfg = new SectionConfig { PlanEn = true, PlanMin = 1.0, Joint = JointType.Socket };
            var targets = new List<double> { 0.5, 10.0, 20.0, 39.5 };
            var rows = PlanPoints.Samples(chain, cfg, targets, null, 1);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Т1", rows[0].Name);
            Assert.Equal("Т2", rows[1].Name);
            Assert.Equal(10.0, rows[0].X, 1);
            Assert.Equal(20.0, rows[1].X, 1);
        }

        [Fact]
        public void SampleArrowInsideTrenchShelfAtEdge()
        {
            var chain = PipeWithMh();
            var cfg = new SectionConfig { PlanEn = true, PlanMin = 1.0, Joint = JointType.Socket };
            double hw = cfg.BottomRight(0.3);
            var rows = PlanPoints.Samples(chain, cfg, new List<double> { 10.0 }, null, 1);
            Assert.Single(rows);
            Assert.True(Math.Abs(rows[0].Y) <= hw + 1e-9);
            Assert.Equal(hw + PlanPoints.SampShelfOut, Math.Abs(rows[0].Ty), 6);
            Assert.Equal(Math.Sign(rows[0].Y), Math.Sign(rows[0].Ty));
        }

        [Fact]
        public void SamplesContinueFromNextNum()
        {
            var chain = PipeWithMh();
            var cfg = new SectionConfig { PlanEn = true, PlanMin = 1.0, Joint = JointType.Socket };
            var rows = PlanPoints.Samples(chain, cfg, new List<double> { 10.0, 20.0 }, null, 3);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Т3", rows[0].Name);
            Assert.Equal("Т4", rows[1].Name);
        }

        [Fact]
        public void SamplesOffWhenPlanDisabled()
        {
            var chain = PipeWithMh();
            var cfg = new SectionConfig { PlanEn = false };
            var rows = PlanPoints.Samples(chain, cfg, new List<double> { 10.0 }, null, 1);
            Assert.Empty(rows);
        }

        [Fact]
        public void SamplesFallbackWhenZonalTargetHitsManhole()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 2.155, "61", "62"),
                Seg(2.155, 23.272, "62", "65"),
                Seg(23.272, 35.380, "65", "66"),
                Seg(35.380, 42.076, "66", "ГП12")
            });
            var cfg = new SectionConfig { PlanEn = true, PlanMin = 15.0, Joint = JointType.Socket };
            var rows = PlanPoints.Samples(chain, cfg, new List<double> { 21.0 }, null, 1);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Т1", rows[0].Name);
            Assert.Equal("Т2", rows[1].Name);
            Assert.InRange(rows[0].X, 10.0, 16.0);
            Assert.InRange(rows[1].X, 26.0, 33.0);
        }

        [Fact]
        public void FallbackTargetsAreMidsOfLongSpans()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                Seg(0, 2.155, "61", "62"),
                Seg(2.155, 23.272, "62", "65"),
                Seg(23.272, 35.380, "65", "66"),
                Seg(35.380, 42.076, "66", "ГП12")
            });
            var t = PlanPoints.FallbackTargets(chain);
            Assert.Equal(2, t.Count);
            Assert.Equal(2.155 + 21.117 * 0.5, t[0], 2);
            Assert.Equal(23.272 + 12.108 * 0.5, t[1], 2);
        }

        static PipeSeg Seg(double x0, double x1, string a, string b)
        {
            return new PipeSeg
            {
                X0 = x0, Y0 = 0, X1 = x1, Y1 = 0,
                InvertStart = 100, InvertEnd = 99.6, Diam = 0.25,
                StartName = a, StartR = 0.5,
                EndName = b, EndR = 0.5
            };
        }
    }
}
