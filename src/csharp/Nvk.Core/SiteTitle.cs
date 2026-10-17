using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Имя участка по колодцам трассы: «3-4-5-6».</summary>
    public static class SiteTitle
    {
        public static string FromChain(Chain chain)
        {
            var names = new List<string>();
            Append(names, chain);
            return Join(names);
        }

        public static string FromChains(IList<Chain> chains)
        {
            var names = new List<string>();
            if (chains != null)
            {
                for (int i = 0; i < chains.Count; i++)
                    Append(names, chains[i]);
            }
            return Join(names);
        }

        static void Append(List<string> names, Chain chain)
        {
            if (names == null || chain == null || chain.Segs == null || chain.Segs.Count == 0)
                return;
            Add(names, chain.Segs[0].StartName);
            for (int i = 0; i < chain.Segs.Count; i++)
                Add(names, chain.Segs[i].EndName);
        }

        static void Add(List<string> names, string raw)
        {
            string t = Token(raw);
            if (t.Length == 0) return;
            if (names.Count > 0
                && string.Equals(names[names.Count - 1], t, StringComparison.OrdinalIgnoreCase))
                return;
            names.Add(t);
        }

        static string Join(List<string> names)
        {
            if (names == null || names.Count == 0) return "";
            return string.Join("-", names.ToArray());
        }

        /// <summary>
        /// Номер колодца: «КК-3», «К3», «Колодец 3а» → «3» / «3а».
        /// Без цифр оставляем имя как есть.
        /// </summary>
        public static string Token(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            int i = s.Length - 1;
            while (i >= 0 && char.IsLetter(s[i])) i--;
            int last = i;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            if (last > i)
                return s.Substring(i + 1).Trim();
            return s;
        }
    }
}
