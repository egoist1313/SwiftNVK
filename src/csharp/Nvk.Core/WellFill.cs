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
        Neck = 7
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
    }

    /// <summary>Наполнение колодца с вкладки «Колодцы».</summary>
    public sealed class WellFill
    {
        public WellBodyKind Body = WellBodyKind.Concrete;
        public WellNeckSide Neck = WellNeckSide.Center;
        public bool Auto = true;
        public string BaseCode = "DN1000";
        public string ShaftCode = "DN1000";
        public string NeckCode = "DN600";
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
                        AxisX = 0.0
                    });
                    z += h;
                }
            }
            return dst;
        }

        static List<WellPartInst> PlacePlastic(WellFill fill, double innerD, double height)
        {
            var bas = PlasticOf(string.IsNullOrEmpty(fill.BaseCode) ? DnTag(innerD) : fill.BaseCode, WellPartKind.Base);
            var neck = PlasticOf(string.IsNullOrEmpty(fill.NeckCode) ? "DN600" : fill.NeckCode, WellPartKind.Neck);
            var shaft = PlasticOf(string.IsNullOrEmpty(fill.ShaftCode) ? DnTag(innerD) : fill.ShaftCode, WellPartKind.Shaft);
            double shaftH = height - bas.Height - neck.Height;
            if (shaftH < 0.20) shaftH = 0.20;
            shaft.Height = shaftH;
            double off = 0.0;
            double room = shaft.InnerD * 0.5 - neck.InnerD * 0.5;
            if (room < 0.0) room = 0.0;
            if (fill.Neck == WellNeckSide.Left) off = -room;
            else if (fill.Neck == WellNeckSide.Right) off = room;
            var dst = new List<WellPartInst>();
            double z = 0.0;
            dst.Add(Inst(bas, z, 0.0)); z += bas.Height;
            dst.Add(Inst(shaft, z, 0.0)); z += shaft.Height;
            dst.Add(Inst(neck, z, off));
            return dst;
        }

        static WellPartInst Inst(WellPart p, double z, double axisX)
        {
            double ir = p.InnerD * 0.5;
            double orad = p.OuterD * 0.5;
            return new WellPartInst
            {
                Code = p.Code,
                Kind = p.Kind,
                InnerR = ir,
                OuterR = orad,
                HoleR = ir,
                Z0 = z,
                Z1 = z + p.Height,
                AxisX = axisX
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
