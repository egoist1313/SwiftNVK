using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class ProfileRowsTests
    {
        [Fact]
        public void L1HasRoadEarthBottomDiam()
        {
            var rows = ProfileRows.For(ProfileKind.Razrabotka);
            Assert.Contains(rows, r => r.Src == HeightSrc.RoadTop && r.Show);
            Assert.Contains(rows, r => r.Src == HeightSrc.BlackEarth && r.Show
                && r.Label == ProfileRows.EarthLabel);
            Assert.Contains(rows, r => r.Src == HeightSrc.TrenchBottom && r.Show);
            Assert.Contains(rows, r => r.Kind == SheetRowKind.Diam);
            Assert.Contains(rows, r => r.Kind == SheetRowKind.Manhole && r.Show);
            Assert.Contains(rows, r => r.Kind == SheetRowKind.Length);
        }

        [Fact]
        public void OldEarthLabelBecomesOtmetkaZemli()
        {
            Assert.Equal("Отметка земли, м",
                ProfileRows.MigrateEarthLabel("Черная земля\\P(подошва одежды), м"));
            Assert.Equal("Отметка земли, м", ProfileRows.MigrateEarthLabel(""));
            Assert.Equal("Своя подпись", ProfileRows.MigrateEarthLabel("Своя подпись"));
        }

        [Fact]
        public void L1StyleTurnsManholesOn()
        {
            var st = L1Style.Default();
            st.ShowManholes = true;
            st.LabelBottom = "Дно";
            var rows = ProfileRows.ForL1(st, new TableCfg());
            Assert.Contains(rows, r => r.Kind == SheetRowKind.Manhole && r.Show);
            Assert.Contains(rows, r => r.Src == HeightSrc.TrenchBottom && r.Label == "Дно");
        }

        [Fact]
        public void L3StartsWithSlope()
        {
            var rows = ProfileRows.For(ProfileKind.Truba);
            Assert.Equal(SheetRowKind.Slope, rows[0].Kind);
        }

        [Fact]
        public void DeltaZashIsL4MinusL2MinusDiam()
        {
            var cfg = new SectionConfig { Bedding = 0.10, ThkZash = 0.30 };
            var p = new AxisPt { Invert = 100.0, Diam = 0.25, EarthZ = 102.0, Surface = 102.0 };
            double l4 = p.NamedZ(NamedLine.ZashTop, cfg);
            double l2 = p.NamedZ(NamedLine.SandTop, cfg);
            double thk = ProfileRows.ZAt(HeightSrc.DeltaZash, p, cfg);
            Assert.Equal(l4 - l2 - 0.25, thk, 6);
            Assert.Equal(0.30, thk, 6);
        }

        [Fact]
        public void DeltaZashZeroWhenL4OnL2()
        {
            Assert.Equal(0.0, ProfileRows.DeltaZashThickness(100.0, 100.0, 0.25), 6);
            Assert.Equal(0.05, ProfileRows.DeltaZashThickness(100.40, 100.10, 0.25), 6);
        }

        [Fact]
        public void FromDefsKeepsLengthAndSlopeRoundTo()
        {
            var defs = new System.Collections.Generic.List<TableRowDef>
            {
                new TableRowDef { Type = SheetRowKind.Length, Label = "Длина, м", Show = true, RoundTo = 3 },
                new TableRowDef { Type = SheetRowKind.Slope, Label = "Уклон, ‰", Show = true, RoundTo = 2 }
            };
            var rows = ProfileRows.FromDefs(defs);
            Assert.Equal(3, rows[0].RoundTo);
            Assert.Equal(2, rows[1].RoundTo);
        }

        [Fact]
        public void FromDefsProjFactMarksFactRow()
        {
            var defs = new System.Collections.Generic.List<TableRowDef>
            {
                new TableRowDef
                {
                    Type = SheetRowKind.Height,
                    Src = HeightSrc.SandTop,
                    Label = "Отметка верха песка, м",
                    Show = true,
                    IsProjFact = true,
                    UseScatter = false
                }
            };
            var rows = ProfileRows.FromDefs(defs);
            Assert.Equal(2, rows.Count);
            Assert.False(rows[0].IsFact);
            Assert.Contains("проект", rows[0].Label);
            Assert.True(rows[1].IsFact);
            Assert.Contains("факт", rows[1].Label);
        }

        [Fact]
        public void DefaultsScatterOnMainHeightRows()
        {
            var l1 = TableRowDef.Defaults(ProfileKind.Razrabotka);
            Assert.Contains(l1, d => d.Src == HeightSrc.TrenchBottom && d.UseScatter && d.ScatterAmp > 0);
            Assert.DoesNotContain(l1, d => d.Src == HeightSrc.RoadTop && d.UseScatter);

            var l2 = TableRowDef.Defaults(ProfileKind.Pesok);
            Assert.Contains(l2, d => d.Src == HeightSrc.SandTop && d.UseScatter);
            Assert.Contains(l2, d => d.Src == HeightSrc.TrenchBottom && d.UseScatter);

            var l3 = TableRowDef.Defaults(ProfileKind.Truba);
            Assert.DoesNotContain(l3, d => d.UseScatter);

            var l4 = TableRowDef.Defaults(ProfileKind.Zashita);
            Assert.Contains(l4, d => d.Src == HeightSrc.ZashTop && d.UseScatter);

            var l5 = TableRowDef.Defaults(ProfileKind.Zasypka);
            Assert.Contains(l5, d => d.Src == HeightSrc.Surface && d.UseScatter);
        }

        [Fact]
        public void SerializeNineFieldsRoundtrip()
        {
            var d = new TableRowDef
            {
                Type = SheetRowKind.Height,
                Src = HeightSrc.SandTop,
                Label = "Песок",
                Show = true,
                UseScatter = true,
                ScatterAmp = 0.04,
                AmpInRecord = true
            };
            var back = TableRowDef.Deserialize(d.Serialize());
            Assert.True(back.UseScatter);
            Assert.Equal(0.04, back.ScatterAmp, 3);
            Assert.True(back.AmpInRecord);
        }

        [Fact]
        public void OldEightFieldHasNoAmpInRecord()
        {
            var d = TableRowDef.Deserialize("H|TrenchBottom|0|Дно|2|1|0|1");
            Assert.True(d.UseScatter);
            Assert.False(d.AmpInRecord);
            Assert.Equal(0.0, d.ScatterAmp, 6);
        }
    }
}
