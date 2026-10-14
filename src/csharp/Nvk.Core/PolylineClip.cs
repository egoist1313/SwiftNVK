using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Клип полилинии снизу: не ниже опорной, вершина на каждом пересечении (pb-clip-pts-above).</summary>
    public static class PolylineClip
    {
        public struct Pt
        {
            public double X;
            public double Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        public static List<Pt> KeepAbove(IList<Pt> pts, IList<Pt> floor)
        {
            return Clip(pts, floor, true);
        }

        /// <summary>Не выше потолка, вершина на пересечении (L4 не выше L6).</summary>
        /// <param name="insertBound">false — только точки пересечения без промежуточных вершин bound.</param>
        public static List<Pt> KeepBelow(IList<Pt> pts, IList<Pt> ceil, bool insertBound = true)
        {
            return Clip(pts, ceil, false, insertBound);
        }

        static List<Pt> Clip(IList<Pt> pts, IList<Pt> bound, bool keepAbove, bool insertBound = true)
        {
            var result = new List<Pt>();
            if (pts == null || pts.Count < 2 || bound == null || bound.Count < 2)
            {
                if (pts != null)
                    for (int i = 0; i < pts.Count; i++) result.Add(pts[i]);
                return result;
            }

            double xa = pts[0].X, ya = pts[0].Y;
            double y2a = YAt(bound, xa);
            result.Add(new Pt(xa, keepAbove ? Math.Max(ya, y2a) : Math.Min(ya, y2a)));

            for (int i = 1; i < pts.Count; i++)
            {
                double xb = pts[i].X, yb = pts[i].Y;
                y2a = YAt(bound, xa);
                double y2b = YAt(bound, xb);
                if (Math.Abs(xb - xa) < 1e-9)
                    result.Add(new Pt(xb, keepAbove ? Math.Max(yb, y2b) : Math.Min(yb, y2b)));
                else
                {
                    var inner = new List<double>();
                    for (int k = 0; k < bound.Count; k++)
                    {
                        double x = bound[k].X;
                        if (x > xa + 1e-9 && x < xb - 1e-9) inner.Add(x);
                    }
                    inner.Sort();
                    inner.Add(xb);
                    double sxa = xa, sy1a = ya, sy2a = y2a;
                    foreach (double sxb in inner)
                    {
                        double sy1b = Math.Abs(sxb - xb) < 1e-9
                            ? yb
                            : ya + ((sxb - xa) / (xb - xa)) * (yb - ya);
                        double sy2b = YAt(bound, sxb);
                        bool aOut = keepAbove ? sy1a < sy2a - 1e-9 : sy1a > sy2a + 1e-9;
                        bool bOut = keepAbove ? sy1b < sy2b - 1e-9 : sy1b > sy2b + 1e-9;
                        if (aOut != bOut)
                        {
                            double d = (sy1b - sy1a) - (sy2b - sy2a);
                            if (Math.Abs(d) > 1e-12)
                            {
                                double xInt = sxa - ((sy1a - sy2a) / d) * (sxb - sxa);
                                double span = sxb - sxa;
                                double yInt = Math.Abs(span) < 1e-18
                                    ? sy2a
                                    : sy2a + ((xInt - sxa) / span) * (sy2b - sy2a);
                                if (xInt > sxa + 1e-9 && xInt < sxb - 1e-9)
                                    result.Add(new Pt(xInt, yInt));
                            }
                        }
                        if (insertBound || Math.Abs(sxb - xb) < 1e-9)
                            result.Add(new Pt(sxb, keepAbove ? Math.Max(sy1b, sy2b) : Math.Min(sy1b, sy2b)));
                        sxa = sxb;
                        sy1a = sy1b;
                        sy2a = sy2b;
                    }
                }
                xa = xb;
                ya = yb;
            }
            return result;
        }

        static double YAt(IList<Pt> pts, double x)
        {
            if (x <= pts[0].X) return pts[0].Y;
            if (x >= pts[pts.Count - 1].X) return pts[pts.Count - 1].Y;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (x >= pts[i].X && x <= pts[i + 1].X)
                {
                    double dx = pts[i + 1].X - pts[i].X;
                    double u = Math.Abs(dx) < 1e-12 ? 0 : (x - pts[i].X) / dx;
                    return pts[i].Y + u * (pts[i + 1].Y - pts[i].Y);
                }
            }
            return pts[pts.Count - 1].Y;
        }
    }
}
