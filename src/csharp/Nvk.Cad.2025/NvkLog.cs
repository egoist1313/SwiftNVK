using System;
using System.IO;
using System.Text;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Полный лог прогона в файл проекта: nvk-log.txt (не в командную строку).</summary>
    public static class NvkLog
    {
        public static string FilePath()
        {
            string dir = NvkSession.RootDir;
            if (string.IsNullOrEmpty(dir))
                dir = @"E:\Новая папка\Lich15\Поперечник НВК";
            try { Directory.CreateDirectory(dir); } catch (Exc) { }
            return Path.Combine(dir, "nvk-log.txt");
        }

        public static void BeginRun(int n, string ver)
        {
            try
            {
                var p = FilePath();
                var sb = new StringBuilder();
                sb.AppendLine("========== НВК прогон #" + n + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==========");
                sb.AppendLine("версия " + (string.IsNullOrEmpty(ver) ? "?" : ver));
                sb.AppendLine("файл " + p);
                File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
            }
            catch (Exc) { }
        }

        public static void Write(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            try
            {
                if (!msg.EndsWith("\n")) msg += "\n";
                File.AppendAllText(FilePath(), msg, Encoding.UTF8);
            }
            catch (Exc) { }
        }

        // Лог из Lisp — только open/write-line в nvk-orchestrator.lsp.
        // LispFunction + vl-catch-all-apply в Civil даёт «неверная функция» на unlock.
    }
}
