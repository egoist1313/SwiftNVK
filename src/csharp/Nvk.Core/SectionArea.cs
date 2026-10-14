using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Площадь поперечника в м². Как pc-section-area:
    /// трапеция (x_низ + x_верх)*h; при m=0 стенки вертикальные;
    /// L1/L5 — Гаусс по контуру с отметками TIN на бровках.
    /// </summary>
    public static class SectionArea
    {
        public static double? For(SectionGeom g, ProfileKind kind, double mSlope)
        {
            return For(g, kind, mSlope, mSlope);
        }

        public static double? For(SectionGeom g, ProfileKind kind, double mLeft, double mRight)
        {
            if (g == null || g.Elev == null) return null;
            if (g.InManhole)
                return kind == ProfileKind.Truba ? (double?)null : 0.0;
            if (mLeft < 0.0) mLeft = 0.0;
            if (mRight < 0.0) mRight = 0.0;
            var e = g.Elev;
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    // Котлован до L6 (черная земля). L0 — будущее покрытие, в разработку не входит.
                    if (g.Elev.Surface <= g.Elev.TrenchBottom + 1e-9) return 0.0;
                    return Poly(TrenchGeom.EarthOutline(g, mLeft, mRight));
                case ProfileKind.Pesok:
                    {
                        double h = e.SandTop - e.TrenchBottom;
                        double bL = TrenchGeom.BottomLeft(g);
                        double bR = TrenchGeom.BottomRight(g);
                        double xL = TrenchGeom.XAtLeft(g, mLeft, e.SandTop);
                        double xR = TrenchGeom.XAtRight(g, mRight, e.SandTop);
                        return Band(bL, bR, xL, xR, h);
                    }
                case ProfileKind.Truba:
                    return null;
                case ProfileKind.Zashita:
                    {
                        var zash = TrenchGeom.ZashOutline(g, mLeft, mRight);
                        if (zash == null || zash.Count < 3) return 0.0;
                        double a = Poly(zash);
                        // Дырка = нарисованный круг (внутренний D), не оболочка.
                        a -= PipeInLayer(e.Invert, e.Diam, 0.0, e.SandTop, e.ZashTop);
                        return a < 0.0 ? 0.0 : a;
                    }
                case ProfileKind.Zasypka:
                    {
                        double z5 = LineClamp.AfterScatterBackfill(e.Surface, e.ZashTop);
                        if (z5 <= e.ZashTop + 1e-9) return 0.0;
                        return Poly(TrenchGeom.BackfillOutline(g, mLeft, mRight));
                    }
                default:
                    return null;
            }
        }

        public static double Poly(List<CsPt> pts)
        {
            if (pts == null || pts.Count < 3) return 0.0;
            double a = 0.0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % n];
                a += p.X * q.Z - q.X * p.Z;
            }
            return Math.Abs(a) * 0.5;
        }

        /// <summary>Площадь трапеции (полуширины) × высота, как в оригинале.</summary>
        public static double Trap(double xLow, double xHigh, double h)
        {
            if (h <= 0.0) return 0.0;
            return (xLow + xHigh) * h;
        }

        /// <summary>Площадь полосы при разных левой и правой полуширинах.</summary>
        public static double Band(double xL0, double xR0, double xL1, double xR1, double h)
        {
            if (h <= 0.0) return 0.0;
            return 0.5 * (xL0 + xR0 + xL1 + xR1) * h;
        }

        /// <summary>
        /// Площадь трубы в полосе [zLo, zHi], м². Оболочка: R = D/2 + стенка,
        /// центр = лоток + D/2. Ноль, если полоса не пересекает круг.
        /// </summary>
        public static double PipeInLayer(double invert, double diam, double wall, double zLo, double zHi)
        {
            if (diam <= 0.0) return 0.0;
            if (wall < 0.0) wall = 0.0;
            return CircleStrip(invert + diam * 0.5, diam * 0.5 + wall, zLo, zHi);
        }

        /// <summary>
        /// Площадь круга между горизонталями zLo и zHi.
        /// Крышка выше z: R² acos(d/R) − d√(R²−d²), d = z − zC.
        /// </summary>
        public static double CircleStrip(double zC, double r, double zLo, double zHi)
        {
            if (r <= 0.0) return 0.0;
            if (zHi < zLo)
            {
                double tmp = zLo;
                zLo = zHi;
                zHi = tmp;
            }
            if (zHi <= zLo + 1e-15) return 0.0;
            return CapAbove(zC, r, zLo) - CapAbove(zC, r, zHi);
        }

        static double CapAbove(double zC, double r, double z)
        {
            double d = z - zC;
            if (d >= r) return 0.0;
            if (d <= -r) return Math.PI * r * r;
            double x = d / r;
            if (x > 1.0) x = 1.0;
            if (x < -1.0) x = -1.0;
            return r * r * Math.Acos(x) - d * Math.Sqrt(Math.Max(0.0, r * r - d * d));
        }
    }
}
