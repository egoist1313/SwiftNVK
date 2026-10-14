using System;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class CsAnnotTests
    {
        static SectionGeom G()
        {
            var hit = new StationHit { Station = 25.959, Invert = 100, Diam = 0.3, Slope = -0.005 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket }, 102.0);
            g.SurfPts = new System.Collections.Generic.List<SurfPt>
            {
                new SurfPt(-3, 103),
                new SurfPt(0, 102),
                new SurfPt(3, 101)
            };
            return g;
        }

        [Fact]
        public void ColGapFitsTablePlusDims()
        {
            var tbl = new TableCfg { CsTextH = 0.5, TextH = 0.5 };
            double labW = TableLayout.LabelColW(tbl.ForCs(), CsAnnot.LayoutLabels());
            double slot = CsAnnot.ColGap(3.0, 1.0, 0.5, labW);
            double tableW = labW + 2.0 * (3.0 * 1.0 + 0.5);
            Assert.True(slot > tableW + 2.0);
            Assert.True(slot > 16.7);
        }

        [Fact]
        public void PlaceMarksStaggersByFontHeight()
        {
            var xs = CsAnnot.PlaceMarks(new[] { 0.0, 1.0, 2.0 }, 1.75);
            Assert.Equal(3, xs.Count);
            Assert.Equal(0.0, xs[0], 6);
            Assert.Equal(1.75, xs[1], 6);
            Assert.Equal(3.50, xs[2], 6);
            var wide = CsAnnot.PlaceMarks(new[] { 0.0, 10.0 }, 1.75);
            Assert.Equal(0.0, wide[0], 6);
            Assert.Equal(10.0, wide[1], 6);
        }

        [Fact]
        public void MaxPerRowWrapsWhenProfileNarrowerThanStrip()
        {
            Assert.Equal(2, CsAnnot.MaxPerRow(55.8, 22.0));
            int col, row;
            CsAnnot.Grid(5, 2, out col, out row);
            Assert.Equal(1, col);
            Assert.Equal(2, row);
        }

        [Fact]
        public void HeaderIsSectionNumberOnly()
        {
            var g = G();
            string h = CsAnnot.Header(0, ProfileKind.Razrabotka, g.Elev.Station);
            Assert.Equal("Сечение 1-1", h);
            Assert.DoesNotContain("L1", h);
            Assert.DoesNotContain("Песок", h);
            Assert.DoesNotContain("ПК", h);
        }

        [Fact]
        public void DiamTextMm()
        {
            Assert.Equal("∅300", CsAnnot.DiamText(0.3));
        }

        [Fact]
        public void AreaLabelMatchesLongitudinalSubscript()
        {
            Assert.Equal("S{\\H0.7x;\\S^∅250;}", CsAnnot.AreaLabel(ProfileKind.Razrabotka, 0.25));
            Assert.Equal("S{\\H0.7x;\\S^∅300;}", CsAnnot.AreaLabel(ProfileKind.Zashita, 0.3));
            Assert.DoesNotContain("S/", CsAnnot.AreaLabel(ProfileKind.Razrabotka, 0.25));
            Assert.Equal("Sпеска", CsAnnot.AreaLabel(ProfileKind.Pesok, 0.25, G(), null));
            var g = G();
            g.ZoneType = "GAZN";
            Assert.Equal("Sместн.грунта", CsAnnot.AreaLabel(ProfileKind.Zasypka, 0.25, g, null));
        }

        [Fact]
        public void L1ProjFactAddsFactRow()
        {
            var g = G();
            g.FactBottom = 99.88;
            var cfg = new SectionConfig { ProjFact = true, SingleScatter = false };
            var rows = CsAnnot.Rows(g, ProfileKind.Razrabotka, 3.0, 0.0, new TableCfg(), cfg);
            Assert.Contains(rows, r => r.Label != null && r.Label.IndexOf("проект") >= 0);
            Assert.Contains(rows, r => r.Label != null && r.Label.IndexOf("факт") >= 0);
        }

        [Fact]
        public void L1RowsGroundAndBottom()
        {
            var g = G();
            var rows = CsAnnot.Rows(g, ProfileKind.Razrabotka, 3.0, 0.5);
            Assert.True(rows.Count >= 2);
            Assert.Equal("Отметка земли, м", rows[0].Label);
            Assert.True(rows[0].IsSurf);
            Assert.True(rows[0].C1.HasValue);
            Assert.True(rows[0].C1.Value > 102.5);
            Assert.Equal(g.Elev.TrenchBottom, rows[1].C2.Value, 6);
        }

        [Fact]
        public void L4AsphAddsPavementRow()
        {
            var g = G();
            g.ZoneType = "ASPH";
            var rows = CsAnnot.Rows(g, ProfileKind.Zashita, 3.0, 0.0);
            Assert.Contains(rows, r => r.Label != null && r.Label.IndexOf("одежды") >= 0);
        }

        [Fact]
        public void L3HasNoSlopeRow()
        {
            var g = G();
            var rows = CsAnnot.Rows(g, ProfileKind.Truba, 3.0, 0.5);
            Assert.DoesNotContain(rows, r => r.Label != null && r.Label.IndexOf("клон") >= 0);
            Assert.DoesNotContain(rows, r => r.Center != null && r.Center.Contains("‰"));
        }

        [Fact]
        public void L6MarksAtTrenchWall()
        {
            var g = G();
            double xTop = TrenchGeom.XTop(g, 0.5);
            var rows = CsAnnot.Rows(g, ProfileKind.Razrabotka, 3.0, 0.5);
            Assert.Equal(TrenchGeom.SurfZAt(g, -xTop), rows[0].C2.Value, 6);
            Assert.Equal(TrenchGeom.SurfZAt(g, xTop), rows[0].C3.Value, 6);
            Assert.True(xTop > TrenchGeom.HalfBottom(g));
        }

        [Fact]
        public void GapsBottomEqualsTrenchWidth()
        {
            var g = G();
            var gaps = CsAnnot.Gaps(g, ProfileKind.Razrabotka, 3.0, 0.5);
            Assert.Equal(3, gaps.Length);
            Assert.Equal(g.TrenchW, gaps[1], 6);
        }

        [Fact]
        public void GapsAsymmetricBottom()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var cfg = new SectionConfig
            {
                Bedding = 0.1, ThkZash = 0.3, Joint = JointType.Socket,
                TrenchLeft = 0.80, TrenchRight = 0.30
            };
            var g = SectionGeom.From(hit, cfg, 102.0);
            var gaps = CsAnnot.Gaps(g, ProfileKind.Razrabotka, 3.0, 0.0);
            Assert.Equal(2.20, gaps[0], 6);
            Assert.Equal(1.10, gaps[1], 6);
            Assert.Equal(2.70, gaps[2], 6);
        }

        [Fact]
        public void SurfZAtInterpolates()
        {
            var g = G();
            Assert.Equal(102.0, TrenchGeom.SurfZAt(g, 0.0), 6);
            double z = TrenchGeom.SurfZAt(g, 1.5);
            Assert.True(z > 101.0 && z < 102.0);
        }

        [Fact]
        public void SectionMarkMatchesHeader()
        {
            Assert.Equal("Сечение 2-2", CsAnnot.SectionMark(1));
        }

        [Fact]
        public void SingleScatterBakesSandThicknessFromProfile()
        {
            var g = G();
            Assert.Equal(0.10, g.Thickness(ProfileKind.Pesok), 6);
            g.FactSand = g.Elev.SandTop - 0.02;
            g.FactBottom = g.Elev.TrenchBottom;
            g.ApplyDrawnScatter(true);
            Assert.Equal(0.08, g.Thickness(ProfileKind.Pesok), 6);
            var rows = CsAnnot.Rows(g, ProfileKind.Pesok, 3.0, 0.0);
            Assert.Contains(rows, r => r.C2.HasValue && Math.Abs(r.C2.Value - g.Elev.SandTop) < 1e-9);
            Assert.Contains(rows, r => r.C2.HasValue && Math.Abs(r.C2.Value - g.Elev.TrenchBottom) < 1e-9);
            Assert.Equal(0.08, g.Elev.SandTop - g.Elev.TrenchBottom, 6);
        }

        [Fact]
        public void SingleScatterKeepsTinWhenEarthBelowTrench()
        {
            var hit = new StationHit { Station = 0.5, Invert = 202.668, Diam = 0.25 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.10, ThkZash = 0.30 }, 202.480, pav: 1.02);
            double tin = g.Elev.Surface;
            Assert.True(tin < g.Elev.TrenchBottom);
            g.FactSand = g.Elev.SandTop + 0.01;
            g.FactBottom = g.Elev.TrenchBottom - 0.03;
            g.FactZash = g.Elev.ZashTop + 0.04;
            g.FactSurf = tin + 0.20;
            g.ApplyDrawnScatter(true);
            Assert.Equal(tin, g.Elev.Surface, 6);
            Assert.Equal(g.Elev.SandTop, g.Elev.ZashTop, 6);
            Assert.True(g.Elev.TrenchBottom <= g.Elev.SandTop + 1e-9);
            Assert.False(g.Elev.HasBackfill);
            Assert.Equal(0.0, SectionArea.For(g, ProfileKind.Razrabotka, 0.5).Value, 6);
        }

        [Fact]
        public void L1AndL2ShareTrenchBottomAfterScatter()
        {
            var g = G();
            g.FactSand = g.Elev.SandTop - 0.02;
            g.FactBottom = g.Elev.TrenchBottom + 0.03;
            g.ApplyDrawnScatter(true);
            var l1 = CsAnnot.Rows(g, ProfileKind.Razrabotka, 3.0, 0.0);
            var l2 = CsAnnot.Rows(g, ProfileKind.Pesok, 3.0, 0.0);
            double z1 = l1.Find(r => r.C2.HasValue && r.Label != null && r.Label.IndexOf("дна") >= 0).C2.Value;
            double z2 = l2.Find(r => r.C2.HasValue && r.Label != null && r.Label.IndexOf("дна") >= 0).C2.Value;
            Assert.Equal(z1, z2, 6);
            Assert.Equal(g.Elev.TrenchBottom, z1, 6);
        }

        [Fact]
        public void ProjFactKeepsCatalogSandOnSection()
        {
            var g = G();
            double catalog = g.Elev.SandTop;
            g.FactSand = catalog - 0.02;
            g.ApplyDrawnScatter(false);
            Assert.Equal(catalog, g.Elev.SandTop, 6);
            Assert.Equal(0.10, g.Thickness(ProfileKind.Pesok), 6);
        }

        [Fact]
        public void L4FactSandUsesFactNotCatalog()
        {
            var g = G();
            g.FactSand = g.Elev.SandTop - 0.03;
            var cfg = new SectionConfig { ProjFact = true, SingleScatter = false };
            var rows = CsAnnot.Rows(g, ProfileKind.Zashita, 3.0, 0.0, new TableCfg(), cfg);
            Assert.Contains(rows, r => r.Label != null && r.Label.IndexOf("факт") >= 0
                && r.C2.HasValue && Math.Abs(r.C2.Value - g.FactSand.Value) < 1e-9);
        }
    }
}
