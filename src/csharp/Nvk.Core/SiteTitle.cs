using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Имя участка по колодцам трассы: «3-4-5-6».</summary>
    public static class SiteTitle
    {
        public static string FromChain(Chain chain, bool stripParen = false)
        {
            var names = new List<string>();
            Append(names, chain, stripParen);
            return Join(names);
        }

        public static string FromChains(IList<Chain> chains, bool stripParen = false)
        {
            var names = new List<string>();
            if (chains != null)
            {
                for (int i = 0; i < chains.Count; i++)
                    Append(names, chains[i], stripParen);
            }
            return Join(names);
        }

        static void Append(List<string> names, Chain chain, bool stripParen)
        {
            if (names == null || chain == null || chain.Segs == null || chain.Segs.Count == 0)
                return;
            Add(names, chain.Segs[0].StartName, stripParen);
            for (int i = 0; i < chain.Segs.Count; i++)
                Add(names, chain.Segs[i].EndName, stripParen);
        }

        static void Add(List<string> names, string raw, bool stripParen)
        {
            string t = Token(raw, stripParen);
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
        /// stripParen: только скобки, префикс остаётся — «ГП12(1)» → «ГП12».
        /// </summary>
        public static string Token(string raw, bool stripParen = false)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            if (stripParen) return StripParen(s);
            if (s.Length == 0) return "";
            int i = s.Length - 1;
            while (i >= 0 && char.IsLetter(s[i])) i--;
            int last = i;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            if (last > i)
                return s.Substring(i + 1).Trim();
            return s;
        }

        /// <summary>Подпись колодца на плане / в таблице: скобки снимаются по флагу.</summary>
        public static string Label(string raw, bool stripParen)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
            string s = raw.Trim();
            return stripParen ? StripParen(s) : s;
        }

        /// <summary>«12(1)», «12 (2)» → «12». Несколько пар скобок снимаются.</summary>
        public static string StripParen(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            string s = raw;
            for (int n = 0; n < 8; n++)
            {
                int open = s.IndexOf('(');
                if (open < 0) break;
                int close = s.IndexOf(')', open + 1);
                if (close < 0) break;
                string left = s.Substring(0, open).TrimEnd();
                string right = s.Substring(close + 1);
                s = (left + right).Trim();
            }
            return s;
        }

        /// <summary>
        /// Имя участка уже собрано: «12(1)-13 (2); 67-68» → «12-13; 67-68».
        /// Соседние одинаковые номера после снятия скобок схлопываются.
        /// </summary>
        public static string CleanTitle(string title, bool stripParen)
        {
            if (string.IsNullOrEmpty(title) || !stripParen) return title ?? "";
            var chunks = title.Split(';');
            var outSites = new List<string>();
            for (int i = 0; i < chunks.Length; i++)
            {
                string chunk = StripParen(chunks[i].Trim());
                if (chunk.Length == 0) continue;
                var parts = chunk.Split('-');
                var names = new List<string>();
                for (int j = 0; j < parts.Length; j++)
                {
                    string t = parts[j].Trim();
                    if (t.Length == 0) continue;
                    if (names.Count > 0
                        && string.Equals(names[names.Count - 1], t, StringComparison.OrdinalIgnoreCase))
                        continue;
                    names.Add(t);
                }
                if (names.Count > 0)
                    outSites.Add(string.Join("-", names.ToArray()));
            }
            if (outSites.Count == 0) return "";
            return string.Join("; ", outSites.ToArray());
        }
    }
}
