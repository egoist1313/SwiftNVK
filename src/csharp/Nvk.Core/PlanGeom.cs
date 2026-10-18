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

        /// <summary>Полуширина ситуационного коридора, м. PipeBuild: ±10 м от оси.</summary>
        public const double SitHalfM = 10.0;
        /// <summary>Прореживание коридора: коллинеарные ближе этого (м) выкидываем.</summary>
        public const double SitSimplifyM = 0.25;

        /// <summary>
        /// Вылет оси за крайние вершины (центры колодцев) на dist метров
        /// вдоль первого и последнего ребра. OFFSET открытой полилинии
        /// без этого ставит торец коридора в колодец.
        /// Направления берутся с исходной оси, чтобы 2-точечная трасса
        /// не поехала после сдвига первой вершины.
        /// </summary>
        public static List<PlanPt> ExtendEnds(List<PlanPt> axis, double dist)
        {
            var r = new List<PlanPt>();
            if (axis == null || axis.Count == 0) return r;
            r.AddRange(axis);
            if (r.Count < 2 || dist <= 0.0) return r;
            double ux, uy;
            if (Unit(axis[0], axis[1], out ux, out uy))
                r[0] = new PlanPt(axis[0].X - ux * dist, axis[0].Y - uy * dist);
            int last = axis.Count - 1;
            if (Unit(axis[last - 1], axis[last], out ux, out uy))
                r[r.Count - 1] = new PlanPt(axis[last].X + ux * dist, axis[last].Y + uy * dist);
            return r;
        }

        static bool Unit(PlanPt a, PlanPt b, out double ux, out double uy)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-12)
            {
                ux = 0.0;
                uy = 0.0;
                return false;
            }
            ux = dx / len;
            uy = dy / len;
            return true;
        }
        /// <summary>Остриё выноски «Предъявляемый участок» от оси, м.</summary>
        public const double SitArrowM = 6.0;
        /// <summary>Полка выноски за границей коридора, м.</summary>
        public const double SitTextM = 14.0;

        /// <summary>
        /// Убрать почти коллинеарные вершины, чтобы OFFSET ±10 м не пилил зубцы
        /// на стыках труб.
        /// </summary>
        public static List<PlanPt> Simplify(List<PlanPt> pts, double tol)
        {
            var r = new List<PlanPt>();
            if (pts == null || pts.Count == 0) return r;
            if (pts.Count <= 2 || tol <= 0.0)
            {
                r.AddRange(pts);
                return r;
            }
            r.Add(pts[0]);
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var a = r[r.Count - 1];
                var b = pts[i];
                var c = pts[i + 1];
                if (DistToSeg(a, c, b) > tol)
                    r.Add(b);
            }
            r.Add(pts[pts.Count - 1]);
            if (r.Count < 2)
            {
                r.Clear();
                r.AddRange(pts);
            }
            return r;
        }

        /// <summary>
        /// Замкнутый контур: выкинуть коллинеарные и дубли, углы оставить.
        /// Плотная нарезка OFFSET (0.5 м) схлопывается до сторон коридора.
        /// </summary>
        public static List<PlanPt> SimplifyClosed(List<PlanPt> pts, double tol)
        {
            var r = new List<PlanPt>();
            if (pts == null || pts.Count == 0) return r;
            for (int i = 0; i < pts.Count; i++)
            {
                if (r.Count > 0)
                {
                    double dx = r[r.Count - 1].X - pts[i].X;
                    double dy = r[r.Count - 1].Y - pts[i].Y;
                    if (dx * dx + dy * dy < 1e-12) continue;
                }
                r.Add(pts[i]);
            }
            if (r.Count >= 2)
            {
                double dx = r[0].X - r[r.Count - 1].X;
                double dy = r[0].Y - r[r.Count - 1].Y;
                if (dx * dx + dy * dy < 1e-12)
                    r.RemoveAt(r.Count - 1);
            }
            if (r.Count <= 3 || tol <= 0.0) return r;
            bool dropped = true;
            while (dropped && r.Count > 3)
            {
                dropped = false;
                for (int i = 0; i < r.Count && r.Count > 3; )
                {
                    int n = r.Count;
                    var prev = r[(i - 1 + n) % n];
                    var next = r[(i + 1) % n];
                    if (DistToSeg(prev, next, r[i]) <= tol)
                    {
                        r.RemoveAt(i);
                        dropped = true;
                    }
                    else i++;
                }
            }
            return r;
        }

        static double DistToSeg(PlanPt a, PlanPt c, PlanPt b)
        {
            double lx = c.X - a.X, ly = c.Y - a.Y;
            double len = Math.Sqrt(lx * lx + ly * ly);
            if (len < 1e-12)
            {
                double dx = b.X - a.X, dy = b.Y - a.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            return Math.Abs(lx * (b.Y - a.Y) - ly * (b.X - a.X)) / len;
        }

        /// <summary>Замкнутый контур коридора постоянной полуширины, как pb-plan-make-situation.</summary>
        public static List<PlanPt> SituationClosed(List<PlanPt> axis, double halfWidth)
        {
            var empty = new List<PlanPt>();
            if (axis == null || axis.Count < 2 || halfWidth <= 0.0) return empty;
            var hws = new List<double>(axis.Count - 1);
            for (int i = 0; i < axis.Count - 1; i++)
                hws.Add(halfWidth);
            var left = OffsetPolyVar(axis, hws, 1.0);
            var right = OffsetPolyVar(axis, hws, -1.0);
            if (left.Count < 2 || right.Count < 2) return empty;
            var closed = new List<PlanPt>(left.Count + right.Count);
            closed.AddRange(left);
            for (int i = right.Count - 1; i >= 0; i--)
                closed.Add(right[i]);
            return closed;
        }

        /// <summary>
        /// Выноска по середине трассы: остриё и текст влево от оси (нормаль CCW).
        /// Как pb-draw-plan-view: mid = n/2, направление от предыдущей вершины.
        /// </summary>
        public static bool SituationLeader(List<PlanPt> axis, double arrowDist, double textDist,
            out PlanPt arrow, out PlanPt text)
        {
            arrow = default(PlanPt);
            text = default(PlanPt);
            if (axis == null || axis.Count < 2) return false;
            int mid = axis.Count / 2;
            if (mid < 1) mid = 1;
            PlanPt p0 = axis[mid - 1];
            PlanPt p1 = axis[mid];
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return false;
            double px = -dy / len;
            double py = dx / len;
            arrow = new PlanPt(p1.X + px * arrowDist, p1.Y + py * arrowDist);
            text = new PlanPt(p1.X + px * textDist, p1.Y + py * textDist);
            return true;
        }

        /// <summary>Масштаб ANSI31 коридора: 5 × max(1, DIMSCALE), как pb-plan-make-situation.</summary>
        public static double SituationHatchScale(double dimscale)
        {
            return 5.0 * Math.Max(1.0, dimscale);
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
