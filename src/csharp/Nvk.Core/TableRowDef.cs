using System;
using System.Collections.Generic;
using System.Text;

namespace Nvk.Core
{
    /// <summary>Определение одной строки таблицы профиля — тип, источник, подпись, знаки, флаги.</summary>
    public sealed class TableRowDef
    {
        public SheetRowKind Type        { get; set; }
        public HeightSrc    Src         { get; set; }
        public bool         IsFact      { get; set; }   // устаревший флаг (backward compat)
        public string       Label       { get; set; }
        public int          RoundTo     { get; set; }   // 0 = брать из глобального TableCfg.RoundTo
        public bool         Show        { get; set; } = true;
        public bool         IsProjFact  { get; set; }   // разбить на две строки: «, проект» + «, факт»
        public bool         UseScatter  { get; set; }   // случайный разброс на этой отметке
        public double       ScatterAmp  { get; set; }   // ± м; 0 = DefaultAmp уровня
        public bool         AmpInRecord { get; set; }   // в сериализации есть поле амплитуды (v0.116+)

        // ── Сериализация ─────────────────────────────────────────────────────
        // Формат: "H|TrenchBottom|0|Подпись|2|1|0|0|0.05"
        //          type|src|IsFact|label|roundTo|show|IsProjFact|UseScatter|scatterAmp
        // Поля 6 и 7 — v0.109; поле 8 — v0.116.

        public string Serialize()
        {
            string t;
            switch (Type)
            {
                case SheetRowKind.Height:  t = "H"; break;
                case SheetRowKind.Length:  t = "L"; break;
                case SheetRowKind.Slope:   t = "S"; break;
                case SheetRowKind.Diam:    t = "D"; break;
                case SheetRowKind.Manhole: t = "M"; break;
                default:                   t = "H"; break;
            }
            string src = Type == SheetRowKind.Height ? Src.ToString() : "";
            string lbl = (Label ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace(";", "\\;");
            string amp = ScatterAmp.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return t + "|" + src + "|" + (IsFact ? "1" : "0") + "|"
                + lbl + "|" + RoundTo + "|" + (Show ? "1" : "0")
                + "|" + (IsProjFact ? "1" : "0") + "|" + (UseScatter ? "1" : "0")
                + "|" + amp;
        }

        public static TableRowDef Deserialize(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = SplitBy(s, '|');
            if (parts.Count < 6) return null;
            var d = new TableRowDef();
            switch (parts[0].Trim().ToUpperInvariant())
            {
                case "H": d.Type = SheetRowKind.Height;  break;
                case "L": d.Type = SheetRowKind.Length;  break;
                case "S": d.Type = SheetRowKind.Slope;   break;
                case "D": d.Type = SheetRowKind.Diam;    break;
                case "M": d.Type = SheetRowKind.Manhole; break;
                default:  d.Type = SheetRowKind.Height;  break;
            }
            if (d.Type == SheetRowKind.Height && !string.IsNullOrEmpty(parts[1].Trim()))
            {
                HeightSrc hs;
                if (Enum.TryParse(parts[1].Trim(), out hs)) d.Src = hs;
            }
            d.IsFact = parts[2].Trim() == "1";
            d.Label  = Unescape(parts[3]);
            int rt;
            d.RoundTo     = int.TryParse(parts[4].Trim(), out rt) ? rt : 0;
            d.Show        = parts[5].Trim() != "0";
            d.IsProjFact  = parts.Count > 6 && parts[6].Trim() == "1";
            d.UseScatter  = parts.Count > 7 && parts[7].Trim() == "1";
            if (parts.Count > 8)
            {
                double a;
                if (double.TryParse(parts[8].Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out a))
                    d.ScatterAmp = a;
                d.AmpInRecord = true;
            }
            // backward compat: IsFact=true в старых данных → UseScatter=true
            if (d.IsFact) d.UseScatter = true;
            return d;
        }

        public static string SerializeList(IList<TableRowDef> rows)
        {
            if (rows == null || rows.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(rows[i].Serialize());
            }
            return sb.ToString();
        }

        public static List<TableRowDef> DeserializeList(string s)
        {
            var list = new List<TableRowDef>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var rec in SplitBy(s, ';'))
            {
                var d = Deserialize(rec.Trim());
                if (d != null) list.Add(d);
            }
            return list;
        }

        // ── Имена для UI ─────────────────────────────────────────────────────

        public static string SrcDisplayName(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.TrenchBottom: return "Дно траншеи";
                case HeightSrc.SandTop:      return "Верх песка";
                case HeightSrc.Invert:       return "Лоток трубы";
                case HeightSrc.PipeTop:      return "Верх трубы";
                case HeightSrc.ZashTop:      return "Верх защиты";
                case HeightSrc.Surface:      return "Засыпка / земля";
                case HeightSrc.RoadTop:      return "Покрытие (дизайн)";
                case HeightSrc.BlackEarth:   return "Чёрная земля";
                case HeightSrc.DeltaZash:    return "ΔH защиты";
                default:                     return src.ToString();
            }
        }

        public static HeightSrc SrcFromDisplayName(string s)
        {
            switch (s)
            {
                case "Дно траншеи":       return HeightSrc.TrenchBottom;
                case "Верх песка":        return HeightSrc.SandTop;
                case "Лоток трубы":       return HeightSrc.Invert;
                case "Верх трубы":        return HeightSrc.PipeTop;
                case "Верх защиты":       return HeightSrc.ZashTop;
                case "Засыпка / земля":   return HeightSrc.Surface;
                case "Покрытие (дизайн)": return HeightSrc.RoadTop;
                case "Чёрная земля":      return HeightSrc.BlackEarth;
                case "ΔH защиты":         return HeightSrc.DeltaZash;
                default:                  return HeightSrc.TrenchBottom;
            }
        }

