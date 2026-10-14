using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nvk.Core
{
    public sealed class RoadZone
    {
        public string Type { get; set; }
        public double Ch0 { get; set; }
        public double Ch1 { get; set; }
    }

    /// <summary>
    /// Зоны ASPH/GAZN/TROT как pb-parse-zones-str / pb-fix-zone-overlaps.
    /// Строка: "А:0-2.088 Г:2.088-17 Т:17-20" (латиница A/G/T тоже).
    /// </summary>
    public static class RoadZones
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Участки в строке зон разделены «|»: «А:0-10 Г:10-20 | А:0-8».
        /// Без разделителя строка относится только к первому участку.
        /// </summary>
        public static string Slice(string s, int runIndex)
        {
            if (string.IsNullOrWhiteSpace(s) || runIndex < 0) return "";
            var parts = SplitRuns(s);
            if (parts.Count == 0) return "";
            if (parts.Count == 1)
                return runIndex == 0 ? parts[0] : "";
            return runIndex < parts.Count ? parts[runIndex] : "";
        }

        public static List<string> SplitRuns(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var p in s.Split('|'))
                list.Add(p.Trim());
            return list;
        }

        public static string JoinRuns(IList<string> parts)
        {
            if (parts == null || parts.Count == 0) return "";
            bool any = false;
            foreach (var p in parts)
                if (!string.IsNullOrWhiteSpace(p)) { any = true; break; }
            if (!any) return "";
            if (parts.Count == 1) return parts[0] ?? "";
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(parts[i] ?? "");
            }
            return sb.ToString();
        }

        public static List<RoadZone> Parse(string s, double totalLen)
        {
            return Parse(s, totalLen, 0);
        }

        public static List<RoadZone> Parse(string s, double totalLen, int runIndex)
        {
            var raw = new List<RoadZone>();
            string slice = Slice(s, runIndex);
            if (string.IsNullOrWhiteSpace(slice))
                return FixOverlaps(raw, totalLen);
            foreach (var tok0 in slice.Trim().Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tok = tok0.Trim();
                int colon = tok.IndexOf(':');
                if (colon <= 0 || colon >= tok.Length - 1) continue;
                string tag = tok.Substring(0, colon);
                string rest = tok.Substring(colon + 1);
                string type = TypeFromTag(tag);
                if (type == null) continue;
                int dash = rest.IndexOf('-');
                if (dash <= 0) continue;
                double ch1, ch2;
                if (!TryNum(rest.Substring(0, dash), out ch1)) continue;
                if (!TryNum(rest.Substring(dash + 1), out ch2)) continue;
                if (ch2 > ch1)
                    raw.Add(new RoadZone { Type = type, Ch0 = ch1, Ch1 = ch2 });
            }
            return FixOverlaps(raw, totalLen);
        }

        public static List<RoadZone> FixOverlaps(List<RoadZone> zones, double totalLen)
        {
            if (zones == null || zones.Count == 0)
                return new List<RoadZone>();
            var sorted = new List<RoadZone>(zones);
            sorted.Sort((a, b) => a.Ch0.CompareTo(b.Ch0));
            var result = new List<RoadZone>();
            double prevEnd = 0.0;
            double cap = totalLen > 0.0 ? totalLen : 1e18;
            foreach (var z in sorted)
            {
                double ch1 = Math.Max(z.Ch0, prevEnd);
                // Close tiny gaps (< 5 mm) between adjacent zones so no-pav spikes don't appear.
                if (result.Count > 0 && ch1 > prevEnd + 1e-6 && ch1 - prevEnd < 5e-3)
                    result[result.Count - 1].Ch1 = ch1;
                double ch2 = Math.Min(z.Ch1, cap);
                if (ch2 - ch1 > 1e-6)
                {
                    result.Add(new RoadZone { Type = z.Type, Ch0 = ch1, Ch1 = ch2 });
                    prevEnd = ch2;
                }
            }
            return result;
        }

        public static string Format(IList<RoadZone> zones)
        {
            if (zones == null || zones.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var z in zones)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(TagFromType(z.Type));
                sb.Append(':');
                sb.Append(z.Ch0.ToString("0.000", Inv));
                sb.Append('-');
                sb.Append(z.Ch1.ToString("0.000", Inv));
            }
            return sb.ToString();
        }

        public static string TypeAt(double ch, IList<RoadZone> zones)
        {
            if (zones == null) return null;
            string best = null;
            double bestS = -1e18;
            foreach (var z in zones)
            {
                if (ch >= z.Ch0 - 1e-9 && ch <= z.Ch1 + 1e-9 && z.Ch0 >= bestS)
                {
                    bestS = z.Ch0;
                    best = z.Type;
                }
            }
            return best;
        }

        /// <summary>Конец текущей зоны или начало следующей, не дальше until.</summary>
        public static double EndAt(double ch, IList<RoadZone> zones, double until)
        {
            if (until < ch) until = ch;
            if (zones == null || zones.Count == 0) return until;
            foreach (var z in zones)
            {
                if (ch >= z.Ch0 - 1e-9 && ch < z.Ch1 - 1e-9)
                    return z.Ch1 < until ? z.Ch1 : until;
            }
            double next = until;
            foreach (var z in zones)
            {
                if (z.Ch0 > ch + 1e-9 && z.Ch0 < next)
                    next = z.Ch0;
            }
            return next;
        }

        public static double ThkAt(double ch, IList<RoadZone> zones, SectionConfig cfg)
        {
            string t = TypeAt(ch, zones);
            if (t == null || cfg == null) return 0.0;
            if (t == "ASPH") return cfg.ThkAsph;
            if (t == "GAZN") return cfg.ThkGazn;
            if (t == "TROT") return cfg.ThkTrot;
            if (t == "OTHER") return cfg.ThkOther;
            return 0.0;
        }

        public static string TypeFromTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            tag = tag.Trim();
            if (tag == "А" || tag == "A" || tag == "a" || string.Equals(tag, "ASPH", StringComparison.OrdinalIgnoreCase))
                return "ASPH";
            if (tag == "Г" || tag == "G" || tag == "g" || string.Equals(tag, "GAZN", StringComparison.OrdinalIgnoreCase))
                return "GAZN";
            if (tag == "Т" || tag == "T" || tag == "t" || string.Equals(tag, "TROT", StringComparison.OrdinalIgnoreCase))
                return "TROT";
            if (tag == "П" || tag == "P" || tag == "p" || tag == "O"
                || string.Equals(tag, "OTHER", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "ПРОЧЕЕ", StringComparison.OrdinalIgnoreCase))
                return "OTHER";
            return null;
        }

        public static string TagFromType(string type)
        {
            if (type == "GAZN") return "Г";
            if (type == "TROT") return "Т";
            if (type == "OTHER") return "П";
            return "А";
        }

        static bool TryNum(string s, out double v)
        {
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out v);
        }
    }
}
