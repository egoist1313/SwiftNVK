using System;

namespace Nvk.Core
{
    /// <summary>
    /// Коды-заполнители штампа/рамки, как ТРУБКОДЫ в PipeBuild.
    /// Рамку не рисуем: подставляем в уже лежащие TEXT/MTEXT/атрибуты.
    /// </summary>
    public static class SheetCodes
    {
        public const string Net = "%%NET%%";
        public const string Seg = "%%SEG%%";
        public const string Tol = "%%TOL%%";
        public const string Type = "%%TYPE%%";

        public static string Subst(string text, string net, string seg, string tol, string type)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            string t = text;
            t = ReplaceAll(t, Net, net ?? "");
            t = ReplaceAll(t, Seg, seg ?? "");
            t = ReplaceAll(t, Tol, tol ?? "");
            t = ReplaceAll(t, Type, type ?? "");
            return t;
        }

        public static bool HasCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf(Net, StringComparison.Ordinal) >= 0
                || text.IndexOf(Seg, StringComparison.Ordinal) >= 0
                || text.IndexOf(Tol, StringComparison.Ordinal) >= 0
                || text.IndexOf(Type, StringComparison.Ordinal) >= 0;
        }

        public static string TypeName(string net)
        {
            if (IsWater(net)) return "водоснабжения";
            if (IsSewer(net)) return "канализации";
            return "";
        }

        public static string TolText(string net)
        {
            if (IsWater(net))
            {
                return "Допустимые отклонения:\\P"
                    + "(СП 129.13330.2019)\\P"
                    + "п. 6.1.6 Максимальные отклонения от проектного положения:\\P"
                    + "Напорные трубопроводы:\\P"
                    + "  -  ось трубопровода в плане \\U+00B1100мм\\P"
                    + "  -  отметки верха трубопровода \\U+00B130мм";
            }
            if (IsSewer(net))
            {
                return "Допустимые отклонения:\\P"
                    + "(СП 129.13330.2019)\\P"
                    + "п. 6.1.6 Максимальные отклонения от проектного положения:\\P"
                    + "Безнапорные трубопроводы:\\P"
                    + "  -  отметки лотков \\U+00B15мм";
            }
            return "";
        }

        public static bool IsWater(string net)
        {
            return MatchNet(net, "В1", "В2", "B1", "B2");
        }

        public static bool IsSewer(string net)
        {
            return MatchNet(net, "К1", "К2", "K1", "K2");
        }

        static bool MatchNet(string net, params string[] keys)
        {
            if (string.IsNullOrEmpty(net) || keys == null) return false;
            string n = net.ToUpperInvariant();
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i].ToUpperInvariant();
                if (n.StartsWith(k, StringComparison.Ordinal) || n.EndsWith(k, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static string ReplaceAll(string text, string old, string neu)
        {
            if (string.IsNullOrEmpty(old) || text.IndexOf(old, StringComparison.Ordinal) < 0)
                return text;
            return text.Replace(old, neu ?? "");
        }
    }
}
