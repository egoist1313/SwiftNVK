using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nvk.Core;

namespace Nvk.Cad
{
    /// <summary>Профиль настроек НВК между сеансами AutoCAD. Хранится в %APPDATA%\NvkPoperechnik\settings.txt.</summary>
    static class NvkProfile
    {
        static string FilePath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NvkPoperechnik", "settings.txt");

        /// <summary>Сохранить все настройки NvkUiState в файл профиля.</summary>
        public static void Save()
        {
            try
            {
                string p = FilePath;
                string dir = System.IO.Path.GetDirectoryName(p);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var map = NvkUiState.Serialize();
                var lines = new List<string>(map.Count);
                foreach (var kv in map)
                    lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(p, lines, Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>
        /// Загрузить настройки из профиля в NvkUiState.
        /// Возвращает true если файл найден и успешно прочитан.
        /// </summary>
        public static bool Load()
        {
            try
            {
                string p = FilePath;
                if (!File.Exists(p)) return false;
                string[] lines = File.ReadAllLines(p, Encoding.UTF8);
                var map = NodPairs.Parse(lines);
                if (map.Count == 0) return false;
                NvkUiState.FromMap(map);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Путь к файлу профиля (для информационных целей).</summary>
        public static string Path => FilePath;
    }
}
