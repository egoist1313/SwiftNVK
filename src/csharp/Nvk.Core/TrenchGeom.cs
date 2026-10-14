using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Поперечная геометрия траншеи в метрах (ось трубы x=0).</summary>
    public static class TrenchGeom
    {
        public static double Osh(StationElevations e)
        {
            return Math.Floor(e.TrenchBottom - 2.0);
        }

        public static double HalfBottom(SectionGeom g)
        {
            return g.TrenchW * 0.5;
        }

        public static double BottomLeft(SectionGeom g)
        {
            if (g != null && g.TrenchLeft > 1e-9) return g.TrenchLeft;
            return HalfBottom(g);
        }

        public static double BottomRight(SectionGeom g)
        {
            if (g != null && g.TrenchRight > 1e-9) return g.TrenchRight;
            return HalfBottom(g);
        }

        /// <summary>Полуширина по откосу на отметке z. Ниже дна = полуширина дна.</summary>
        public static double XAt(SectionGeom g, double mSlope, double z)
        {
            double zb = g.Elev.TrenchBottom;
            return HalfBottom(g) + mSlope * Math.Max(0.0, z - zb);
        }

        public static double XAtLeft(SectionGeom g, double mSlope, double z)
        {
            if (mSlope < 0.0) mSlope = 0.0;
            double zb = g.Elev.TrenchBottom;
            return BottomLeft(g) + mSlope * Math.Max(0.0, z - zb);
        }

        public static double XAtRight(SectionGeom g, double mSlope, double z)
        {
            if (mSlope < 0.0) mSlope = 0.0;
            double zb = g.Elev.TrenchBottom;
            return BottomRight(g) + mSlope * Math.Max(0.0, z - zb);
        }

        public static double XTop(SectionGeom g, double mSlope)
        {
            return XAt(g, mSlope, g.Elev.Surface);
        }

        public static double XTopLeft(SectionGeom g, double mSlope)
        {
            return XAtLeft(g, mSlope, g.Elev.Surface);
        }

        public static double XTopRight(SectionGeom g, double mSlope)
        {
            return XAtRight(g, mSlope, g.Elev.Surface);
        }

        /// <summary>Полуширина по откосу на уровне сырого TIN (Surface + Pavement).</summary>
        public static double XTopRaw(SectionGeom g, double mSlope)
        {
            return XAt(g, mSlope, g.Elev.Surface + g.Pavement);
        }

        public static double XTopRawLeft(SectionGeom g, double mSlope)
        {
            return XAtLeft(g, mSlope, g.Elev.Surface + g.Pavement);
        }

        public static double XTopRawRight(SectionGeom g, double mSlope)
        {
            return XAtRight(g, mSlope, g.Elev.Surface + g.Pavement);
        }

        /// <summary>Z земли (сырой TIN) в поперечнике на расстоянии T. = SurfZAt + Pavement.</summary>
        public static double SurfZAtRaw(SectionGeom g, double t)
        {
            return SurfZAt(g, t) + g.Pavement;
        }

        public static double ZCenter(SectionGeom g)
        {
            return g.Elev.Invert + g.Elev.Diam * 0.5;
        }

        /// <summary>Z земли в поперечнике на расстоянии T от оси. Линейно по SurfPts.</summary>
        public static double SurfZAt(SectionGeom g, double t)
        {
            double fallback = g.Elev.Surface;
            if (g.SurfPts == null || g.SurfPts.Count == 0)
                return fallback;
            var s = new List<SurfPt>(g.SurfPts);
            s.Sort((a, b) => a.T.CompareTo(b.T));
            if (t <= s[0].T) return s[0].Z;
            if (t >= s[s.Count - 1].T) return s[s.Count - 1].Z;
            for (int i = 0; i < s.Count - 1; i++)
            {
                if (t >= s[i].T && t <= s[i + 1].T)
                {
                    double dt = s[i + 1].T - s[i].T;
                    double u = dt < 1e-12 ? 0.0 : (t - s[i].T) / dt;
                    return s[i].Z + u * (s[i + 1].Z - s[i].Z);
                }
            }
            return fallback;
        }

        /// <summary>
        /// Контур котлована как pc-draw-layers: бровки ±xTop по TIN-pav, дно ±bHalf.
        /// m=0 → вертикальные стенки (xTop=bHalf), не диагональ на всю полуширину проб.
        /// Использует Surface = TIN - pav (без одежды).
        /// </summary>
        public static List<CsPt> EarthOutline(SectionGeom g, double mSlope)
        {
            return EarthOutline(g, mSlope, mSlope);
        }

        public static List<CsPt> EarthOutline(SectionGeom g, double mLeft, double mRight)
        {
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double zBot = g.Elev.TrenchBottom;
            double bL = BottomLeft(g);
            double bR = BottomRight(g);
            double xTopL = XTopLeft(g, mLeft);
            double xTopR = XTopRight(g, mRight);
            double zL = Math.Max(zBot, SurfZAt(g, -xTopL));
            double zR = Math.Max(zBot, SurfZAt(g, xTopR));
            return new List<CsPt>
            {
                new CsPt(-xTopL, zL),
                new CsPt(-bL, zBot),
                new CsPt(bR, zBot),
                new CsPt(xTopR, zR)
            };
        }

        /// <summary>
        /// Контур до сырого TIN (L0). L1 разработка его не использует:
        /// котлован до L6, L0 — будущее покрытие.
        /// </summary>
        public static List<CsPt> EarthOutlineRaw(SectionGeom g, double mSlope)
        {
            return EarthOutlineRaw(g, mSlope, mSlope);
        }

        public static List<CsPt> EarthOutlineRaw(SectionGeom g, double mLeft, double mRight)
        {
            if (g.Pavement < 1e-6) return EarthOutline(g, mLeft, mRight);
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double zBot = g.Elev.TrenchBottom;
            double bL = BottomLeft(g);
            double bR = BottomRight(g);
            double xTopL = XTopRawLeft(g, mLeft);
            double xTopR = XTopRawRight(g, mRight);
            double zL = Math.Max(zBot, SurfZAtRaw(g, -xTopL));
            double zR = Math.Max(zBot, SurfZAtRaw(g, xTopR));
            return new List<CsPt>
            {
                new CsPt(-xTopL, zL),
                new CsPt(-bL, zBot),
                new CsPt(bR, zBot),
                new CsPt(xTopR, zR)
            };
        }

        public static List<CsPt> BackfillOutline(SectionGeom g, double mSlope)
        {
            return BackfillOutline(g, mSlope, mSlope);
        }

        public static List<CsPt> BackfillOutline(SectionGeom g, double mLeft, double mRight)
        {
            if (!g.Elev.HasBackfill) return new List<CsPt>();
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double zZ = g.Elev.ZashTop;
            double xZL = XAtLeft(g, mLeft, zZ);
            double xZR = XAtRight(g, mRight, zZ);
            double xTopL = XTopLeft(g, mLeft);
            double xTopR = XTopRight(g, mRight);
            double zL = Math.Max(zZ, SurfZAt(g, -xTopL));
            double zR = Math.Max(zZ, SurfZAt(g, xTopR));
            return new List<CsPt>
            {
                new CsPt(-xZL, zZ),
                new CsPt(-xTopL, zL),
                new CsPt(xTopR, zR),
                new CsPt(xZR, zZ)
            };
        }

        /// <summary>
        /// Контур L5 как pc-zasypka-build-poly: бровки + точки TIN внутри котлована.
        /// Площадь по-прежнему считается по BackfillOutline (4 точки).
        /// </summary>
        public static List<CsPt> BackfillDrawPoly(SectionGeom g, double mSlope)
        {
            return BackfillDrawPoly(g, mSlope, mSlope);
        }

        public static List<CsPt> BackfillDrawPoly(SectionGeom g, double mLeft, double mRight)
        {
            var box = BackfillOutline(g, mLeft, mRight);
            if (box.Count < 4) return box;
            if (g.SurfPts == null || g.SurfPts.Count == 0) return box;
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double xTopL = XTopLeft(g, mLeft);
            double xTopR = XTopRight(g, mRight);
            var mid = new List<SurfPt>();
            foreach (var p in g.SurfPts)
            {
                if (p.T > -xTopL + 1e-9 && p.T < xTopR - 1e-9)
                    mid.Add(p);
            }
            mid.Sort((a, b) => a.T.CompareTo(b.T));
            var pts = new List<CsPt>(4 + mid.Count);
            pts.Add(box[0]);
            pts.Add(box[1]);
            foreach (var p in mid)
                pts.Add(new CsPt(p.T, Math.Max(g.Elev.ZashTop, p.Z)));
            pts.Add(box[2]);
            pts.Add(box[3]);
            return pts;
        }

        /// <summary>
        /// L0 (rawTin) или L6 (TIN−одежда) по всей ширине сечения.
        /// Без зажима к L1 — как на продольнике: линия остаётся на TIN.
        /// </summary>
        public static List<CsPt> SurfaceLine(SectionGeom g, bool rawTin)
        {
            double pav = rawTin ? Math.Max(0.0, g.Pavement) : 0.0;
            if (g.SurfPts != null && g.SurfPts.Count >= 2)
            {
                var s = new List<SurfPt>(g.SurfPts);
                s.Sort((a, b) => a.T.CompareTo(b.T));
                var all = new List<CsPt>(s.Count);
                foreach (var p in s)
                    all.Add(new CsPt(p.T, p.Z + pav));
                return all;
            }
            double z = g.Elev.Surface + pav;
            double x = Math.Max(XTop(g, 0.0), 1.0);
            return new List<CsPt> { new CsPt(-x, z), new CsPt(x, z) };
        }

        /// <summary>
        /// Крылья L0/L6 с разрывом в котловане ±xTop (как pc-draw-surface на L2–L4).
        /// До засыпки земли в яме нет — линии обрываются у стенки.
        /// </summary>
        public static void SurfaceWings(SectionGeom g, bool rawTin, double mSlope, out List<CsPt> left, out List<CsPt> right)
        {
            SurfaceWings(g, rawTin, mSlope, mSlope, out left, out right);
        }

        public static void SurfaceWings(SectionGeom g, bool rawTin, double mLeft, double mRight,
            out List<CsPt> left, out List<CsPt> right)
        {
            left = new List<CsPt>();
            right = new List<CsPt>();
            var all = SurfaceLine(g, rawTin);
            if (all == null || all.Count < 2) return;
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double xTopL = rawTin ? XTopRawLeft(g, mLeft) : XTopLeft(g, mLeft);
            double xTopR = rawTin ? XTopRawRight(g, mRight) : XTopRight(g, mRight);
            double zL = rawTin ? SurfZAtRaw(g, -xTopL) : SurfZAt(g, -xTopL);
            double zR = rawTin ? SurfZAtRaw(g, xTopR) : SurfZAt(g, xTopR);
            foreach (var p in all)
            {
                if (p.X < -xTopL - 1e-9) left.Add(p);
            }
            left.Add(new CsPt(-xTopL, zL));
            right.Add(new CsPt(xTopR, zR));
            foreach (var p in all)
            {
                if (p.X > xTopR + 1e-9) right.Add(p);
            }
        }

        /// <summary>Полоса одежды L6→L0 по точкам TIN. Пусто если pav=0.</summary>
        public static List<CsPt> PavementPoly(SectionGeom g)
        {
            if (g == null || g.Pavement < 1e-6) return new List<CsPt>();
            var l6 = SurfaceLine(g, false);
            var l0 = SurfaceLine(g, true);
            if (l6.Count < 2 || l0.Count < 2) return new List<CsPt>();
            var poly = new List<CsPt>(l6.Count + l0.Count);
            for (int i = 0; i < l6.Count; i++) poly.Add(l6[i]);
            for (int i = l0.Count - 1; i >= 0; i--) poly.Add(l0[i]);
            return poly;
        }

        /// <summary>
        /// L4 в точке x: L2≤L4≤L6; если местный L6 &lt; L2 — L4 на L2.
        /// Ось и стенки считаются по одному правилу.
        /// </summary>
        public static double ZashZAt(SectionGeom g, double x)
        {
            double sand = g.Elev.SandTop;
            return LineClamp.AfterScatterZash(g.Elev.ZashTop, sand, SurfZAt(g, x));
        }

        /// <summary>
        /// Контур защиты: верх min(L4, местный L6), низ L2.
        /// Пусто — L4 лежит на L2, штриховки нет.
        /// </summary>
        public static List<CsPt> ZashOutline(SectionGeom g, double mSlope)
        {
            return ZashOutline(g, mSlope, mSlope);
        }

        public static List<CsPt> ZashOutline(SectionGeom g, double mLeft, double mRight)
        {
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            double sand = g.Elev.SandTop;
            double z4 = LineClamp.AfterScatterZash(g.Elev.ZashTop, sand, g.Elev.Surface);
            if (z4 <= sand + 1e-9) return new List<CsPt>();

            double xInvL = XAtLeft(g, mLeft, sand);
            double xInvR = XAtRight(g, mRight, sand);
            double xZL = XAtLeft(g, mLeft, z4);
            double xZR = XAtRight(g, mRight, z4);
            var xs = new List<double> { -xZL, -xInvL, xInvR, xZR };
            if (g.SurfPts != null)
            {
                foreach (var p in g.SurfPts)
                {
                    if (p.T > -xZL + 1e-9 && p.T < xZR - 1e-9)
                        xs.Add(p.T);
                }
            }
            xs.Sort();
            var top = new List<CsPt>();
            double prev = double.NaN;
            foreach (double x in xs)
            {
                if (!double.IsNaN(prev) && Math.Abs(x - prev) < 1e-9) continue;
                prev = x;
                top.Add(new CsPt(x, ZashZAt(g, x)));
            }

            bool any = false;
            for (int i = 0; i < top.Count; i++)
            {
                if (top[i].Z > sand + 1e-9) { any = true; break; }
            }
            if (!any || top.Count < 2) return new List<CsPt>();

            var poly = new List<CsPt>(top.Count + 2);
            for (int i = 0; i < top.Count; i++) poly.Add(top[i]);
            poly.Add(new CsPt(xInvR, sand));
            poly.Add(new CsPt(-xInvL, sand));
            return poly;
        }
    }

    public struct CsPt
    {
        public double X;
        public double Z;
        public CsPt(double x, double z) { X = x; Z = z; }
    }

    public struct SurfPt
    {
        public double T;
        public double Z;
        public SurfPt(double t, double z) { T = t; Z = z; }
    }
}
