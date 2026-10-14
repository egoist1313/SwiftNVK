using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class TableLayoutTests
    {
        [Fact]
        public void RtosKeepsTrailingZeros()
        {
            Assert.Equal("10.50", TableLayout.Rtos(10.5, 2));
            Assert.Equal("5.00", TableLayout.Rtos(5.0, 2));
            Assert.Equal("10", TableLayout.Rtos(10.4, 0));
        }

        [Fact]
        public void HeightRowMatchesPoperSwift()
        {
            // th*(0.7*7+0.3) = 2.5*5.2 = 13
            Assert.Equal(13.0, TableLayout.RowHeight(TableRowKind.Height, 2.5, 7, 4), 6);
            Assert.Equal(7.5, TableLayout.RowHeight(TableRowKind.Slope, 2.5, 7, 4), 6);
            Assert.Equal(7.75, TableLayout.RowHeight(TableRowKind.Length, 2.5, 7, 4), 6);
        }

        [Fact]
        public void AutoColWMinTenTextH()
        {
            Assert.Equal(25.0, TableLayout.AutoLabelColW(new List<string> { "Уклон" }, 2.5), 6);
        }

        [Fact]
        public void AutoColWUsesLongestLine()
        {
            double w = TableLayout.AutoLabelColW(new List<string> { "Отметка земли, м" }, 2.5);
            Assert.True(w > 25.0);
            Assert.Equal(2.5 * TableLayout.MaxLineLen("Отметка земли, м") * 0.9, w, 6);
        }

        [Fact]
        public void TextColWFollowsTextHeightAndLongest()
        {
            double w = TableLayout.TextColW(2.5, new List<string> { TableLayout.CoordNo, "К1" });
            Assert.Equal(2.5 * (2 * 0.7 + 1.6), w, 6);
            double xw = TableLayout.TextColW(2.5, new List<string> { "X", "2338936.150" });
            Assert.True(xw < 2.5 * 14);
            Assert.Equal(2.5 * (11 * 0.7 + 1.6), xw, 6);
        }

        [Fact]
        public void XyTextThreeDecimals()
        {
            Assert.Equal("2338936.150", TableLayout.XyText(2338936.15));
            Assert.Equal("477499.000", TableLayout.XyText(477499));
            Assert.Equal("№", TableLayout.CoordNo);
        }

        [Fact]
        public void MaxLineLenSplitsP()
        {
            Assert.Equal(7, TableLayout.MaxLineLen("Отметка\\Pдна"));
        }

        [Fact]
        public void RotateWhenNarrowerThanTwoTextH()
        {
            Assert.True(TableLayout.RotateLength(4.0, 2.5));
            Assert.False(TableLayout.RotateLength(6.0, 2.5));
        }

        [Fact]
        public void SlopePermilleWithSignChar()
        {
            var cfg = new TableCfg { SlopeRoundTo = 1, ShowPermille = true, SlopeUnit = "permille" };
            Assert.Equal("10.0‰", TableLayout.SlopeText(-0.1, 10.0, cfg));
        }

        [Fact]
        public void SlopeTextUsesRowPrecision()
        {
            var cfg = new TableCfg { SlopeRoundTo = 1, ShowPermille = true, SlopeUnit = "permille" };
            Assert.Equal("10.00‰", TableLayout.SlopeText(-0.1, 10.0, cfg, 2));
            Assert.Equal("10‰", TableLayout.SlopeText(-0.1, 10.0, cfg, 0));
        }

        [Fact]
        public void PrecFallsBackWhenRowIsZero()
        {
            Assert.Equal(2, TableLayout.Prec(0, 2));
            Assert.Equal(3, TableLayout.Prec(3, 2));
        }

        [Fact]
        public void SlopeSignedKeepsMinus()
        {
            var cfg = new TableCfg { SlopeRoundTo = 1, ShowPermille = true };
            Assert.Equal("-5.0‰", TableLayout.SlopeSigned(-0.005, cfg));
        }

        [Fact]
        public void ScaleLegendMatchesProfile()
        {
            string s = TableLayout.ScaleLegend(500.0, 50.0, 97.0);
            Assert.Contains("М 1:500 по горизонтали", s);
            Assert.Contains("М 1:50 по вертикали", s);
            Assert.Contains("Условный горизонт 97.00 м", s);
            Assert.Equal(100.0, TableLayout.LegendDenom(1000.0, 10.0), 6);
            Assert.Equal(500.0, TableLayout.LegendDenom(500.0, 1.0), 6);
            Assert.Equal(50.0, TableLayout.LegendDenom(500.0, 10.0), 6);
            Assert.Equal(200.0, TableLayout.LegendDenom(200.0, 1.0), 6);
        }

        [Fact]
        public void CsHatchFixedMatchesPipeBuild()
        {
            Assert.Equal(1.0, HatchPat.CsFixed("ANSI31"), 6);
            Assert.Equal(0.2, HatchPat.CsFixed("DOTS"), 6);
            Assert.Equal(0.01, HatchPat.CsFixed("AR-SAND"), 6);
            Assert.Equal("AR-SAND", HatchPat.Backfill("ASPH"));
            Assert.Equal("AR-SAND", HatchPat.Backfill("TROT"));
            Assert.Equal("DOTS", HatchPat.Backfill("GAZN"));
            Assert.Equal("DOTS", HatchPat.Backfill(null));
            var cfg = new SectionConfig { FillGazn = "SAND", FillAsph = "SOIL" };
            Assert.Equal("DOTS", HatchPat.Backfill("ASPH", cfg));
            Assert.Equal("AR-SAND", HatchPat.Backfill("GAZN", cfg));
            Assert.Equal(BackfillKind.Sand, BackfillMap.Of("ASPH", null));
            Assert.Equal(BackfillKind.Soil, BackfillMap.Of("GAZN", null));
        }

        [Fact]
        public void CsTableHeightUsesSevenCharFloor()
        {
            var hit = new StationHit { Station = 10, Invert = 100, Diam = 0.3 };
            var g = SectionGeom.From(hit, new SectionConfig { Bedding = 0.1, ThkZash = 0.3 }, 102.0);
            var tbl = new TableCfg { TextH = 0.5, RoundTo = 2 };
            double h = CsAnnot.TableHeight(g, ProfileKind.Razrabotka, 3.0, 0.0, tbl);
            double rowH = TableLayout.RowHeight(TableRowKind.Height, 0.5, 7, 4);
            double rowL = TableLayout.RowHeight(TableRowKind.Length, 0.5, 7, 4);
            Assert.Equal(2 * rowH + rowL, h, 6);
        }

        [Fact]
        public void DiamMarkEmptySet()
        {
            Assert.Equal("∅300", TableLayout.DiamMark(0.3));
        }

        [Fact]
        public void SDiamIsMtextSubscript()
        {
            Assert.Equal("S{\\H0.7x;\\S^∅250;}", TableLayout.SDiam(0.25));
            Assert.Equal("S{\\H0.7x;\\S^∅300;}", TableLayout.SDiam(300));
            Assert.Equal("{\\H0.7x;\\S^∅160 песок;}", TableLayout.DiamSub(160, "песок"));
        }

        [Fact]
        public void ScaleLegendKeepsDiamSub()
        {
            string s = TableLayout.ScaleLegend(500.0, 50.0, 97.0, 1.5, TableLayout.SDiam(0.25));
            Assert.Contains("S{\\H0.7x;\\S^∅250;}", s);
            Assert.Contains("= 1.50 м²", s);
        }

        [Fact]
        public void LengthIntervalsIncludeManholeGaps()
        {
            var spans = TableLayout.LengthIntervals(
                new[] { 0.5, 10.5 },
                new[] { 9.5, 19.5 },
                20.0);
            Assert.Equal(5, spans.Count);
            Assert.Equal(0.0, spans[0][0], 6);
            Assert.Equal(0.5, spans[0][1], 6);
            Assert.Equal(0.5, spans[1][0], 6);
            Assert.Equal(9.5, spans[1][1], 6);
            Assert.Equal(9.5, spans[2][0], 6);
            Assert.Equal(10.5, spans[2][1], 6);
            Assert.Equal(10.5, spans[3][0], 6);
            Assert.Equal(19.5, spans[3][1], 6);
            Assert.Equal(19.5, spans[4][0], 6);
            Assert.Equal(20.0, spans[4][1], 6);
        }

        [Fact]
        public void LengthIntervalsWithoutGapsArePipesOnly()
        {
            var spans = TableLayout.LengthIntervals(
                new[] { 0.0, 10.0 },
                new[] { 10.0, 20.0 },
                20.0);
            Assert.Equal(2, spans.Count);
            Assert.Equal(0.0, spans[0][0], 6);
            Assert.Equal(10.0, spans[0][1], 6);
            Assert.Equal(10.0, spans[1][0], 6);
            Assert.Equal(20.0, spans[1][1], 6);
        }
    }
}
