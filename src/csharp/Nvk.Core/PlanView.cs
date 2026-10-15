using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Поворот плана и набор слоёв видового экрана.</summary>
    public static class PlanView
    {
        /// <summary>
        /// Габарит окна в системе, повёрнутой на deg градусов против часовой.
        /// Центр тот же, ширина/высота — осевой ящик повёрнутых углов.
        /// </summary>
        public static void RotatedSize(double x0, double y0, double x1, double y1,
            double deg, out double w, out double h)
        {
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }
            w = x1 - x0;
            h = y1 - y0;
            if (w < 0.0) w = 0.0;
            if (h < 0.0) h = 0.0;
            if (Math.Abs(deg) < 1e-9) return;
            double rad = deg * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            double cx = 0.5 * (x0 + x1), cy = 0.5 * (y0 + y1);
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            Corner(x0, y0); Corner(x1, y0); Corner(x1, y1); Corner(x0, y1);
            w = maxX - minX;
            h = maxY - minY;
            if (w < 0.0) w = 0.0;
            if (h < 0.0) h = 0.0;

            void Corner(double x, double y)
            {
                double dx = x - cx, dy = y - cy;
                double vx = dx * c + dy * s;
                double vy = -dx * s + dy * c;
                if (vx < minX) minX = vx;
                if (vy < minY) minY = vy;
                if (vx > maxX) maxX = vx;
                if (vy > maxY) maxY = vy;
            }
        }

        public static List<string> SplitLayers(string s)
        {
            var r = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return r;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in s.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                if (!seen.Add(n)) continue;
                r.Add(n);
            }
            return r;
        }

        public static string JoinLayers(IList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i] != null ? names[i].Trim() : "";
                if (n.Length == 0 || !seen.Add(n)) continue;
                parts.Add(n);
            }
            return string.Join(";", parts);
        }

        /// <summary>Пустой список — все слои включены. Слой 0 всегда включён.</summary>
        public static bool IsOn(string layer, IList<string> on)
        {
            if (string.IsNullOrEmpty(layer)) return false;
            if (layer == "0") return true;
            if (string.Equals(layer, "Defpoints", StringComparison.OrdinalIgnoreCase))
                return true;
            if (on == null || on.Count == 0) return true;
            for (int i = 0; i < on.Count; i++)
            {
                if (string.Equals(on[i], layer, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
