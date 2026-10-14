using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nvk.Core
{
    public sealed class PipeWallRow
    {
        public double OdMm;
        public double IdMm;
        public double WallMm;
    }

    /// <summary>
    /// Каталог стенок. Дефолт — Корсис SN8, стенка = (OD−ID)/2.
    /// Поиск по ближайшему OD или ID к диаметру Civil (обычно внутренний).
    /// </summary>
    public static class PipeWalls
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<PipeWallRow> KorsisSn8()
        {
            return new List<PipeWallRow>
            {
                Row(110, 91, 9.5),
                Row(160, 139, 10.5),
                Row(200, 176, 12.0),
                Row(250, 216, 17.0),
                Row(315, 271, 22.0),
                Row(400, 343, 28.5),
                Row(500, 427, 36.5),
                Row(630, 535, 47.5),
                Row(800, 678, 61.0),
                Row(1000, 851, 74.5),
                Row(1200, 1030, 85.0)
            };
        }

        static PipeWallRow Row(double od, double id, double w)
        {
            return new PipeWallRow { OdMm = od, IdMm = id, WallMm = w };
        }

        /// <summary>Толщина стенки в метрах. 0 если выкл. или таблица пуста.</summary>
        public static double MetersAt(double diamM, IList<PipeWallRow> table, bool enabled)
        {
            if (!enabled || diamM <= 0.0 || table == null || table.Count == 0)
                return 0.0;
            double mm = diamM * 1000.0;
            PipeWallRow best = null;
            double bestD = double.MaxValue;
            foreach (var r in table)
            {
                if (r == null || r.WallMm < 0.0) continue;
                double dOd = Math.Abs(r.OdMm - mm);
                double dId = Math.Abs(r.IdMm - mm);
                double d = dOd < dId ? dOd : dId;
                if (d < bestD)
                {
                    bestD = d;
                    best = r;
                }
            }
            return best == null ? 0.0 : best.WallMm / 1000.0;
        }

        public static string Format(IList<PipeWallRow> table)
        {
            if (table == null || table.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var r in table)
            {
                if (r == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(r.OdMm.ToString("0.#", Inv)).Append(',')
                    .Append(r.IdMm.ToString("0.#", Inv)).Append(',')
                    .Append(r.WallMm.ToString("0.#", Inv));
            }
            return sb.ToString();
        }

        public static List<PipeWallRow> Parse(string s)
        {
            var r = new List<PipeWallRow>();
            if (string.IsNullOrWhiteSpace(s)) return r;
            foreach (var tok in s.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = tok.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) continue;
                double od, id, w;
                if (!Try(p[0], out od) || !Try(p[1], out id) || !Try(p[2], out w)) continue;
                if (od <= 0 || w < 0) continue;
                r.Add(new PipeWallRow { OdMm = od, IdMm = id, WallMm = w });
            }
            return r;
        }

        static bool Try(string s, out double v)
        {
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out v);
        }
    }
}