        // ── Дефолтные конфигурации ────────────────────────────────────────────

        public static List<TableRowDef> Defaults(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return DefaultsL1();
                case ProfileKind.Pesok:      return DefaultsL2();
                case ProfileKind.Truba:      return DefaultsL3();
                case ProfileKind.Zashita:    return DefaultsL4();
                case ProfileKind.Zasypka:    return DefaultsL5();
                default: return new List<TableRowDef>();
            }
        }

        static List<TableRowDef> DefaultsL1() => new List<TableRowDef>
        {
            D(SheetRowKind.Height,  HeightSrc.RoadTop,      "Проектно-планировочное\\Pпокрытие, м",   2, true),
            D(SheetRowKind.Height,  HeightSrc.BlackEarth,   ProfileRows.EarthLabel,                  2, true),
            D(SheetRowKind.Height,  HeightSrc.TrenchBottom, "Отметка дна\\Pтраншеи, м",               2, true, true, 0.05),
            D(SheetRowKind.Length,  default,                "Длина, м",                               2, true),
            D(SheetRowKind.Diam,    default,                "%%c трубопровода",                        2, true),
            D(SheetRowKind.Manhole, default,                "Номер колодца, точки,\\Pуглы поворота",   2, true),
        };

        static List<TableRowDef> DefaultsL2() => new List<TableRowDef>
        {
            D(SheetRowKind.Height,  HeightSrc.SandTop,      "Отметка верха\\Pпостели из песка, м",    2, true, true, 0.05),
            D(SheetRowKind.Height,  HeightSrc.TrenchBottom, "Отметка дна\\Pтраншеи, м",               2, true, true, 0.05),
            D(SheetRowKind.Length,  default,                "Длина, м",                               2, true),
            D(SheetRowKind.Diam,    default,                "%%c трубопровода",                        2, true),
            D(SheetRowKind.Manhole, default,                "Номер колодца, точки,\\Pуглы поворота",   2, true),
        };

        static List<TableRowDef> DefaultsL3() => new List<TableRowDef>
        {
            D(SheetRowKind.Slope,   default,                "Уклон, ‰",                               1, true),
            D(SheetRowKind.Height,  HeightSrc.Invert,       "Отметка низа\\Pили лотка трубы, м",      3, true),
            D(SheetRowKind.Length,  default,                "Длина, м",                               2, true),
            D(SheetRowKind.Diam,    default,                "%%c трубопровода",                        2, true),
            D(SheetRowKind.Manhole, default,                "Номер колодца, точки,\\Pуглы поворота",   2, true),
        };

        static List<TableRowDef> DefaultsL4() => new List<TableRowDef>
        {
            D(SheetRowKind.Height,  HeightSrc.DeltaZash,    "Толщина защитного слоя, м",              2, true),
            D(SheetRowKind.Height,  HeightSrc.ZashTop,      "Отметка верха\\Pзащитного слоя, м",      2, true, true, 0.05),
            D(SheetRowKind.Height,  HeightSrc.SandTop,      "Отметка верха\\Pпостели из песка, м",    2, true),
            D(SheetRowKind.Length,  default,                "Длина, м",                               2, true),
            D(SheetRowKind.Diam,    default,                "%%c трубопровода",                        2, true),
            D(SheetRowKind.Manhole, default,                "Номер колодца, точки,\\Pуглы поворота",   2, true),
        };

        static List<TableRowDef> DefaultsL5() => new List<TableRowDef>
        {
            D(SheetRowKind.Height,  HeightSrc.Surface,      "Отметка обратной\\Pзасыпки, м",          2, true, true, 0.05),
            D(SheetRowKind.Height,  HeightSrc.ZashTop,      "Отметка верха\\Pзащитного слоя, м",      2, true),
            D(SheetRowKind.Length,  default,                "Длина, м",                               2, true),
            D(SheetRowKind.Diam,    default,                "%%c трубопровода",                        2, true),
            D(SheetRowKind.Manhole, default,                "Номер колодца, точки,\\Pуглы поворота",   2, true),
        };

        static TableRowDef D(SheetRowKind type, HeightSrc src, string label, int roundTo, bool show)
            => D(type, src, label, roundTo, show, false, 0.0);

        static TableRowDef D(SheetRowKind type, HeightSrc src, string label, int roundTo, bool show, bool scatter, double amp)
            => new TableRowDef
            {
                Type = type, Src = src, Label = label, RoundTo = roundTo, Show = show,
                UseScatter = scatter, ScatterAmp = amp, AmpInRecord = true
            };

        // ── Внутренние утилиты ────────────────────────────────────────────────

        static List<string> SplitBy(string s, char delim)
        {
            var result = new List<string>();
            var cur    = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length) { cur.Append(c); cur.Append(s[++i]); }
                else if (c == delim)                { result.Add(cur.ToString()); cur.Clear(); }
                else                                { cur.Append(c); }
            }
            result.Add(cur.ToString());
            return result;
        }

        static string Unescape(string s)
            => s.Replace("\\;", ";").Replace("\\|", "|").Replace("\\\\", "\\");
    }
}
