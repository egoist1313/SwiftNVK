using System;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class WellFillTests
    {
        [Fact]
        public void FiveStagesAreNamed()
        {
            Assert.Equal(5, WellCutStage.Count);
            Assert.Equal("Разработка котлована", WellCutStage.Name(1));
            Assert.Equal("Песчаная подготовка", WellCutStage.Name(2));
            Assert.Equal("Колодец", WellCutStage.Name(3));
            Assert.Equal("Заделка труб", WellCutStage.Name(4));
            Assert.Equal("Обратная засыпка", WellCutStage.Name(5));
            Assert.Equal("Сечение 1-1. Разработка котлована", WellCutStage.Title(1, false));
            Assert.Equal("Разрез 3-3. Колодец", WellCutStage.Title(3, true));
            Assert.True(WellCutStage.ShowsWell(3));
            Assert.False(WellCutStage.ShowsWell(2));
            Assert.True(WellCutStage.ShowsPipes(4));
            Assert.True(WellCutStage.ShowsBackfill(5));
        }

        [Fact]
        public void AutoStackFillsHeightWithGostRings()
        {
            var p = WellCatalog.AutoStack(1.00, 3.00);
            Assert.True(p.Count >= 4);
            Assert.Equal(WellPartKind.Bottom, p[0].Kind);
            Assert.Equal("ПН-10", p[0].Code);
            double h = p[p.Count - 1].Z1;
            Assert.True(h >= 2.85 && h <= 3.50);
            bool ring = false;
            for (int i = 0; i < p.Count; i++)
                if (p[i].Kind == WellPartKind.Ring && p[i].Code.StartsWith("КС-10"))
                    ring = true;
            Assert.True(ring);
            Assert.Equal(WellPartKind.Hatch, p[p.Count - 1].Kind);
        }

        [Fact]
        public void AutoStackPicksSeries15ForWideWell()
        {
            var p = WellCatalog.AutoStack(1.50, 2.50);
            Assert.Equal("ПН-15", p[0].Code);
        }

        [Fact]
        public void ExactPartsKeepQty()
        {
            var fill = new WellFill
            {
                Auto = false,
                Parts = WellCatalog.ParseParts("ПН-10:1;КС-10.9:2;ПП-10:1")
            };
            var p = WellCatalog.Place(fill, 1.0, 5.0);
            Assert.Equal(4, p.Count);
            Assert.Equal("КС-10.9", p[1].Code);
            Assert.Equal("КС-10.9", p[2].Code);
            Assert.Equal("ПП-10", p[3].Code);
        }

        [Fact]
        public void ParseRoundtrip()
        {
            var a = WellCatalog.ParseParts("КС-10.9:2;ПН-10:1");
            string s = WellCatalog.FormatParts(a);
            var b = WellCatalog.ParseParts(s);
            Assert.Equal(2, b.Count);
            Assert.Equal("КС-10.9", b[0].Code);
            Assert.Equal(2, b[0].Qty);
        }

        [Fact]
        public void PlasticNeckShiftsLeft()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                Neck = WellNeckSide.Left,
                BaseCode = "Прямопроходная",
                ShaftCode = "Тридэвелл DN1000",
                NeckCode = "Конус"
            };
            var p = WellCatalog.Place(fill, 1.0, 3.0);
            Assert.Equal(WellPartKind.Base, p[0].Kind);
            Assert.Contains("DN1000", p[0].Code);
            bool shaft = false, cone = false, neck = false, form = false;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind == WellPartKind.Shaft)
                {
                    shaft = true;
                    Assert.Equal(0.0, p[i].AxisX, 6);
                }
                if (p[i].Kind == WellPartKind.Cone)
                {
                    cone = true;
                    Assert.True(p[i].TopInnerR < p[i].InnerR - 0.05);
                    Assert.True(p[i].TopAxisX < -0.05);
                    Assert.Equal(0.0, p[i].AxisX, 6);
                }
                if (p[i].Kind == WellPartKind.Neck)
                {
                    neck = true;
                    Assert.True(p[i].AxisX < -0.05);
                }
                if (p[i].Kind == WellPartKind.Form) form = true;
            }
            Assert.True(shaft);
            Assert.True(cone);
            Assert.True(neck);
            Assert.True(form);
            Assert.True(p[p.Count - 1].Z1 >= 2.9);
        }

        [Fact]
        public void PlasticNeckCenterIsZero()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                Neck = WellNeckSide.Center,
                BaseCode = "Прямопроходная",
                ShaftCode = "Тридэвелл DN1000",
                NeckCode = "Конус"
            };
            var p = WellCatalog.Place(fill, 1.0, 2.5);
            for (int i = 0; i < p.Count; i++)
            {
                Assert.Equal(0.0, p[i].AxisX, 6);
                Assert.Equal(0.0, p[i].TopAxisX, 6);
            }
        }

        [Fact]
        public void PlasticAutoPicksDn1000FromMeterWell()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                ShaftCode = "Авто",
                BaseCode = "Прямопроходная",
                NeckCode = "Конус"
            };
            var p = WellCatalog.Place(fill, 1.0, 3.0);
            Assert.Contains("DN1000", p[0].Code);
            Assert.Equal(1000, WellCatalog.PlasticSeriesMm("Авто", 1.0));
            Assert.Equal(1500, WellCatalog.PlasticSeriesMm("Авто", 1.5));
        }

        [Fact]
        public void PlasticAutoPicksDn1500FromWideWell()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                ShaftCode = "Авто",
                BaseCode = "Прямопроходная",
                NeckCode = "Конус"
            };
            var p = WellCatalog.Place(fill, 1.50, 4.0);
            Assert.Contains("DN1500", p[0].Code);
            bool cone = false;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind != WellPartKind.Cone) continue;
                cone = true;
                Assert.True(p[i].Z1 - p[i].Z0 > 0.50);
                Assert.True(p[i].OuterR > 0.70);
            }
            Assert.True(cone);
        }

        [Fact]
        public void PlasticRainConeHasRectangularTop()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                ShaftCode = "Тридэвелл DN1000",
                NeckCode = "Дождеприемный"
            };
            var p = WellCatalog.Place(fill, 1.0, 2.5);
            bool rain = false;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind != WellPartKind.Cone) continue;
                rain = true;
                Assert.Equal(0.385, p[i].TopInnerR, 3);
            }
            Assert.True(rain);
        }

        [Fact]
        public void PlasticOldDnCodesStillMeanSeries1000()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                BaseCode = "DN1000",
                ShaftCode = "DN1000",
                NeckCode = "DN600"
            };
            var p = WellCatalog.Place(fill, 1.0, 3.0);
            Assert.Contains("DN1000", p[0].Code);
            Assert.Equal("Тридэвелл DN1000", WellCatalog.PlasticSeriesUiOf("DN1000"));
            Assert.Equal("Прямопроходная", WellCatalog.PlasticBaseUiOf("DN1000"));
            Assert.Equal("Конус", WellCatalog.PlasticConeUiOf("DN600"));
        }

        [Fact]
        public void ConcreteCatalogHasAllSeries()
        {
            var codes = WellCatalog.ConcreteCodes();
            Assert.Contains("ПН-7", codes);
            Assert.Contains("КС-7.3", codes);
            Assert.Contains("КС-10.12", codes);
            Assert.Contains("КС-15.3", codes);
            Assert.Contains("КС-20.12", codes);
            Assert.Contains("ПП-20", codes);
            var p = WellCatalog.AutoParts(1.50, 3.0);
            Assert.Equal("ПН-15", p[0].Code);
            Assert.Equal(0.70, WellCatalog.SeriesDiamM(0), 2);
            Assert.Equal(1.00, WellCatalog.SeriesDiamM(1), 2);
            Assert.Equal(1.50, WellCatalog.SeriesDiamM(2), 2);
            Assert.Equal(2.00, WellCatalog.SeriesDiamM(3), 2);
        }
    }
}
