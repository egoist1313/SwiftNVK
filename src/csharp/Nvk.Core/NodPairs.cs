using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Разбор NOD XRECORD: строки KEY=value, куски KEY#2, KEY#3 (лимит DXF 1 = 255).
    /// </summary>
    public static class NodPairs
    {
        public static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (lines == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                raw[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
            {
                if (IsChunkKey(kv.Key)) continue;
                result[kv.Key] = JoinChunks(raw, kv.Key);
            }
            return result;
        }

        public static string Get(Dictionary<string, string> map, string key, string def)
        {
            if (map == null || string.IsNullOrEmpty(key)) return def;
            string v;
            if (map.TryGetValue(key, out v) && v != null) return v;
            return def;
        }

        static bool IsChunkKey(string k)
        {
            int hash = k.LastIndexOf('#');
            if (hash <= 0 || hash >= k.Length - 1) return false;
            int n;
            return int.TryParse(k.Substring(hash + 1), out n) && n >= 2;
        }

        static string JoinChunks(Dictionary<string, string> raw, string key)
        {
            string v;
            if (!raw.TryGetValue(key, out v) || v == null) v = "";
            for (int n = 2; ; n++)
            {
                string extra;
                if (!raw.TryGetValue(key + "#" + n, out extra) || extra == null) break;
                v += extra;
            }
            return v;
        }
    }
}
