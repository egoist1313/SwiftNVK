using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nvk.Core
{
    public sealed class LeaderPose
    {
        public string Key;
        public double X;
        public double Y;
        public double Rot;
        public double Ax;
        public double Ay;
        public bool HasArrow;
    }

    /// <summary>
    /// Положение текста и острия выноски плана. NOD: «ключ|tx|ty|rot|ax|ay».
    /// </summary>
    public static class LeaderPoses
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(IDictionary<string, LeaderPose> map)
        {
            if (map == null || map.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                if (kv.Value == null || string.IsNullOrEmpty(kv.Key)) continue;
                if (sb.Length > 0) sb.Append('\n');
                var p = kv.Value;
                sb.Append(Esc(kv.Key)).Append('|')
                    .Append(p.X.ToString("G17", Inv)).Append('|')
                    .Append(p.Y.ToString("G17", Inv)).Append('|')
                    .Append(p.Rot.ToString("G17", Inv));
                if (p.HasArrow)
                    sb.Append('|').Append(p.Ax.ToString("G17", Inv))
                        .Append('|').Append(p.Ay.ToString("G17", Inv));
            }
            return sb.ToString();
        }

        public static Dictionary<string, LeaderPose> Parse(string s)
        {
            var map = new Dictionary<string, LeaderPose>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(s)) return map;
            foreach (var line0 in s.Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = line0.Trim();
                if (line.Length == 0) continue;
                var p = line.Split('|');
                if (p.Length < 4) continue;
                double x, y, rot;
                if (!TryNum(p[1], out x) || !TryNum(p[2], out y) || !TryNum(p[3], out rot))
                    continue;
                string key = Unesc(p[0]).Trim();
                if (key.Length == 0) continue;
                var pose = new LeaderPose { Key = key, X = x, Y = y, Rot = rot };
                double ax, ay;
                if (p.Length >= 6 && TryNum(p[4], out ax) && TryNum(p[5], out ay))
                {
                    pose.Ax = ax;
                    pose.Ay = ay;
                    pose.HasArrow = true;
                }
                map[key] = pose;
            }
            return map;
        }

        public static void Overlay(Dictionary<string, LeaderPose> dst, Dictionary<string, LeaderPose> src)
        {
            if (dst == null || src == null) return;
            foreach (var kv in src)
                dst[kv.Key] = kv.Value;
        }

        static string Esc(string k)
        {
            return (k ?? "").Replace('|', '/').Replace('\n', ' ');
        }

        static string Unesc(string k)
        {
            return k ?? "";
        }

        static bool TryNum(string s, out double v)
        {
            return double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, Inv, out v);
        }
    }
}