using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Замкнутый или разомкнутый контур после объединения.</summary>
    public sealed class PolyLoop
    {
        public List<WellPt> Pts = new List<WellPt>();
        public bool Closed;
    }

    /// <summary>
    /// Объединение прямоугольников траншей минус диск колодца.
    /// Общие рёбра (наложение) выкидываются — остаётся внешний контур и дырка колодца.
    /// </summary>
    public static class PolyUnion
    {
        const double Off = 0.005;
        const int WellN = 36;
        const double Quant = 1e7;

        /// <param name="dropClose">Выкинуть замыкающее ребро каждого входа (бровка).</param>
        public static List<PolyLoop> UnionMinusDisk(
            IList<List<WellPt>> rects, double cx, double cy, double R, double z,
            bool dropClose)
        {
            var loops = new List<PolyLoop>();
            if (rects == null || rects.Count == 0) return loops;
            var subjects = new List<List<WellPt>>();
            var drop = new List<WellPt[]>();
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                if (r == null || r.Count < 3) continue;
                subjects.Add(r);
                if (dropClose)
                    drop.Add(new[] { r[r.Count - 1], r[0] });
            }
            if (subjects.Count == 0) return loops;

            var well = WellPoly(cx, cy, R, z);
            var raw = new List<Raw>();
            for (int i = 0; i < subjects.Count; i++)
                AddClosed(raw, subjects[i]);
            if (well != null && well.Count >= 3)
                AddClosed(raw, well);

            var weld = new Weld();
            var cuts = new List<double>[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                cuts[i] = new List<double>();
                cuts[i].Add(0.0);
                cuts[i].Add(1.0);
            }
            for (int i = 0; i < raw.Count; i++)
            {
                for (int j = i + 1; j < raw.Count; j++)
                    CutPair(raw[i], raw[j], cuts[i], cuts[j]);
            }

            var frags = new List<Frag>();
            var seen = new HashSet<long>();
            for (int i = 0; i < raw.Count; i++)
            {
                var ts = UniqueT(cuts[i], raw[i].Len());
                for (int k = 0; k < ts.Count - 1; k++)
                {
                    double t0 = ts[k], t1 = ts[k + 1];
                    double ax, ay, bx, by;
                    raw[i].At(t0, out ax, out ay);
                    raw[i].At(t1, out bx, out by);
                    if (Dist(ax, ay, bx, by) < 1e-8) continue;
                    int a = weld.Add(ax, ay);
                    int b = weld.Add(bx, by);
                    if (a == b) continue;
                    int lo = a < b ? a : b;
                    int hi = a < b ? b : a;
                    long key = ((long)lo << 32) ^ (uint)hi;
                    if (!seen.Add(key)) continue;
                    frags.Add(new Frag { A = a, B = b });
                }
            }

            var keep = new List<Frag>();
            for (int i = 0; i < frags.Count; i++)
            {
                var f = frags[i];
                double ax = weld.X[f.A], ay = weld.Y[f.A];
                double bx = weld.X[f.B], by = weld.Y[f.B];
                double mx = 0.5 * (ax + bx), my = 0.5 * (ay + by);
                if (OnDrops(mx, my, drop)) continue;
                double dx = bx - ax, dy = by - ay;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-9) continue;
                double nx = -dy / len, ny = dx / len;
                bool inL = InUnion(mx + nx * Off, my + ny * Off, subjects, well);
                bool inR = InUnion(mx - nx * Off, my - ny * Off, subjects, well);
                if (inL == inR) continue;
                keep.Add(f);
            }

            return Chain(keep, weld, z);
        }

        static List<WellPt> WellPoly(double cx, double cy, double R, double z)
        {
            if (R < 1e-6) return null;
            var pts = new List<WellPt>(WellN);
            for (int i = 0; i < WellN; i++)
            {
                double a = 2.0 * Math.PI * i / WellN;
                pts.Add(new WellPt(cx + R * Math.Cos(a), cy + R * Math.Sin(a), z));
            }
            return pts;
        }

        static void AddClosed(List<Raw> raw, List<WellPt> poly)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                if (Dist(a.X, a.Y, b.X, b.Y) < 1e-9) continue;
                raw.Add(new Raw { Ax = a.X, Ay = a.Y, Bx = b.X, By = b.Y });
            }
        }

        static bool InUnion(double x, double y, List<List<WellPt>> subjects, List<WellPt> well)
        {
            if (well != null && WellPitGeom.PointInPoly(well, x, y)) return false;
            for (int i = 0; i < subjects.Count; i++)
                if (WellPitGeom.PointInPoly(subjects[i], x, y)) return true;
            return false;
        }

        static bool OnDrops(double x, double y, List<WellPt[]> drop)
        {
            if (drop == null) return false;
            for (int i = 0; i < drop.Count; i++)
            {
                var d = drop[i];
                if (OnSeg(x, y, d[0].X, d[0].Y, d[1].X, d[1].Y, 1e-3))
                    return true;
            }
            return false;
        }

        static bool OnSeg(double px, double py, double ax, double ay, double bx, double by, double eps)
        {
            double dx = bx - ax, dy = by - ay;
            double L = dx * dx + dy * dy;
            if (L < 1e-20) return Dist(px, py, ax, ay) < eps;
            double t = ((px - ax) * dx + (py - ay) * dy) / L;
            if (t < -1e-6 || t > 1.0 + 1e-6) return false;
            double qx = ax + t * dx, qy = ay + t * dy;
            return Dist(px, py, qx, qy) < eps;
        }

        static void CutPair(Raw a, Raw b, List<double> ta, List<double> tb)
        {
            double t, u;
            if (SegHit(a.Ax, a.Ay, a.Bx, a.By, b.Ax, b.Ay, b.Bx, b.By, out t, out u))
            {
                ta.Add(t);
                tb.Add(u);
            }
            Collinear(a, b, ta, tb);
        }

        static bool SegHit(double ax, double ay, double bx, double by,
            double cx, double cy, double dx, double dy, out double t, out double u)
        {
            t = 0.0;
            u = 0.0;
            double rx = bx - ax, ry = by - ay;
            double sx = dx - cx, sy = dy - cy;
            double den = rx * sy - ry * sx;
            double rl = Math.Sqrt(rx * rx + ry * ry);
            double sl = Math.Sqrt(sx * sx + sy * sy);
            if (Math.Abs(den) < 1e-12 * Math.Max(1.0, rl * sl)) return false;
            double qx = cx - ax, qy = cy - ay;
            t = (qx * sy - qy * sx) / den;
            u = (qx * ry - qy * rx) / den;
            return t >= -1e-9 && t <= 1.0 + 1e-9 && u >= -1e-9 && u <= 1.0 + 1e-9;
        }

        static void Collinear(Raw a, Raw b, List<double> ta, List<double> tb)
        {
            double rx = a.Bx - a.Ax, ry = a.By - a.Ay;
            double sx = b.Bx - b.Ax, sy = b.By - b.Ay;
            double cross = rx * sy - ry * sx;
            double al = a.Len(), bl = b.Len();
            if (al < 1e-12 || bl < 1e-12) return;
            if (Math.Abs(cross) > 1e-8 * al * bl) return;
            double dist = Math.Abs((b.Ax - a.Ax) * ry - (b.Ay - a.Ay) * rx) / al;
            if (dist > 1e-6) return;
            ProjectOn(a, b.Ax, b.Ay, ta);
            ProjectOn(a, b.Bx, b.By, ta);
            ProjectOn(b, a.Ax, a.Ay, tb);
            ProjectOn(b, a.Bx, a.By, tb);
        }

        static void ProjectOn(Raw s, double x, double y, List<double> ts)
        {
            double dx = s.Bx - s.Ax, dy = s.By - s.Ay;
            double L = dx * dx + dy * dy;
            if (L < 1e-20) return;
            double t = ((x - s.Ax) * dx + (y - s.Ay) * dy) / L;
            if (t >= -1e-9 && t <= 1.0 + 1e-9)
                ts.Add(t < 0 ? 0 : (t > 1 ? 1 : t));
        }

        static List<double> UniqueT(List<double> raw, double len)
        {
            raw.Sort();
            double merge = len > 1e-9 ? (1e-7 / len) : 1e-9;
            var o = new List<double>();
            for (int i = 0; i < raw.Count; i++)
            {
                double t = raw[i];
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;
                if (o.Count == 0 || t - o[o.Count - 1] > merge)
                    o.Add(t);
            }
            if (o.Count == 0 || o[0] > 0) o.Insert(0, 0.0);
            if (o[o.Count - 1] < 1.0) o.Add(1.0);
            return o;
        }

        static List<PolyLoop> Chain(List<Frag> frags, Weld weld, double z)
        {
            var loops = new List<PolyLoop>();
            if (frags.Count == 0) return loops;
            int nPt = weld.X.Count;
            var adj = new List<int>[nPt];
            for (int i = 0; i < nPt; i++) adj[i] = new List<int>();
            for (int i = 0; i < frags.Count; i++)
            {
                adj[frags[i].A].Add(i);
                adj[frags[i].B].Add(i);
            }
            var used = new bool[frags.Count];

            for (int p = 0; p < nPt; p++)
            {
                if (UnusedDeg(p, adj, used) != 1) continue;
                var loop = Walk(p, frags, adj, used, weld, z);
                if (loop != null && loop.Pts.Count >= 2) loops.Add(loop);
            }
            for (int i = 0; i < frags.Count; i++)
            {
                if (used[i]) continue;
                var loop = Walk(frags[i].A, frags, adj, used, weld, z);
                if (loop != null && loop.Pts.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        static int UnusedDeg(int p, List<int>[] adj, bool[] used)
        {
            int d = 0;
            var a = adj[p];
            for (int i = 0; i < a.Count; i++)
                if (!used[a[i]]) d++;
            return d;
        }

        static PolyLoop Walk(int start, List<Frag> frags, List<int>[] adj, bool[] used,
            Weld weld, double z)
        {
            var pts = new List<WellPt>();
            int cur = start;
            int guard = frags.Count + 4;
            pts.Add(new WellPt(weld.X[cur], weld.Y[cur], z));
            bool returned = false;
            while (guard-- > 0)
            {
                int fi = NextFrag(cur, adj, used);
                if (fi < 0) break;
                used[fi] = true;
                var f = frags[fi];
                cur = f.A == cur ? f.B : f.A;
                if (cur == start)
                {
                    returned = true;
                    break;
                }
                pts.Add(new WellPt(weld.X[cur], weld.Y[cur], z));
            }
            if (pts.Count < 2) return null;
            return new PolyLoop { Pts = pts, Closed = returned && pts.Count >= 3 };
        }

        static int NextFrag(int p, List<int>[] adj, bool[] used)
        {
            var a = adj[p];
            for (int i = 0; i < a.Count; i++)
                if (!used[a[i]]) return a[i];
            return -1;
        }

        static double Dist(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        sealed class Raw
        {
            public double Ax, Ay, Bx, By;
            public double Len()
            {
                double dx = Bx - Ax, dy = By - Ay;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            public void At(double t, out double x, out double y)
            {
                x = Ax + t * (Bx - Ax);
                y = Ay + t * (By - Ay);
            }
        }

        sealed class Frag
        {
            public int A, B;
        }

        sealed class Weld
        {
            public List<double> X = new List<double>();
            public List<double> Y = new List<double>();
            readonly Dictionary<long, int> _map = new Dictionary<long, int>();

            public int Add(double x, double y)
            {
                long kx = (long)Math.Round(x * Quant);
                long ky = (long)Math.Round(y * Quant);
                long key = (kx * 397) ^ ky;
                // collision-safe: linear probe via unique list check
                int id;
                if (_map.TryGetValue(key, out id))
                {
                    if (Math.Abs(X[id] - x) < 1e-6 && Math.Abs(Y[id] - y) < 1e-6)
                        return id;
                    for (int i = 0; i < X.Count; i++)
                        if (Math.Abs(X[i] - x) < 1e-6 && Math.Abs(Y[i] - y) < 1e-6)
                            return i;
                }
                id = X.Count;
                X.Add(x);
                Y.Add(y);
                _map[key] = id;
                return id;
            }
        }
    }
}
