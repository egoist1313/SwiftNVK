using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    public sealed class NamedXy
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Tx { get; set; }
        public double Ty { get; set; }
    }

    /// <summary>Координаты колодцев и точек отбора проб в плане.</summary>
    public static class PlanPoints
    {
        public static List<NamedXy> Manholes(Chain chain)
        {
            var rows = new List<NamedXy>();
            if (chain == null || chain.Segs == null || chain.Segs.Count == 0)
                return rows;
            var seen = new HashSet<string>();
            foreach (var s in chain.Segs)
                AddMh(rows, seen, s.StartName, s.X0, s.Y0, s.StartR);
            var last = chain.Segs[chain.Segs.Count - 1];
            AddMh(rows, seen, last.EndName, last.X1, last.Y1, last.EndR);
            return rows;
        }

        static void AddMh(List<NamedXy> rows, HashSet<string> seen,
            string name, double x, double y, double r)
        {
            string key = x.ToString("0.00") + "," + y.ToString("0.00");
            if (seen.Contains(key)) return;
            seen.Add(key);
            if (string.IsNullOrEmpty(name) && r < 0.05) return;
            rows.Add(new NamedXy
            {
                Name = string.IsNullOrEmpty(name) ? "—" : name,
                X = x,
                Y = y
            });
        }

        /// <summary>Точки отбора: те же правила, что выноски на плане. nextNum — следующий номер «Тn».</summary>
        public static List<NamedXy> Samples(Chain chain, SectionConfig cfg,
            IList<double> targets, IList<double> latOffs, int nextNum)
        {
            var rows = new List<NamedXy>();
            if (chain == null || targets == null) return rows;
            if (cfg != null && !cfg.PlanEn) return rows;
            if (nextNum < 1) nextNum = 1;
            double lastCh = -1e9;
            double min = cfg != null ? cfg.PlanMin : 0.0;
            for (int i = 0; i < targets.Count; i++)
            {
                double tgt = targets[i];
                if (tgt - lastCh < min) continue;
                if (NearManhole(chain, tgt, 5.0)) continue;
                lastCh = tgt;
                var hit = chain.Locate(tgt);
                double hwL = cfg != null ? cfg.BottomLeft(hit.Diam) : 0.5;
                double hwR = cfg != null ? cfg.BottomRight(hit.Diam) : hwL;
                double hw = Math.Max(hwL, hwR);
                double lat = latOffs != null && i < latOffs.Count
                    ? latOffs[i] : Scatter.PlanLat(hw, 1, i);
                hw = lat >= 0.0 ? hwR : hwL;
                double ax = hit.X + hit.Nx * lat;
                double ay = hit.Y + hit.Ny * lat;
                double side = lat >= 0.0 ? 1.0 : -1.0;
                rows.Add(new NamedXy
                {
                    Name = "Т" + nextNum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    X = ax,
                    Y = ay,
                    Tx = hit.X + hit.Nx * side * (hw + 5.0),
                    Ty = hit.Y + hit.Ny * side * (hw + 5.0)
                });
                nextNum++;
            }
            return rows;
        }

        public static bool NearManhole(Chain chain, double ch, double minDist)
        {
            if (chain == null || chain.Segs == null) return false;
            double acc = 0.0;
            foreach (var s in chain.Segs)
            {
                if (s.StartR > 0.05 && Math.Abs(ch - acc) < minDist) return true;
                acc += s.Length2d;
                if (s.EndR > 0.05 && Math.Abs(ch - acc) < minDist) return true;
            }
            return false;
        }
    }
}
