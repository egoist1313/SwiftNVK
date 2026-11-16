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

        [Fact]
        public void PlasticShaftCodeKeepsDnAndHeightApart()
        {
            var a = WellCatalog.Find("Шахта DN1000×250");
            Assert.NotNull(a);
            Assert.Equal(WellPartKind.Shaft, a.Kind);
            Assert.Equal(1.00, a.InnerD, 3);
            Assert.Equal(1.090, a.OuterD, 3);
            Assert.Equal(0.25, a.Height, 3);
            var b = WellCatalog.Find("Шахта DN1500×250");
            Assert.Equal(1.50, b.InnerD, 3);
            Assert.Equal(1.590, b.OuterD, 3);
            Assert.Equal(0.25, b.Height, 3);
            var zero = WellCatalog.ParseParts("Шахта DN1000×250:0");
            Assert.Equal(1, zero.Count);
            Assert.Equal(0, zero[0].Qty);
        }

        [Fact]
        public void PlasticBothSeriesUseQuarterMeterRing()
        {
            var narrow = WellCatalog.PlasticShaftParts(1000, 2.04, "Прямопроходная", "Конус");
            Assert.Equal(1, narrow.Count);
            Assert.Contains("×250", narrow[0].Code);
            var wide = WellCatalog.PlasticShaftParts(1500, 2.74, "Прямопроходная", "Конус");
            Assert.Equal(1, wide.Count);
            Assert.Contains("DN1500×250", wide[0].Code);
            var full = WellCatalog.PlasticShaftParts(1500, 4.24, "Прямопроходная", "Конус");
            Assert.Equal(3, full.Count);
            Assert.Equal(1.00, full[0].Height, 2);
            Assert.Equal(0.50, full[1].Height, 2);
            Assert.Equal(0.25, full[2].Height, 2);
        }

        [Fact]
        public void PlasticManualQuarterRingsReplaceAuto()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                Auto = false,
                ShaftCode = "Тридэвелл DN1000",
                Parts = WellCatalog.ParseParts("Шахта DN1000×250:3")
            };
            var p = WellCatalog.Place(fill, 1.0, 4.0);
            int n = 0;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind != WellPartKind.Shaft) continue;
                Assert.Equal(0.25, p[i].Z1 - p[i].Z0, 3);
                n++;
            }
            Assert.Equal(3, n);
        }

        [Fact]
        public void OwnSetIsPerWellAndPerBody()
        {
            string packed = WellCatalog.FormatOwn(new[]
            {
                new WellOwnSet { Handle = "AB12", Plastic = false, Parts = "ПН-10:1;КС-10.9:2" },
                new WellOwnSet { Handle = "AB12", Plastic = true, Parts = "Шахта DN1000×250:3" }
            });
            var plas = WellCatalog.WithOwn(new WellFill
            {
                Body = WellBodyKind.Plastic,
                Auto = true
            }, "AB12", packed);
            Assert.False(plas.Auto);
            var drawn = WellCatalog.Place(plas, 1.0, 5.0);
            int n = 0;
            for (int i = 0; i < drawn.Count; i++)
                if (drawn[i].Kind == WellPartKind.Shaft) n++;
            Assert.Equal(3, n);

            var conc = WellCatalog.WithOwn(new WellFill { Auto = true }, "AB12", packed);
            Assert.False(conc.Auto);
            Assert.Equal("ПН-10", conc.Parts[0].Code);

            var other = WellCatalog.WithOwn(new WellFill { Auto = true }, "ZZ", packed);
            Assert.True(other.Auto);

            string next = WellCatalog.ReplaceOwn(packed, true, new[] { "AB12" }, new[]
            {
                new WellOwnSet { Handle = "AB12", Parts = "Шахта DN1500×1000:1" }
            });
            Assert.Contains("ПН-10", WellCatalog.OwnOf(next, "AB12", false).Parts);
            Assert.Contains("DN1500", WellCatalog.OwnOf(next, "AB12", true).Parts);
        }

        [Fact]
        public void RingSectionHasSpanningJointLines()
        {
            var p = new WellPartInst
            {
                Kind = WellPartKind.Ring,
                OuterR = 0.58,
                InnerR = 0.50,
                AxisX = 0
            };
            var loops = WellSection.LoopsOf(p, 0.0, 0.90);
            Assert.Equal(2, loops.Count);
            Assert.Equal(-0.58, loops[0][0].X, 3);
            Assert.Equal(0.58, loops[0][1].X, 3);
            Assert.Equal(0.0, loops[0][0].Y, 3);
            Assert.Equal(0.90, loops[0][2].Y, 3);
            Assert.Equal(-0.50, loops[1][0].X, 3);
            Assert.Equal(0.50, loops[1][1].X, 3);
        }

        [Fact]
        public void CoverAndShaftSectionsSpanDiameter()
        {
            var cover = new WellPartInst
            {
                Kind = WellPartKind.Cover,
                OuterR = 0.80,
                InnerR = 0.70,
                HoleR = 0.35,
                AxisX = 0
            };
            var c = WellSection.LoopsOf(cover, 2.0, 2.16);
            Assert.Equal(2, c.Count);
            Assert.Equal(-0.80, c[0][0].X, 3);
            Assert.Equal(0.80, c[0][1].X, 3);
            Assert.Equal(-0.35, c[1][0].X, 3);
            Assert.Equal(0.35, c[1][1].X, 3);

            var shaft = new WellPartInst
            {
                Kind = WellPartKind.Shaft,
                OuterR = 0.60,
                InnerR = 0.50,
                AxisX = 0
            };
            var s = WellSection.LoopsOf(shaft, 0.5, 1.5);
            Assert.Equal(2, s.Count);
            Assert.Equal(-0.60, s[0][0].X, 3);
            Assert.Equal(0.60, s[0][1].X, 3);
        }

        [Fact]
        public void ConeSectionIsNestedTrapezoids()
        {
            var p = new WellPartInst
            {
                Kind = WellPartKind.Cone,
                OuterR = 0.60,
                InnerR = 0.50,
                TopOuterR = 0.40,
                TopInnerR = 0.30,
                AxisX = 0,
                TopAxisX = 0.10
            };
            var loops = WellSection.LoopsOf(p, 1.0, 1.6);
            Assert.Equal(2, loops.Count);
            Assert.Equal(-0.60, loops[0][0].X, 3);
            Assert.Equal(0.60, loops[0][1].X, 3);
            Assert.Equal(0.50, loops[0][2].X, 3);
            Assert.Equal(-0.30, loops[0][3].X, 3);
            Assert.Equal(-0.50, loops[1][0].X, 3);
            Assert.Equal(0.40, loops[1][2].X, 3);
        }

        [Fact]
        public void BottomSectionIsSolidAndBaseHasInnerFromFloor()
        {
            var bot = new WellPartInst { Kind = WellPartKind.Bottom, OuterR = 0.58, AxisX = 0 };
            var b = WellSection.LoopsOf(bot, 0.0, 0.12);
            Assert.Single(b);
            Assert.Equal(-0.58, b[0][0].X, 3);
            Assert.Equal(0.58, b[0][1].X, 3);

            var bas = new WellPartInst
            {
                Kind = WellPartKind.Base,
                OuterR = 0.60,
                InnerR = 0.50,
                AxisX = 0
            };
            var loops = WellSection.LoopsOf(bas, 0.0, 0.50);
            Assert.Equal(2, loops.Count);
            Assert.Equal(0.0, loops[0][0].Y, 3);
            Assert.Equal(0.50, loops[0][2].Y, 3);
            Assert.True(loops[1][0].Y > 0.05);
            Assert.Equal(0.50, loops[1][2].Y, 3);
            Assert.Equal(-0.50, loops[1][0].X, 3);
        }

        [Fact]
        public void AutoStackCanOmitCoverAndHatch()
        {
            var fill = new WellFill { ShowCover = false, ShowHatch = false };
            var p = WellCatalog.AutoStack(1.00, 3.00, fill);
            Assert.True(p.Count >= 2);
            Assert.Equal(WellPartKind.Bottom, p[0].Kind);
            for (int i = 0; i < p.Count; i++)
            {
                Assert.NotEqual(WellPartKind.Cover, p[i].Kind);
                Assert.NotEqual(WellPartKind.Hatch, p[i].Kind);
                Assert.NotEqual(WellPartKind.Support, p[i].Kind);
            }
            Assert.Equal(WellPartKind.Ring, p[p.Count - 1].Kind);
            Assert.True(p[p.Count - 1].Z1 >= 2.85);
        }

        [Fact]
        public void PlaceExactHonorsCoverFlag()
        {
            var fill = new WellFill
            {
                Auto = false,
                ShowCover = false,
                ShowHatch = true,
                Parts = WellCatalog.ParseParts("ПН-10:1;КС-10.9:1;ПП-10:1;Люк-Т:1")
            };
            var p = WellCatalog.Place(fill, 1.0, 5.0);
            bool cover = false, hatch = false, ring = false;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind == WellPartKind.Cover) cover = true;
                if (p[i].Kind == WellPartKind.Hatch) hatch = true;
                if (p[i].Kind == WellPartKind.Ring) ring = true;
            }
            Assert.False(cover);
            Assert.True(hatch);
            Assert.True(ring);
        }

        [Fact]
        public void PlasticCanOmitHatchAndForm()
        {
            var fill = new WellFill
            {
                Body = WellBodyKind.Plastic,
                ShowCover = false,
                ShowHatch = false,
                ShaftCode = "Тридэвелл DN1000",
                NeckCode = "Конус"
            };
            var p = WellCatalog.Place(fill, 1.0, 3.0);
            bool cone = false, form = false, hatch = false, shaft = false;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind == WellPartKind.Cone) cone = true;
                if (p[i].Kind == WellPartKind.Form) form = true;
                if (p[i].Kind == WellPartKind.Hatch) hatch = true;
                if (p[i].Kind == WellPartKind.Shaft) shaft = true;
            }
            Assert.True(cone);
            Assert.True(shaft);
            Assert.False(form);
            Assert.False(hatch);
        }
    }
}
