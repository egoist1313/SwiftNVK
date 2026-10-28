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
        /// <summary>Полка выноски отбора сразу за бровкой дна котлована, м.</summary>
        public const double SampShelfOut = 0.5;

        public static List<NamedXy> Manholes(Chain chain, bool stripParen = false)
        {
            var rows = new List<NamedXy>();
            if (chain == null || chain.Segs == null || chain.Segs.Count == 0)
                return rows;
            var seen = new HashSet<string>();
            foreach (var s in chain.Segs)
                AddMh(rows, seen, SiteTitle.Label(s.StartName, stripParen), s.X0, s.Y0, s.StartR);
            var last = chain.Segs[chain.Segs.Count - 1];
            AddMh(rows, seen, SiteTitle.Label(last.EndName, stripParen), last.X1, last.Y1, last.EndR);
            return rows;
        }

        static void AddMh(List<NamedXy> rows, HashSet<string> seen,
            string name, double x, double y, double r)
        {
            string key = x.ToString("0.00") + "," + y.ToString("0.00");
            if (seen.Contains(key)) return;
            seen.Add(key);
            bool named = !string.IsNullOrEmpty(name);
            if (!named && r < 0.05) return;
            rows.Add(new NamedXy
            {
                Name = named ? name : "—",
                X = x,
                Y = y
            });
        }

        /// <summary>Точки отбора: те же правила, что выноски на плане. nextNum — следующий номер «Тn».</summary>
        public static List<NamedXy> Samples(Chain chain, SectionConfig cfg,
            IList<double> targets, IList<double> latOffs, int nextNum)
        {
            var rows = Collect(chain, cfg, targets, latOffs, nextNum);
            if (rows.Count > 0 || chain == null) return rows;
            if (cfg != null && !cfg.PlanEn) return rows;
            var fb = FallbackTargets(chain);
            if (fb.Count == 0) return rows;
            return Collect(chain, cfg, fb, null, nextNum);
        }

        /// <summary>
        /// Середины труб длиннее 10 м, дальше 5 м от колодца.
        /// Когда зональный шаг (50 м) попадает в колодец, Обновить стирает
        /// выноски и без этого запасного плана они не возвращаются.
        /// </summary>
        public static List<double> FallbackTargets(Chain chain)
        {
            var t = new List<double>();
            if (chain == null || chain.Segs == null) return t;
            double acc = 0.0;
            foreach (var s in chain.Segs)
            {
                double len = s.Length2d;
                double mid = acc + len * 0.5;
                acc += len;
                if (len <= 10.0) continue;
                if (NearManhole(chain, mid, 5.0)) continue;
                t.Add(mid);
            }
            return t;
        }

        public static int CountFromTargets(Chain chain, SectionConfig cfg,
            IList<double> targets, IList<double> latOffs, int nextNum)
        {
            return Collect(chain, cfg, targets, latOffs, nextNum).Count;
        }

        static List<NamedXy> Collect(Chain chain, SectionConfig cfg,
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
                double side = lat >= 0.0 ? 1.0 : -1.0;
                if (Math.Abs(lat) > hw) lat = side * hw;
                double ax = hit.X + hit.Nx * lat;
                double ay = hit.Y + hit.Ny * lat;
                double shelf = hw + SampShelfOut;
                rows.Add(new NamedXy
                {
                    Name = "Т" + nextNum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    X = ax,
                    Y = ay,
                    Tx = hit.X + hit.Nx * side * shelf,
                    Ty = hit.Y + hit.Ny * side * shelf
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
