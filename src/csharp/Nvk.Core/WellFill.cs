using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nvk.Core
{
    public enum WellBodyKind
    {
        Concrete = 0,
        Plastic = 1
    }

    public enum WellNeckSide
    {
        Center = 0,
        Left = 1,
        Right = 2
    }

    public enum WellPartKind
    {
        Bottom = 0,
        Ring = 1,
        Cover = 2,
        Support = 3,
        Hatch = 4,
        Base = 5,
        Shaft = 6,
        Neck = 7,
        Cone = 8,
        Form = 9
    }

    /// <summary>Элемент колодца (ГОСТ 8020 или пластик). Qty — число одинаковых.</summary>
    public sealed class WellPart
    {
        public string Code;
        public WellPartKind Kind;
        public double InnerD;
        public double OuterD;
        public double Height;
        public int Qty = 1;
    }

    /// <summary>Экземпляр на разрезе: от z0 до z1, м, ось X = смещение горловины.</summary>
    public sealed class WellPartInst
    {
        public string Code;
        public WellPartKind Kind;
        public double InnerR;
        public double OuterR;
        public double HoleR;
        public double Z0;
        public double Z1;
        public double AxisX;
        /// <summary>Верх трапеции (конус). 0 = цилиндр, как InnerR/OuterR.</summary>
        public double TopInnerR;
        public double TopOuterR;
        public double TopAxisX;
    }

    /// <summary>Наполнение колодца с вкладки «Колодцы».</summary>
    public sealed class WellFill
    {
        public WellBodyKind Body = WellBodyKind.Concrete;
        public WellNeckSide Neck = WellNeckSide.Center;
        public bool Auto = true;
        public string BaseCode = "Прямопроходная";
        public string ShaftCode = "Авто";
        public string NeckCode = "Конус";
        public List<WellPart> Parts = new List<WellPart>();
        /// <summary>false — без плиты перекрытия (ПП) и без формы пластика.</summary>
        public bool ShowCover = true;
        /// <summary>false — без люка и опорного кольца КО.</summary>
        public bool ShowHatch = true;
    }

    /// <summary>Пять разрезов/сечений колодца по этапам работ.</summary>
    public static class WellCutStage
    {
        public const int Count = 5;

        public static string Name(int stage)
        {
            switch (stage)
            {
                case 1: return "Разработка котлована";
                case 2: return "Песчаная подготовка";
                case 3: return "Колодец";
                case 4: return "Заделка труб";
                default: return "Обратная засыпка";
            }
        }

        public static string Title(int stage, bool cut)
        {
            string kind = cut ? "Разрез" : "Сечение";
            int n = stage < 1 ? 1 : stage;
            return kind + " " + n.ToString(CultureInfo.InvariantCulture)
                 + "-" + n.ToString(CultureInfo.InvariantCulture)
                 + ". " + Name(stage);
        }

        public static ProfileKind KindOf(int stage)
        {
            if (stage <= 1) return ProfileKind.Razrabotka;
            return ProfileKind.Pesok;
        }

        public static bool ShowsWell(int stage) { return stage >= 3; }
        public static bool ShowsPipes(int stage) { return stage >= 4; }
        public static bool ShowsBackfill(int stage) { return stage >= 5; }
    }

    /// <summary>Свой набор одного колодца. Handle — ручка структуры, без звёздочек.</summary>
    public sealed class WellOwnSet
    {
        public string Handle;
        public bool Plastic;
        public string Parts;
    }

    /// <summary>
    /// Каталог КС/ПН/ПП/КО (ГОСТ 8020) и пластиковых DN.
    /// Автоподбор колец по Ø и высоте колодца.
    /// </summary>
    public static class WellCatalog
    {
        public static WellPart[] Concrete =
        {
            P("ПН-7",  WellPartKind.Bottom,  0.70, 0.84, 0.10),
            P("ПН-10", WellPartKind.Bottom,  1.00, 1.16, 0.10),
            P("ПН-15", WellPartKind.Bottom,  1.50, 1.68, 0.12),
            P("ПН-20", WellPartKind.Bottom,  2.00, 2.20, 0.12),
            P("КС-7.3",  WellPartKind.Ring, 0.70, 0.84, 0.29),
            P("КС-7.6",  WellPartKind.Ring, 0.70, 0.84, 0.59),
            P("КС-7.9",  WellPartKind.Ring, 0.70, 0.84, 0.89),
            P("КС-10.3", WellPartKind.Ring, 1.00, 1.16, 0.29),
            P("КС-10.6", WellPartKind.Ring, 1.00, 1.16, 0.59),
            P("КС-10.9", WellPartKind.Ring, 1.00, 1.16, 0.89),
            P("КС-10.12",WellPartKind.Ring, 1.00, 1.16, 1.19),
            P("КС-15.3", WellPartKind.Ring, 1.50, 1.68, 0.29),
            P("КС-15.6", WellPartKind.Ring, 1.50, 1.68, 0.59),
            P("КС-15.9", WellPartKind.Ring, 1.50, 1.68, 0.89),
            P("КС-15.12",WellPartKind.Ring, 1.50, 1.68, 1.19),
            P("КС-20.6", WellPartKind.Ring, 2.00, 2.20, 0.59),
            P("КС-20.9", WellPartKind.Ring, 2.00, 2.20, 0.89),
            P("КС-20.12",WellPartKind.Ring, 2.00, 2.20, 1.19),
            P("ПП-7",  WellPartKind.Cover, 0.70, 0.84, 0.15),
            P("ПП-10", WellPartKind.Cover, 1.00, 1.16, 0.15),
            P("ПП-15", WellPartKind.Cover, 1.50, 1.68, 0.15),
            P("ПП-20", WellPartKind.Cover, 2.00, 2.20, 0.15),
            P("КО-6",  WellPartKind.Support, 0.70, 0.84, 0.07),
            P("Люк-Т", WellPartKind.Hatch, 0.60, 0.80, 0.09)
        };

        public static readonly string[] PlasticDn = { "DN400", "DN600", "DN800", "DN1000", "DN1200" };
        public static readonly string[] PlasticSeriesUi = { "Авто", "Тридэвелл DN1000", "Тридэвелл DN1500" };
        public static readonly string[] PlasticBaseUi =
        {
            "Прямопроходная", "Угловая", "Тройниковая", "Крестовинная", "Двойная стенка"
        };
        public static readonly string[] PlasticConeUi =
        {
            "Конус", "Конус со ступенями", "Дождеприемный"
        };
        public static readonly string[] ConcreteSeriesUi = { "Ø700", "Ø1000", "Ø1500", "Ø2000" };

        public static string[] ConcreteCodes()
        {
            var a = new string[Concrete.Length];
            for (int i = 0; i < Concrete.Length; i++) a[i] = Concrete[i].Code;
            return a;
        }

        public static double SeriesDiamM(int seriesUiIndex)
        {
            switch (seriesUiIndex)
            {
                case 0: return 0.70;
                case 2: return 1.50;
                case 3: return 2.00;
                default: return 1.00;
            }
        }

        public static int SeriesUiIndex(double innerD)
        {
            int s = SeriesMm(innerD);
            if (s <= 7) return 0;
            if (s <= 10) return 1;
            if (s <= 15) return 2;
            return 3;
        }

        static WellPart P(string code, WellPartKind k, double id, double od, double h)
        {
            return new WellPart { Code = code, Kind = k, InnerD = id, OuterD = od, Height = h, Qty = 1 };
        }

        public static WellPart Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            for (int i = 0; i < Concrete.Length; i++)
                if (string.Equals(Concrete[i].Code, code, StringComparison.OrdinalIgnoreCase))
                    return Clone(Concrete[i]);
            var shaft = ShaftFromCode(code);
            if (shaft != null) return shaft;
            return PlasticOf(code, WellPartKind.Shaft);
        }

        public static WellPart PlasticOf(string code, WellPartKind kind)
        {
            double d = DnMeters(code);
            if (d < 0.15) d = kind == WellPartKind.Neck ? 0.60 : 1.00;
            double wall = d <= 0.65 ? 0.03 : 0.04;
            double h = 0.50;
            if (kind == WellPartKind.Neck) h = 0.55;
            if (kind == WellPartKind.Base) h = 0.45;
            if (kind == WellPartKind.Shaft) h = 1.00;
            string c = "DN" + ((int)Math.Round(d * 1000)).ToString(CultureInfo.InvariantCulture);
            return new WellPart { Code = c, Kind = kind, InnerD = d, OuterD = d + 2.0 * wall, Height = h, Qty = 1 };
        }

        public static double DnMeters(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0.0;
            var sb = new StringBuilder();
            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];
                if (ch >= '0' && ch <= '9') sb.Append(ch);
            }
            if (sb.Length == 0) return 0.0;
            double mm;
            if (!double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out mm))
                return 0.0;
            return mm * 0.001;
        }

        public static int SeriesMm(double innerD)
        {
            int mm = (int)Math.Round(innerD * 1000.0);
            if (mm < 850) return 7;
            if (mm < 1250) return 10;
            if (mm < 1750) return 15;
            return 20;
        }

        public static bool Shows(WellPartKind kind, WellFill fill)
        {
            bool cover = fill == null || fill.ShowCover;
            bool hatch = fill == null || fill.ShowHatch;
            if (kind == WellPartKind.Cover || kind == WellPartKind.Form) return cover;
            if (kind == WellPartKind.Hatch || kind == WellPartKind.Support) return hatch;
            return true;
        }

        public static List<WellPartInst> Place(WellFill fill, double innerD, double height)
        {
            if (fill == null) fill = new WellFill();
            if (height < 0.4) height = 0.4;
            if (innerD < 0.3) innerD = 1.0;
            if (fill.Body == WellBodyKind.Plastic)
                return PlacePlastic(fill, innerD, height);
            if (!fill.Auto && fill.Parts != null && fill.Parts.Count > 0)
                return PlaceExact(fill.Parts, fill);
            return AutoStack(innerD, height, fill);
        }

        public static List<WellPart> AutoParts(double innerD, double height)
        {
            return AutoParts(innerD, height, null);
        }

        public static List<WellPart> AutoParts(double innerD, double height, WellFill fill)
        {
            var inst = AutoStack(innerD, height, fill);
            var list = new List<WellPart>();
            for (int i = 0; i < inst.Count; i++)
            {
                var p = inst[i];
                if (list.Count > 0 && list[list.Count - 1].Code == p.Code)
                {
                    list[list.Count - 1].Qty++;
                    continue;
                }
                var src = Find(p.Code);
                if (src == null)
                {
                    src = new WellPart
                    {
                        Code = p.Code,
                        Kind = p.Kind,
                        InnerD = p.InnerR * 2.0,
                        OuterD = p.OuterR * 2.0,
                        Height = p.Z1 - p.Z0,
                        Qty = 1
                    };
                }
                src.Qty = 1;
                list.Add(src);
            }
            return list;
        }

        public static List<WellPartInst> AutoStack(double innerD, double height)
        {
            return AutoStack(innerD, height, null);
        }

        public static List<WellPartInst> AutoStack(double innerD, double height, WellFill fill)
        {
            int ser = SeriesMm(innerD);
            var bot = Find("ПН-" + ser);
            var cover = Shows(WellPartKind.Cover, fill) ? Find("ПП-" + ser) : null;
            var sup = Shows(WellPartKind.Support, fill) ? Find("КО-6") : null;
            var hatch = Shows(WellPartKind.Hatch, fill) ? Find("Люк-Т") : null;
            var rings = RingsOf(ser);
            double cap = 0.0;
            if (bot != null) cap += bot.Height;
            if (cover != null) cap += cover.Height;
            if (sup != null) cap += sup.Height;
            if (hatch != null) cap += hatch.Height;
            double need = height - cap;
            if (need < 0.20) need = 0.20;
            var qty = new int[rings.Count];
            for (int i = 0; i < rings.Count; i++) qty[i] = 0;
            double left = need;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                while (left + 1e-6 >= r.Height)
                {
                    qty[i]++;
                    left -= r.Height;
                }
            }
            if (left > 0.04 && rings.Count > 0)
            {
                int last = rings.Count - 1;
                qty[last]++;
            }
            var parts = new List<WellPart>();
            if (bot != null) parts.Add(bot);
            for (int i = 0; i < rings.Count; i++)
            {
                if (qty[i] <= 0) continue;
                var r = Clone(rings[i]);
                r.Qty = qty[i];
                parts.Add(r);
            }
            if (cover != null) parts.Add(cover);
            if (sup != null) parts.Add(sup);
            if (hatch != null) parts.Add(hatch);
            return PlaceExact(parts);
        }

        static List<WellPart> RingsOf(int ser)
        {
            var list = new List<WellPart>();
            string[] codes =
            {
                "КС-" + ser + ".12",
                "КС-" + ser + ".9",
                "КС-" + ser + ".6",
                "КС-" + ser + ".3"
            };
            for (int i = 0; i < codes.Length; i++)
            {
                var p = Find(codes[i]);
                if (p != null && p.Kind == WellPartKind.Ring) list.Add(p);
            }
            return list;
        }

        public static List<WellPartInst> PlaceExact(IList<WellPart> parts)
        {
            return PlaceExact(parts, null);
        }

        public static List<WellPartInst> PlaceExact(IList<WellPart> parts, WellFill fill)
        {
            var dst = new List<WellPartInst>();
            if (parts == null) return dst;
            double z = 0.0;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                if (!Shows(p.Kind, fill)) continue;
                int n = p.Qty;
                if (n < 1) continue;
                double h = p.Height > 0.02 ? p.Height : 0.29;
                double ir = p.InnerD * 0.5;
                double orad = p.OuterD * 0.5;
                if (orad < ir + 0.02) orad = ir + 0.08;
                double hole = p.Kind == WellPartKind.Cover ? 0.35 : ir;
                if (p.Kind == WellPartKind.Hatch) hole = ir;
                for (int k = 0; k < n; k++)
                {
                    dst.Add(new WellPartInst
                    {
                        Code = p.Code,
                        Kind = p.Kind,
                        InnerR = ir,
                        OuterR = orad,
                        HoleR = hole,
                        Z0 = z,
                        Z1 = z + h,
                        AxisX = 0.0,
                        TopInnerR = ir,
                        TopOuterR = orad,
                        TopAxisX = 0.0
                    });
                    z += h;
                }
            }
            return dst;
        }

        /// <summary>Типоразмер Тридэвелл: 1000 или 1500. «Авто» — по Ø колодца Civil.</summary>
        public static int PlasticSeriesMm(string shaftCode, double innerD)
        {
            if (!string.IsNullOrEmpty(shaftCode))
            {
                string s = shaftCode.Trim();
                if (Has(s, "авто")) { /* по innerD */ }
                else if (Has(s, "1500")) return 1500;
                else if (Has(s, "1000")) return 1000;
                else
                {
                    double d = DnMeters(s);
                    if (d >= 1.25) return 1500;
                    if (d >= 0.30) return 1000;
                }
            }
            return innerD < 1.25 ? 1000 : 1500;
        }

        public static string PlasticSeriesUiOf(string shaftCode)
        {
            if (string.IsNullOrEmpty(shaftCode) || Has(shaftCode, "авто")) return PlasticSeriesUi[0];
            if (Has(shaftCode, "1500")) return PlasticSeriesUi[2];
            if (Has(shaftCode, "1000") || DnMeters(shaftCode) >= 0.30) return PlasticSeriesUi[1];
            return PlasticSeriesUi[0];
        }

        public static string PlasticBaseUiOf(string baseCode)
        {
            if (string.IsNullOrEmpty(baseCode)) return PlasticBaseUi[0];
            if (Has(baseCode, "угл")) return PlasticBaseUi[1];
            if (Has(baseCode, "тройн")) return PlasticBaseUi[2];
            if (Has(baseCode, "крест")) return PlasticBaseUi[3];
            if (Has(baseCode, "двойн") || Has(baseCode, "дс")) return PlasticBaseUi[4];
            return PlasticBaseUi[0];
        }

        public static string PlasticConeUiOf(string neckCode)
        {
            if (string.IsNullOrEmpty(neckCode)) return PlasticConeUi[0];
            if (Has(neckCode, "дожд")) return PlasticConeUi[2];
            if (Has(neckCode, "ступ")) return PlasticConeUi[1];
            return PlasticConeUi[0];
        }

        static List<WellPartInst> PlacePlastic(WellFill fill, double innerD, double height)
        {
            int ser = PlasticSeriesMm(fill != null ? fill.ShaftCode : null, innerD);
            int[] custom = CustomShaftQty(fill, ref ser);
            string baseUi = PlasticBaseUiOf(fill != null ? fill.BaseCode : null);
            string coneUi = PlasticConeUiOf(fill != null ? fill.NeckCode : null);
            var g = PlasOf(ser, baseUi, coneUi);

            double need = height - CapOf(g, fill);
            if (need < 0.20) need = 0.20;
            var rings = TridevellRingHeights(ser);
            var qty = custom ?? GreedyRings(rings, need);

            double room = g.ShaftIn * 0.5 - g.NeckIn * 0.5;
            if (room < 0.0) room = 0.0;
            double off = 0.0;
            if (fill != null && fill.Neck == WellNeckSide.Left) off = -room;
            else if (fill != null && fill.Neck == WellNeckSide.Right) off = room;

            var dst = new List<WellPartInst>();
            double z = 0.0;
            string baseCode = g.Dbl ? "База DN1500 ДС" : ("База DN" + ser + " " + BaseShort(baseUi));
            dst.Add(Inst(baseCode, WellPartKind.Base, g.ShaftIn * 0.5, g.ShaftOut * 0.5, z, z + g.BaseH, 0.0));
            z += g.BaseH;
            for (int i = 0; i < rings.Length && i < qty.Length; i++)
            {
                for (int k = 0; k < qty[i]; k++)
                {
                    var shaft = MakePlasticShaft(ser, rings[i]);
                    dst.Add(Inst(shaft.Code, WellPartKind.Shaft, g.ShaftIn * 0.5, g.ShaftOut * 0.5, z, z + rings[i], 0.0));
                    z += rings[i];
                }
            }

            double botIr = g.FlangeIn * 0.5;
            double botOr = g.ShaftOut * 0.5;
            double topIr = g.Rain ? 0.385 : g.NeckIn * 0.5;
            double topOr = g.Rain ? 0.400 : g.NeckIn * 0.5 + g.NeckWall;
            string coneCode = g.Rain
                ? "Конус-Д DN1000×720"
                : (g.Steps ? "Конус DN" + ser + " ст." : "Конус DN" + ser);
            var cone = Inst(coneCode, WellPartKind.Cone, botIr, botOr, z, z + g.TaperH, 0.0);
            cone.TopInnerR = topIr;
            cone.TopOuterR = topOr;
            cone.TopAxisX = off;
            dst.Add(cone);
            z += g.TaperH;
            if (g.NeckCylH > 0.02)
            {
                dst.Add(Inst("Горловина DN" + ser, WellPartKind.Neck, topIr, topOr, z, z + g.NeckCylH, off));
                z += g.NeckCylH;
            }
            if (Shows(WellPartKind.Form, fill))
            {
                dst.Add(Inst("Форма DN" + ser, WellPartKind.Form, g.FormIn * 0.5, g.FormOut * 0.5, z, z + g.FormH, off));
                z += g.FormH;
            }
            var hatch = Shows(WellPartKind.Hatch, fill) ? Find("Люк-Т") : null;
            if (hatch != null)
                dst.Add(Inst(hatch.Code, WellPartKind.Hatch, hatch.InnerD * 0.5, hatch.OuterD * 0.5, z, z + g.HatchH, off));
            return dst;
        }

        static double CapOf(PlasGeom g, WellFill fill)
        {
            double cap = g.BaseH + g.ConeH;
            if (Shows(WellPartKind.Form, fill)) cap += g.FormH;
            if (Shows(WellPartKind.Hatch, fill)) cap += g.HatchH;
            return cap;
        }

        struct PlasGeom
        {
            public bool Rain, Steps, Dbl;
            public double ShaftIn, ShaftOut, NeckIn, NeckWall, FlangeIn;
            public double BaseH, ConeH, NeckCylH, TaperH, FormH, HatchH, Cap;
            public double FormIn, FormOut;
        }

        static PlasGeom PlasOf(int ser, string baseUi, string coneUi)
        {
            var g = new PlasGeom();
            if (ser != 1500) ser = 1000;
            g.Rain = ser == 1000 && Has(coneUi, "дожд");
            g.Steps = Has(coneUi, "ступ");
            g.Dbl = ser == 1500 && Has(baseUi, "двойн");
            g.ShaftIn = ser * 0.001;
            g.ShaftOut = ser == 1000 ? 1.090 : 1.590;
            g.NeckIn = 0.680;
            g.NeckWall = 0.015;
            g.FlangeIn = ser == 1000 ? 0.927 : 1.417;
            g.ConeH = g.Rain ? 0.720 : (ser == 1000 ? 0.750 : 1.000);
            g.NeckCylH = g.Rain ? 0.0 : (ser == 1000 ? 0.432 : 0.398);
            g.TaperH = g.ConeH - g.NeckCylH;
            if (g.TaperH < 0.15) g.TaperH = g.ConeH;
            g.BaseH = ser == 1000 ? 0.750 : 1.200;
            g.FormH = 0.200;
            var hatch = Find("Люк-Т");
            g.HatchH = hatch != null ? hatch.Height : 0.09;
            g.Cap = g.BaseH + g.ConeH + g.FormH + g.HatchH;
            g.FormIn = 0.750;
            g.FormOut = ser == 1000 ? 1.250 : 1.750;
            return g;
        }

        /// <summary>Кольца шахты Тридэвелл, м: 1,00 / 0,50 / 0,25. Обе серии.</summary>
        public static double[] PlasticShaftHeights()
        {
            return new[] { 1.00, 0.50, 0.25 };
        }

        static double[] TridevellRingHeights(int ser)
        {
            return PlasticShaftHeights();
        }

        public static WellPart MakePlasticShaft(int ser, double heightM)
        {
            if (ser != 1500) ser = 1000;
            double h = 1.00;
            if (heightM < 0.375) h = 0.25;
            else if (heightM < 0.75) h = 0.50;
            int mm = (int)Math.Round(h * 1000.0);
            double id = ser * 0.001;
            double od = ser == 1000 ? 1.090 : 1.590;
            return new WellPart
            {
                Code = "Шахта DN" + ser.ToString(CultureInfo.InvariantCulture)
                     + "×" + mm.ToString(CultureInfo.InvariantCulture),
                Kind = WellPartKind.Shaft,
                InnerD = id,
                OuterD = od,
                Height = h,
                Qty = 1
            };
        }

        public static bool TryPlasticShaft(string code, out int ser, out double height)
        {
            ser = 0;
            height = 0;
            if (!Has(code, "шахт")) return false;
            int i = code.IndexOf("DN", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i += 2;
            ser = ReadDigits(code, ref i);
            if (ser != 1000 && ser != 1500) return false;
            while (i < code.Length && (code[i] < '0' || code[i] > '9')) i++;
            int mm = ReadDigits(code, ref i);
            if (mm == 250) height = 0.25;
            else if (mm == 500) height = 0.50;
            else if (mm == 1000) height = 1.00;
            else return false;
            return true;
        }

        static WellPart ShaftFromCode(string code)
        {
            int ser;
            double h;
            if (!TryPlasticShaft(code, out ser, out h)) return null;
            return MakePlasticShaft(ser, h);
        }

        static int ReadDigits(string s, ref int i)
        {
            int n = 0;
            bool any = false;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                any = true;
                n = n * 10 + (s[i] - '0');
                i++;
                if (n > 1000000) break;
            }
            return any ? n : 0;
        }

        static int[] GreedyRings(double[] rings, double need)
        {
            var qty = new int[rings.Length];
            double left = need;
            for (int i = 0; i < rings.Length; i++)
            {
                while (left + 1e-6 >= rings[i])
                {
                    qty[i]++;
                    left -= rings[i];
                }
            }
            if (left > 0.04 && rings.Length > 0)
                qty[qty.Length - 1]++;
            return qty;
        }

        /// <summary>Число колец 1 / 0,5 / 0,25 м на остаток высоты после базы, конуса, формы и люка.</summary>
        public static int[] PlasticShaftQty(int ser, double wellHeight, string baseCode, string neckCode)
        {
            return PlasticShaftQty(ser, wellHeight, baseCode, neckCode, null);
        }

        public static int[] PlasticShaftQty(int ser, double wellHeight, string baseCode, string neckCode, WellFill fill)
        {
            if (ser != 1500) ser = 1000;
            var g = PlasOf(ser, PlasticBaseUiOf(baseCode), PlasticConeUiOf(neckCode));
            double need = wellHeight - CapOf(g, fill);
            if (need < 0.20) need = 0.20;
            return GreedyRings(TridevellRingHeights(ser), need);
        }

        public static List<WellPart> PlasticShaftParts(int ser, double wellHeight, string baseCode, string neckCode)
        {
            return PlasticShaftParts(ser, wellHeight, baseCode, neckCode, null);
        }

        public static List<WellPart> PlasticShaftParts(int ser, double wellHeight, string baseCode, string neckCode, WellFill fill)
        {
            if (ser != 1500) ser = 1000;
            var rings = TridevellRingHeights(ser);
            var qty = PlasticShaftQty(ser, wellHeight, baseCode, neckCode, fill);
            var list = new List<WellPart>();
            for (int i = 0; i < rings.Length; i++)
            {
                if (qty[i] <= 0) continue;
                var p = MakePlasticShaft(ser, rings[i]);
                p.Qty = qty[i];
                list.Add(p);
            }
            return list;
        }

        /// <summary>
        /// Ручные шахты из Parts, если авто выключено.
        /// Ноль — кольца этой высоты нет. null — считать автоматом.
        /// </summary>
        static int[] CustomShaftQty(WellFill fill, ref int ser)
        {
            if (fill == null || fill.Auto || fill.Parts == null || fill.Parts.Count == 0)
                return null;
            int found = 0;
            bool any = false;
            for (int i = 0; i < fill.Parts.Count; i++)
            {
                var p = fill.Parts[i];
                if (p == null) continue;
                int ps;
                double h;
                if (!TryPlasticShaft(p.Code, out ps, out h)) continue;
                if (!any) found = ps;
                any = true;
            }
            if (!any) return null;
            ser = found == 1500 ? 1500 : 1000;
            var rings = TridevellRingHeights(ser);
            var qty = new int[rings.Length];
            for (int i = 0; i < fill.Parts.Count; i++)
            {
                var p = fill.Parts[i];
                if (p == null) continue;
                int ps;
                double h;
                if (!TryPlasticShaft(p.Code, out ps, out h) || ps != ser) continue;
                int q = p.Qty < 0 ? 0 : p.Qty;
                for (int r = 0; r < rings.Length; r++)
                {
                    if (Math.Abs(rings[r] - h) < 0.02)
                    {
                        qty[r] += q;
                        break;
                    }
                }
            }
            return qty;
        }

        static string BaseShort(string ui)
        {
            if (Has(ui, "угл")) return "угл.";
            if (Has(ui, "тройн")) return "тр.";
            if (Has(ui, "крест")) return "крест.";
            if (Has(ui, "двойн")) return "ДС";
            return "пр.";
        }

        static bool Has(string s, string token)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(token)) return false;
            return s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static WellPartInst Inst(WellPart p, double z, double axisX)
        {
            double ir = p.InnerD * 0.5;
            double orad = p.OuterD * 0.5;
            return Inst(p.Code, p.Kind, ir, orad, z, z + p.Height, axisX);
        }

        static WellPartInst Inst(string code, WellPartKind kind, double ir, double orad,
            double z0, double z1, double axisX)
        {
            if (orad < ir + 0.01) orad = ir + 0.045;
            return new WellPartInst
            {
                Code = code,
                Kind = kind,
                InnerR = ir,
                OuterR = orad,
                HoleR = ir,
                Z0 = z0,
                Z1 = z1,
                AxisX = axisX,
                TopInnerR = ir,
                TopOuterR = orad,
                TopAxisX = axisX
            };
        }

        static string DnTag(double d)
        {
            int mm = (int)Math.Round(d * 1000.0);
            if (mm < 500) mm = 400;
            else if (mm < 700) mm = 600;
            else if (mm < 900) mm = 800;
            else if (mm < 1100) mm = 1000;
            else mm = 1200;
            return "DN" + mm.ToString(CultureInfo.InvariantCulture);
        }

        static WellPart Clone(WellPart p)
        {
            return new WellPart
            {
                Code = p.Code,
                Kind = p.Kind,
                InnerD = p.InnerD,
                OuterD = p.OuterD,
                Height = p.Height,
                Qty = p.Qty
            };
        }

        public static string FormatParts(IList<WellPart> parts)
        {
            if (parts == null || parts.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null || string.IsNullOrEmpty(parts[i].Code)) continue;
                if (sb.Length > 0) sb.Append(';');
                int q = parts[i].Qty < 0 ? 0 : parts[i].Qty;
                sb.Append(parts[i].Code);
                sb.Append(':');
                sb.Append(q.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static List<WellPart> ParseParts(string s)
        {
            var list = new List<WellPart>();
            if (string.IsNullOrEmpty(s)) return list;
            var chunks = s.Split(';');
            for (int i = 0; i < chunks.Length; i++)
            {
                string t = chunks[i].Trim();
                if (t.Length == 0) continue;
                int colon = t.LastIndexOf(':');
                string code = t;
                int q = 1;
                if (colon > 0)
                {
                    code = t.Substring(0, colon).Trim();
                    int.TryParse(t.Substring(colon + 1).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out q);
                }
                var p = Find(code);
                if (p == null) continue;
                if (q < 0) q = 0;
                p.Qty = q;
                list.Add(p);
            }
            return list;
        }

        /// <summary>HANDLE*C*коды или HANDLE*P*коды, колодцы через |.</summary>
        public static string FormatOwn(IList<WellOwnSet> sets)
        {
            if (sets == null || sets.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < sets.Count; i++)
            {
                var s = sets[i];
                if (s == null || string.IsNullOrEmpty(s.Handle)) continue;
                if (string.IsNullOrEmpty(s.Parts)) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append(s.Handle.Trim());
                sb.Append(s.Plastic ? "*P*" : "*C*");
                sb.Append(s.Parts);
            }
            return sb.ToString();
        }

        public static List<WellOwnSet> ParseOwn(string packed)
        {
            var list = new List<WellOwnSet>();
            if (string.IsNullOrEmpty(packed)) return list;
            var chunks = packed.Split('|');
            for (int i = 0; i < chunks.Length; i++)
            {
                string t = chunks[i].Trim();
                if (t.Length == 0) continue;
                int a = t.IndexOf('*');
                if (a <= 0) continue;
                int b = t.IndexOf('*', a + 1);
                if (b <= a) continue;
                string body = t.Substring(a + 1, b - a - 1);
                list.Add(new WellOwnSet
                {
                    Handle = t.Substring(0, a).Trim(),
                    Plastic = body.Equals("P", StringComparison.OrdinalIgnoreCase)
                           || body.Equals("П", StringComparison.OrdinalIgnoreCase),
                    Parts = t.Substring(b + 1)
                });
            }
            return list;
        }

        public static WellOwnSet OwnOf(string packed, string handle, bool plastic)
        {
            if (string.IsNullOrEmpty(handle)) return null;
            var all = ParseOwn(packed);
            WellOwnSet found = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Plastic != plastic) continue;
                if (string.Equals(all[i].Handle, handle, StringComparison.OrdinalIgnoreCase))
                    found = all[i];
            }
            return found;
        }

        /// <summary>
        /// Заменить свои наборы этого типа у перечисленных колодцев.
        /// Чужой тип и ручки не из списка остаются.
        /// </summary>
        public static string ReplaceOwn(string packed, bool plastic, IList<string> handles, IList<WellOwnSet> own)
        {
            var keep = new List<WellOwnSet>();
            var all = ParseOwn(packed);
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s.Plastic == plastic && Listed(handles, s.Handle)) continue;
                keep.Add(s);
            }
            if (own != null)
            {
                for (int i = 0; i < own.Count; i++)
                {
                    var s = own[i];
                    if (s == null || string.IsNullOrEmpty(s.Handle) || string.IsNullOrEmpty(s.Parts))
                        continue;
                    s.Plastic = plastic;
                    keep.Add(s);
                }
            }
            return FormatOwn(keep);
        }

        static bool Listed(IList<string> handles, string handle)
        {
            if (handles == null || string.IsNullOrEmpty(handle)) return false;
            for (int i = 0; i < handles.Count; i++)
                if (string.Equals(handles[i], handle, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Свой набор этой ручки и этого типа тела выключает авто и подставляет части.</summary>
        public static WellFill WithOwn(WellFill fill, string handle, string packed)
        {
            if (fill == null) fill = new WellFill();
            var own = OwnOf(packed, handle, fill.Body == WellBodyKind.Plastic);
            if (own == null || string.IsNullOrEmpty(own.Parts)) return fill;
            bool plas = fill.Body == WellBodyKind.Plastic;
            if (own.Plastic != plas) return fill;
            var parts = ParseParts(own.Parts);
            if (parts.Count == 0) return fill;
            fill.Auto = false;
            fill.Parts = parts;
            return fill;
        }
    }
}
