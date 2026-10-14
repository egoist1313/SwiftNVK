using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    public struct PlanPt
    {
        public double X;
        public double Y;
        public PlanPt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Ось и контур траншеи в плане (метры, мировые XY).</summary>
    public static class PlanGeom
    {
        public static List<PlanPt> Axis(Chain chain)
        {
            var pts = new List<PlanPt>();
            if (chain == null || chain.Segs.Count == 0) return pts;
            var first = chain.Segs[0];
            pts.Add(new PlanPt(first.X0, first.Y0));
            foreach (var s in chain.Segs)
                pts.Add(new PlanPt(s.X1, s.Y1));
            return pts;
        }

        public static List<PlanPt> TrenchClosed(Chain chain, JointType joint)
        {
            return TrenchClosed(chain, null, joint);
        }

        public static List<PlanPt> TrenchClosed(Chain chain, SectionConfig cfg)
        {
            return TrenchClosed(chain, cfg, cfg != null ? cfg.Joint : JointType.Socket);
        }

        static List<PlanPt> TrenchClosed(Chain chain, SectionConfig cfg, JointType joint)
        {
            var pts = Axis(chain);
            var leftHw = new List<double>();
            var rightHw = new List<double>();
            if (chain != null)
            {
                foreach (var s in chain.Segs)
                {
                    if (cfg != null)
                    {
                        leftHw.Add(cfg.BottomLeft(s.Diam));
                        rightHw.Add(cfg.BottomRight(s.Diam));
                    }
                    else
                    {
                        double h = TrenchWidth.For(s.Diam, joint) * 0.5;
                        leftHw.Add(h);
                        rightHw.Add(h);
                    }
                }
            }
            var left = OffsetPolyVar(pts, leftHw, 1.0);
            var right = OffsetPolyVar(pts, rightHw, -1.0);
            var closed = new List<PlanPt>(left);
            for (int i = right.Count - 1; i >= 0; i--)
                closed.Add(right[i]);
            return closed;
        }

        /// <summary>
        /// Смещение ломаной с переменной полушириной, как pb-plan-offset-poly-var:
        /// miter на стыке, вылет ≤ 4×max(hw), иначе фаска.
        /// </summary>
        public static List<PlanPt> OffsetPolyVar(List<PlanPt> pts, List<double> hws, double side)
        {
            var result = new List<PlanPt>();
            if (pts == null || hws == null || pts.Count < 2 || hws.Count < pts.Count - 1)
                return result;
            int n = pts.Count;
            int ns = n - 1;
            var dx = new double[ns];
            var dy = new double[ns];
            var nx = new double[ns];
            var ny = new double[ns];
            double maxHw = 0.0;
            for (int i = 0; i < ns; i++)
            {
                double lx = pts[i + 1].X - pts[i].X;
                double ly = pts[i + 1].Y - pts[i].Y;
                double len = Math.Sqrt(lx * lx + ly * ly);
                if (len < 1e-12) { dx[i] = 1; dy[i] = 0; nx[i] = 0; ny[i] = 1; }
                else
                {
                    dx[i] = lx / len;
                    dy[i] = ly / len;
                    nx[i] = -dy[i];
                    ny[i] = dx[i];
                }
                if (hws[i] > maxHw) maxHw = hws[i];
            }
            for (int i = 0; i < n; i++)
            {
                if (i == 0)
                    result.Add(Off(pts[0], nx[0], ny[0], side * hws[0]));
                else if (i == n - 1)
                    result.Add(Off(pts[n - 1], nx[ns - 1], ny[ns - 1], side * hws[ns - 1]));
                else
                {
                    double hp = side * hws[i - 1];
                    double hi = side * hws[i];
                    var p0 = Off(pts[i], nx[i - 1], ny[i - 1], hp);
                    var p1 = Off(pts[i], nx[i], ny[i], hi);
                    PlanPt hit;
                    bool ok = Isect(p0, dx[i - 1], dy[i - 1], p1, dx[i], dy[i], out hit)
                        && SameSide(pts[i], dx[i - 1], dy[i - 1], hit, side)
                        && SameSide(pts[i], dx[i], dy[i], hit, side);
                    if (ok)
                    {
                        double ddx = hit.X - pts[i].X, ddy = hit.Y - pts[i].Y;
                        double dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                        if (dist > 4.0 * Math.Max(maxHw, 1e-6))
                            result.Add(new PlanPt(0.5 * (p0.X + p1.X), 0.5 * (p0.Y + p1.Y)));
                        else
                            result.Add(hit);
                    }
                    else
                        result.Add(new PlanPt(0.5 * (p0.X + p1.X), 0.5 * (p0.Y + p1.Y)));
                }
            }
            return result;
        }

        static PlanPt Off(PlanPt p, double nx, double ny, double d)
        {
            return new PlanPt(p.X + nx * d, p.Y + ny * d);
        }

        /// <summary>
        /// Точка по ту же сторону от направленного ребра, что и смещение side:
        /// cross(dir, hit−vertex) имеет знак side. Иначе miter проткнул ось.
        /// </summary>
        static bool SameSide(PlanPt vertex, double dx, double dy, PlanPt hit, double side)
        {
            double cross = dx * (hit.Y - vertex.Y) - dy * (hit.X - vertex.X);
            return cross * side > -1e-9;
        }

        static bool Isect(PlanPt p, double dx, double dy, PlanPt q, double ex, double ey, out PlanPt hit)
        {
            double cross = dx * ey - dy * ex;
            if (Math.Abs(cross) < 1e-12)
            {
                hit = default(PlanPt);
                return false;
            }
            double t = ((q.X - p.X) * ey - (q.Y - p.Y) * ex) / cross;
            hit = new PlanPt(p.X + t * dx, p.Y + t * dy);
            return true;
        }
    }
}
