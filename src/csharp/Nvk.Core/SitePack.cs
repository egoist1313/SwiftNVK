using System;
using System.Collections.Generic;
using System.Text;

namespace Nvk.Core
{
    /// <summary>Один участок трассы: id, имя, ручки труб.</summary>
    public sealed class SiteRec
    {
        public string Id;
        public string Name;
        public string Pipes;

        public SiteRec() { }

        public SiteRec(string id, string name, string pipes)
        {
            Id = id ?? "";
            Name = name ?? "";
            Pipes = pipes ?? "";
        }
    }

    /// <summary>
    /// Список участков в одну строку NOD: id::имя::ручки||id2::…
    /// </summary>
    public static class SitePack
    {
        const string SiteSep = "||";
        const string FieldSep = "::";

        public static string Join(IList<SiteRec> list)
        {
            if (list == null || list.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(SiteSep);
                var s = list[i];
                sb.Append(Safe(s != null ? s.Id : ""));
                sb.Append(FieldSep);
                sb.Append(Safe(s != null ? s.Name : ""));
                sb.Append(FieldSep);
                sb.Append(Safe(s != null ? s.Pipes : ""));
            }
            return sb.ToString();
        }

        public static List<SiteRec> Split(string raw)
        {
            var r = new List<SiteRec>();
            if (string.IsNullOrWhiteSpace(raw)) return r;
            int i = 0;
            while (i < raw.Length)
            {
                int next = IndexOf(raw, SiteSep, i);
                string part = next < 0 ? raw.Substring(i) : raw.Substring(i, next - i);
                i = next < 0 ? raw.Length : next + SiteSep.Length;
                if (string.IsNullOrWhiteSpace(part)) continue;
                var rec = ParseOne(part);
                if (rec != null) r.Add(rec);
            }
            return r;
        }

        static SiteRec ParseOne(string part)
        {
            string id = "", name = "", pipes = "";
            int a = IndexOf(part, FieldSep, 0);
            if (a < 0)
            {
                id = part.Trim();
            }
            else
            {
                id = part.Substring(0, a).Trim();
                int b = IndexOf(part, FieldSep, a + FieldSep.Length);
                if (b < 0)
                    name = part.Substring(a + FieldSep.Length).Trim();
                else
                {
                    name = part.Substring(a + FieldSep.Length, b - a - FieldSep.Length).Trim();
                    pipes = part.Substring(b + FieldSep.Length).Trim();
                }
            }
            if (id.Length == 0) return null;
            if (name.Length == 0) name = id;
            return new SiteRec(id, name, pipes);
        }

        static int IndexOf(string s, string tok, int start)
        {
            if (start < 0 || start >= s.Length) return -1;
            return s.IndexOf(tok, start, StringComparison.Ordinal);
        }

        static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(FieldSep, " ").Replace(SiteSep, " ").Trim();
        }

        /// <summary>
        /// Берётся самый длинный из: SITES STATE, записи S_*, список в памяти
        /// (один «Участок 1» в памяти не считается). Пустой packed не затирает.
        /// </summary>
        public static List<SiteRec> Prefer(string packed, IList<SiteRec> fromKeys, IList<SiteRec> current)
        {
            var recs = Split(packed);
            var keys = Copy(fromKeys);
            var mem = Copy(current);
            if (mem.Count <= 1) mem = new List<SiteRec>();
            var best = recs;
            if (keys.Count > best.Count) best = keys;
            if (mem.Count > best.Count) best = mem;
            return best;
        }

        static List<SiteRec> Copy(IList<SiteRec> src)
        {
            var r = new List<SiteRec>();
            if (src == null) return r;
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null) r.Add(src[i]);
            return r;
        }

        public static bool Same(IList<SiteRec> a, IList<SiteRec> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i];
                var y = b[i];
                if (x == null && y == null) continue;
                if (x == null || y == null) return false;
                if (!string.Equals(x.Id ?? "", y.Id ?? "", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.Equals(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal))
                    return false;
                if (!string.Equals(x.Pipes ?? "", y.Pipes ?? "", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        public static string NextId(IList<SiteRec> list)
        {
            int max = 0;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string id = list[i] != null ? list[i].Id : null;
                    int n = ParseU(id);
                    if (n > max) max = n;
                }
            }
            return "U" + (max + 1).ToString();
        }

        public static string NextName(IList<SiteRec> list)
        {
            int n = (list == null ? 0 : list.Count) + 1;
            string name = "Участок " + n.ToString();
            if (list == null) return name;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                string nm = list[i] != null ? list[i].Name : null;
                if (!string.IsNullOrEmpty(nm)) used.Add(nm);
            }
            while (used.Contains(name))
            {
                n++;
                name = "Участок " + n.ToString();
            }
            return name;
        }

        static int ParseU(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2) return 0;
            if (id[0] != 'U' && id[0] != 'u') return 0;
            int n;
            if (!int.TryParse(id.Substring(1), out n) || n < 0) return 0;
            return n;
        }
    }
}
