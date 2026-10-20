using System;
using System.Text;

namespace Nvk.Core
{
    /// <summary>
    /// Подпись «План М1:N» на бумаге — масштаб видового плана с панели.
    /// Ситуационный план не трогаем.
    /// </summary>
    public static class ScaleText
    {
        public static string SubstPlan(string text, int scaleN)
        {
            if (string.IsNullOrEmpty(text) || scaleN < 1) return text ?? "";
            if (!IsPlanScaleLabel(text)) return text;
            string n = scaleN.ToString();
            string t = ReplaceAfter(text, "М1:", n);
            t = ReplaceAfter(t, "M1:", n);
            t = ReplaceAfter(t, "М 1:", n);
            t = ReplaceAfter(t, "M 1:", n);
            return t;
        }

        public static bool IsPlanScaleLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string u = text.ToUpperInvariant();
            if (u.IndexOf("СИТУАЦИОН", StringComparison.Ordinal) >= 0)
                return false;
            if (u.IndexOf("ПЛАН", StringComparison.Ordinal) < 0)
                return false;
            return HasMark(u);
        }

        static bool HasMark(string u)
        {
            return u.IndexOf("М1:", StringComparison.Ordinal) >= 0
                || u.IndexOf("M1:", StringComparison.Ordinal) >= 0
                || u.IndexOf("М 1:", StringComparison.Ordinal) >= 0
                || u.IndexOf("M 1:", StringComparison.Ordinal) >= 0;
        }

        static string ReplaceAfter(string text, string mark, string n)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(mark)) return text;
            int i = 0;
            int hit = text.IndexOf(mark, i, StringComparison.Ordinal);
            if (hit < 0) return text;
            var sb = new StringBuilder();
            while (hit >= 0)
            {
                sb.Append(text, i, hit - i);
                sb.Append(mark);
                int j = hit + mark.Length;
                while (j < text.Length && text[j] >= '0' && text[j] <= '9') j++;
                sb.Append(n);
                i = j;
                hit = text.IndexOf(mark, i, StringComparison.Ordinal);
            }
            if (i < text.Length) sb.Append(text, i, text.Length - i);
            return sb.ToString();
        }
    }
}
