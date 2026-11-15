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

        public static List<WellPartInst> Place(WellFill fill, double innerD, double height)
        {
            if (fill == null) fill = new WellFill();
            if (height < 0.4) height = 0.4;
            if (innerD < 0.3) innerD = 1.0;
            if (fill.Body == WellBodyKind.Plastic)
                return PlacePlastic(fill, innerD, height);
            if (!fill.Auto && fill.Parts != null && fill.Parts.Count > 0)
                return PlaceExact(fill.Parts);
            return AutoStack(innerD, height);
        }

        public static List<WellPart> AutoParts(double innerD, double height)
        {
            var inst = AutoStack(innerD, height);
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
            int ser = SeriesMm(innerD);
            var bot = Find("ПН-" + ser);
            var cover = Find("ПП-" + ser);
            var sup = Find("КО-6");
            var hatch = Find("Люк-Т");
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
            var dst = new List<WellPartInst>();
            if (parts == null) return dst;
            double z = 0.0;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                int n = p.Qty < 1 ? 1 : p.Qty;
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
            string baseUi = PlasticBaseUiOf(fill != null ? fill.BaseCode : null);
            string coneUi = PlasticConeUiOf(fill != null ? fill.NeckCode : null);
            bool rain = ser == 1000 && Has(coneUi, "дожд");
            bool steps = Has(coneUi, "ступ");
            bool dbl = ser == 1500 && Has(baseUi, "двойн");

            double shaftIn = ser * 0.001;
            double shaftOut = ser == 1000 ? 1.090 : 1.590;
            double neckIn = 0.680;
            double neckWall = 0.015;
            double flangeIn = ser == 1000 ? 0.927 : 1.417;
            double coneH = rain ? 0.720 : (ser == 1000 ? 0.750 : 1.000);
            double neckCylH = rain ? 0.0 : (ser == 1000 ? 0.432 : 0.398);
            double taperH = coneH - neckCylH;
            if (taperH < 0.15) taperH = coneH;
            double baseH = ser == 1000 ? 0.750 : 1.200;
            double formH = 0.200;
            var hatch = Find("Люк-Т");
            double hatchH = hatch != null ? hatch.Height : 0.09;

            double cap = baseH + coneH + formH + hatchH;
            double need = height - cap;
            if (need < 0.20) need = 0.20;
            var rings = TridevellRingHeights(ser);
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

            double room = shaftIn * 0.5 - neckIn * 0.5;
            if (room < 0.0) room = 0.0;
            double off = 0.0;
            if (fill != null && fill.Neck == WellNeckSide.Left) off = -room;
            else if (fill != null && fill.Neck == WellNeckSide.Right) off = room;

            var dst = new List<WellPartInst>();
            double z = 0.0;
            string baseCode = dbl ? "База DN1500 ДС" : ("База DN" + ser + " " + BaseShort(baseUi));
            dst.Add(Inst(baseCode, WellPartKind.Base, shaftIn * 0.5, shaftOut * 0.5, z, z + baseH, 0.0));
            z += baseH;
            for (int i = 0; i < rings.Length; i++)
            {
                for (int k = 0; k < qty[i]; k++)
                {
                    string rc = "Шахта DN" + ser + "×" + ((int)Math.Round(rings[i] * 1000)).ToString(CultureInfo.InvariantCulture);
                    dst.Add(Inst(rc, WellPartKind.Shaft, shaftIn * 0.5, shaftOut * 0.5, z, z + rings[i], 0.0));
                    z += rings[i];
                }
            }

            double botIr = flangeIn * 0.5;
            double botOr = shaftOut * 0.5;
            double topIr = rain ? 0.385 : neckIn * 0.5;
            double topOr = rain ? 0.400 : neckIn * 0.5 + neckWall;
            string coneCode = rain
                ? "Конус-Д DN1000×720"
                : (steps ? "Конус DN" + ser + " ст." : "Конус DN" + ser);
            var cone = Inst(coneCode, WellPartKind.Cone, botIr, botOr, z, z + taperH, 0.0);
            cone.TopInnerR = topIr;
            cone.TopOuterR = topOr;
            cone.TopAxisX = off;
            dst.Add(cone);
            z += taperH;
            if (neckCylH > 0.02)
            {
                dst.Add(Inst("Горловина DN" + ser, WellPartKind.Neck, topIr, topOr, z, z + neckCylH, off));
                z += neckCylH;
            }
            double formIn = 0.750;
            double formOut = ser == 1000 ? 1.250 : 1.750;
            dst.Add(Inst("Форма DN" + ser, WellPartKind.Form, formIn * 0.5, formOut * 0.5, z, z + formH, off));
            z += formH;
            if (hatch != null)
                dst.Add(Inst(hatch.Code, WellPartKind.Hatch, hatch.InnerD * 0.5, hatch.OuterD * 0.5, z, z + hatchH, off));
            return dst;
        }

        static double[] TridevellRingHeights(int ser)
        {
            if (ser == 1500) return new[] { 1.00, 0.50 };
            return new[] { 1.00, 0.50, 0.25 };
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
                int q = parts[i].Qty < 1 ? 1 : parts[i].Qty;
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
                p.Qty = q < 1 ? 1 : q;
                list.Add(p);
            }
            return list;
        }
    }
}
