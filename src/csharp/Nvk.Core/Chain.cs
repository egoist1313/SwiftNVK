using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Отсортированная цепочка труб. Алгоритм как pipebuild-sort-pipes:
    /// голова — труба, чьё начало не совпадает ни с чьим концом;
    /// дальше жадно клеим по XY (tol=0.15), при необходимости переворачиваем.
    /// </summary>
    public sealed class Chain
    {
        public const double DefaultTol = 0.15;

        public IReadOnlyList<PipeSeg> Segs { get; }
        public double Total { get; }

        public Chain(IReadOnlyList<PipeSeg> segs)
        {
            if (segs == null || segs.Count == 0)
                throw new ArgumentException("пустая цепочка", nameof(segs));
            Segs = segs;
            double t = 0.0;
            for (int i = 0; i < segs.Count; i++)
                t += segs[i].Length2d;
            Total = t;
        }

        /// <summary>Связные участки: не склеивать трубы, которые не стыкуются.</summary>
        public static List<Chain> Components(IList<PipeSeg> raw, double tol = DefaultTol)
        {
            var list = new List<Chain>();
            if (raw == null || raw.Count == 0) return list;
            var remaining = new List<PipeSeg>(raw);
            while (remaining.Count > 0)
            {
                var one = TakeConnected(remaining, tol);
                if (one.Count == 0) break;
                list.Add(new Chain(one));
            }
            return list;
        }

        public static Chain Sort(IList<PipeSeg> raw, double tol = DefaultTol)
        {
            var all = Components(raw, tol);
            if (all.Count == 0)
                throw new ArgumentException("нет труб", nameof(raw));
            return all[0];
        }

        static List<PipeSeg> TakeConnected(List<PipeSeg> remaining, double tol)
        {
            if (remaining.Count == 0) return new List<PipeSeg>();
            PipeSeg first = null;
            foreach (var pd in remaining)
            {
                if (!IsSomeonesEnd(pd.X0, pd.Y0, remaining, tol))
                {
                    first = pd;
                    break;
                }
            }
            if (first == null) first = remaining[0];

            var sorted = new List<PipeSeg> { first };
            remaining.Remove(first);
            int iter = 0;
            int cap = remaining.Count * 2 + 4;
            while (remaining.Count > 0 && iter < cap)
            {
                var last = sorted[sorted.Count - 1];
                PipeSeg found = null;
                bool rev = false;
                foreach (var pd in remaining)
                {
                    if (pd.DistStartTo(last.X1, last.Y1) < tol)
                    {
                        found = pd;
                        break;
                    }
                }
                if (found == null)
                {
                    foreach (var pd in remaining)
                    {
                        if (pd.DistEndTo(last.X1, last.Y1) < tol)
                        {
                            found = pd;
                            rev = true;
                            break;
                        }
                    }
                }
                if (found == null)
                    break;
                sorted.Add(rev ? found.Reversed() : found);
                remaining.Remove(found);
                iter++;
            }
            return sorted;
        }

        static bool IsSomeonesEnd(double x, double y, IList<PipeSeg> list, double tol)
        {
            foreach (var pd in list)
            {
                if (pd.DistEndTo(x, y) < tol)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Пикет в шахте колодца, включая центр. Внутренняя стенка (ch+R / end−R) — уже траншея.
        /// </summary>
        public bool InManhole(double station)
        {
            double ch = 0.0;
            foreach (var s in Segs)
            {
                double len = s.Length2d;
                double end = ch + len;
                double rs = s.ManholeR(true);
                if (rs > 1e-9 && station >= ch - 1e-6 && station < ch + rs - 1e-6)
                    return true;
                double re = s.ManholeR(false);
                if (re > 1e-9 && station > end - re + 1e-6 && station <= end + 1e-6)
                    return true;
                ch = end;
            }
            return false;
        }

        /// <summary>Радиус колодца в пикете. interior — шахта без внутренней стенки.</summary>
        public double ManholeRAt(double station, bool interior)
        {
            double ch = 0.0;
            foreach (var s in Segs)
            {
                double len = s.Length2d;
                double end = ch + len;
                double rs = s.ManholeR(true);
                if (rs > 1e-9)
                {
                    bool hit = interior
                        ? (station >= ch - 1e-6 && station < ch + rs - 1e-6)
                        : (station >= ch - 1e-6 && station <= ch + rs + 1e-6);
                    if (hit) return rs;
                }
                double re = s.ManholeR(false);
                if (re > 1e-9)
                {
                    bool hit = interior
                        ? (station > end - re + 1e-6 && station <= end + 1e-6)
                        : (station >= end - re - 1e-6 && station <= end + 1e-6);
                    if (hit) return re;
                }
                ch = end;
            }
            return 0.0;
        }

        /// <summary>Пикетаж ближайшей точки оси к плану (x,y), как pb-project-pt-to-chain.</summary>
        public double Project(double x, double y)
        {
            double dist;
            return Project(x, y, out dist);
        }

        public double Project(double x, double y, out double dist)
        {
            double bestCh = 0.0, bestD = 1e99, ch = 0.0;
            foreach (var s in Segs)
            {
                double dx = s.X1 - s.X0, dy = s.Y1 - s.Y0;
                double len = s.Length2d;
                double t = 0.0;
                if (len > 1e-9)
                    t = ((x - s.X0) * dx + (y - s.Y0) * dy) / (len * len);
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;
                double px = s.X0 + t * dx, py = s.Y0 + t * dy;
                double d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < bestD)
                {
                    bestD = d;
                    bestCh = ch + t * len;
                }
                ch += len;
            }
            dist = bestD < 1e98 ? bestD : 0.0;
            return bestCh;
        }

        /// <summary>Индекс трубы и параметр t∈[0,1] для пикетажа s.</summary>
        public StationHit Locate(double s)
        {
            double ch = 0.0;
            int n = Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var pd = Segs[i];
                double len = pd.Length2d;
                if (s <= ch + len + 1e-9)
                {
                    double t = len > 1e-6 ? (s - ch) / len : 0.0;
                    if (t < 0.0) t = 0.0;
                    if (t > 1.0) t = 1.0;
                    return HitFrom(i, t, ch + t * len);
                }
                ch += len;
            }
            return HitFrom(n - 1, 1.0, Total);
        }

        StationHit HitFrom(int i, double t, double station)
        {
            var pd = Segs[i];
            double x = pd.X0 + t * (pd.X1 - pd.X0);
            double y = pd.Y0 + t * (pd.Y1 - pd.Y0);
            double inv = pd.InvertStart + t * (pd.InvertEnd - pd.InvertStart);
            double dx = pd.X1 - pd.X0;
            double dy = pd.Y1 - pd.Y0;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double nx, ny;
            if (len < 1e-6) { nx = 0.0; ny = 1.0; }
            else { nx = -dy / len; ny = dx / len; }
            double slope = len > 1e-9 ? (pd.InvertEnd - pd.InvertStart) / len : 0.0;
            return new StationHit
            {
                Index = i,
                T = t,
                Station = station,
                X = x,
                Y = y,
                Invert = inv,
                Diam = pd.Diam,
                Nx = nx,
                Ny = ny,
                Slope = slope,
                Handle = pd.Handle
            };
        }
    }

    public sealed class StationHit
    {
        public int Index { get; set; }
        public double T { get; set; }
        public double Station { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Invert { get; set; }
        public double Diam { get; set; }
        public double Nx { get; set; }
        public double Ny { get; set; }
        public double Slope { get; set; }
        public string Handle { get; set; }
    }
}
