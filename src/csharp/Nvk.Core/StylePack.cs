using System;
using System.Globalization;

namespace Nvk.Core
{
    /// <summary>Сериализация LineStyle / FillStyle в NOD: aci,r,g,b,ltype,weight и pat,aci,r,g,b,scale.</summary>
    public static class StylePack
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Line(LineStyle st)
        {
            if (st == null) return "";
            return st.Color.ToString(Inv) + ","
                + st.R.ToString(Inv) + ","
                + st.G.ToString(Inv) + ","
                + st.B.ToString(Inv) + ","
                + (string.IsNullOrEmpty(st.Linetype) ? "Continuous" : st.Linetype) + ","
                + st.Weight.ToString(Inv);
        }

        public static LineStyle ParseLine(string s, LineStyle fallback)
        {
            var st = CloneLine(fallback);
            if (string.IsNullOrWhiteSpace(s)) return st;
            var p = s.Split(',');
            if (p.Length < 4) return st;
            short aci;
            if (short.TryParse(p[0].Trim(), NumberStyles.Integer, Inv, out aci)) st.Color = aci;
            byte r, g, b;
            if (byte.TryParse(p[1].Trim(), NumberStyles.Integer, Inv, out r)) st.R = r;
            if (byte.TryParse(p[2].Trim(), NumberStyles.Integer, Inv, out g)) st.G = g;
            if (byte.TryParse(p[3].Trim(), NumberStyles.Integer, Inv, out b)) st.B = b;
            if (p.Length > 4 && !string.IsNullOrWhiteSpace(p[4])) st.Linetype = p[4].Trim();
            int w;
            if (p.Length > 5 && int.TryParse(p[5].Trim(), NumberStyles.Integer, Inv, out w))
                st.Weight = w;
            return st;
        }

        public static string Fill(FillStyle st)
        {
            if (st == null) return "";
            return (string.IsNullOrEmpty(st.Pattern) ? "ANSI31" : st.Pattern) + ","
                + st.Color.ToString(Inv) + ","
                + st.R.ToString(Inv) + ","
                + st.G.ToString(Inv) + ","
                + st.B.ToString(Inv) + ","
                + st.Scale.ToString("0.####", Inv);
        }

        public static FillStyle ParseFill(string s, FillStyle fallback)
        {
            var st = CloneFill(fallback);
            if (string.IsNullOrWhiteSpace(s)) return st;
            var p = s.Split(',');
            if (p.Length < 1) return st;
            if (!string.IsNullOrWhiteSpace(p[0])) st.Pattern = p[0].Trim();
            if (p.Length < 5) return st;
            short aci;
            if (short.TryParse(p[1].Trim(), NumberStyles.Integer, Inv, out aci)) st.Color = aci;
            byte r, g, b;
            if (byte.TryParse(p[2].Trim(), NumberStyles.Integer, Inv, out r)) st.R = r;
            if (byte.TryParse(p[3].Trim(), NumberStyles.Integer, Inv, out g)) st.G = g;
            if (byte.TryParse(p[4].Trim(), NumberStyles.Integer, Inv, out b)) st.B = b;
            double sc;
            if (p.Length > 5
                && double.TryParse(p[5].Trim().Replace(',', '.'), NumberStyles.Float, Inv, out sc)
                && sc > 0.0)
                st.Scale = sc;
            return st;
        }

        static LineStyle CloneLine(LineStyle src)
        {
            if (src == null) return LineStyle.Of(129, "Continuous", 15);
            return new LineStyle
            {
                Color = src.Color,
                R = src.R,
                G = src.G,
                B = src.B,
                Linetype = src.Linetype ?? "Continuous",
                Weight = src.Weight
            };
        }

        static FillStyle CloneFill(FillStyle src)
        {
            if (src == null) return FillStyle.Rgb("ANSI31", 0, 0, 255, 1);
            return new FillStyle
            {
                Pattern = src.Pattern ?? "ANSI31",
                Color = src.Color,
                R = src.R,
                G = src.G,
                B = src.B,
                Scale = src.Scale
            };
        }
    }
}
