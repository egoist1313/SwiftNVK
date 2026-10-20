using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nvk.Core;

namespace Nvk.Cad
{
    /// <summary>Полные профили настроек L1–L5. Хранятся в RootDir/presets/full/*.txt.</summary>
    static class FullPresetStore
    {
        static bool SkipKey(string key)
        {
            return NodPairs.DrawingBound.Contains(key ?? "");
        }

        public static string Dir()
        {
            string root = NvkSession.RootDir;
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nvk");
            string d = Path.Combine(root, "presets", "full");
            Directory.CreateDirectory(d);
            return d;
        }

        static string PathOf(string name) => Path.Combine(Dir(), Sanitize(name) + ".txt");

        public static List<string> List()
        {
            var r = new List<string>();
            foreach (var f in Directory.GetFiles(Dir(), "*.txt"))
                r.Add(Path.GetFileNameWithoutExtension(f));
            r.Sort(StringComparer.OrdinalIgnoreCase);
            return r;
        }

        public static void Save(string name, Dictionary<string, string> map)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "default";
            var lines = new List<string>(map.Count);
            foreach (var kv in map)
            {
                if (SkipKey(kv.Key)) continue;
                lines.Add(kv.Key + "=" + kv.Value);
            }
            File.WriteAllLines(PathOf(name), lines, Encoding.UTF8);
        }

        /// <summary>Загружает карту ключей из профиля. Возвращает null если файл не найден.</summary>
        public static Dictionary<string, string> Load(string name)
        {
            string p = PathOf(name);
            if (!File.Exists(p)) return null;
            try
            {
                string[] lines = File.ReadAllLines(p, Encoding.UTF8);
                var map = NodPairs.Parse(lines);
                return map.Count > 0 ? map : null;
            }
            catch { return null; }
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
