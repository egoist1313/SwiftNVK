using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class L1PresetStore
    {
        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static string Dir()
        {
            string root = NvkSession.RootDir;
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nvk");
            string d = Path.Combine(root, "presets", "l1");
            Directory.CreateDirectory(d);
            return d;
        }

        public static string PathOf(string name)
        {
            string n = Sanitize(name);
            return Path.Combine(Dir(), n + ".json");
        }

        public static List<string> List()
        {
            var r = new List<string>();
            foreach (var f in Directory.GetFiles(Dir(), "*.json"))
                r.Add(Path.GetFileNameWithoutExtension(f));
            r.Sort(StringComparer.OrdinalIgnoreCase);
            return r;
        }

        public static void Save(L1Style st)
        {
            if (st == null) st = L1Style.Default();
            if (string.IsNullOrWhiteSpace(st.Name)) st.Name = "default";
            File.WriteAllText(PathOf(st.Name), JsonSerializer.Serialize(st, Opt));
        }

        public static L1Style Load(string name)
        {
            string p = PathOf(name);
            if (!File.Exists(p)) return L1Style.Default();
            try
            {
                var st = JsonSerializer.Deserialize<L1Style>(File.ReadAllText(p), Opt);
                if (st == null) return L1Style.Default();
                if (st.Road == null) st.Road = LineStyle.Of(1, "Continuous", 30);
                if (st.Earth == null) st.Earth = LineStyle.Of(5, "Continuous", 30);
                if (st.Bottom == null) st.Bottom = LineStyle.Of(1, "Continuous", 30);
                if (st.HatchExcavation == null) st.HatchExcavation = FillStyle.Of("ANSI31", 8, 1);
                if (st.HatchPavement == null) st.HatchPavement = FillStyle.Of("ANSI31", 8, 1);
                return st;
            }
            catch (Exc)
            {
                return L1Style.Default();
            }
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "default";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
