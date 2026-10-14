using System;

namespace Nvk.Core
{
    /// <summary>
    /// Порядок линий (снизу вверх):
    ///   L4 ≥ L2. Если L6 ≥ L2, то L4 ≤ L6; если L6 < L2 — L4 на L2.
    /// Одежда не ниже разработки; защита и засыпка не выше подошвы разработки (L6).
    /// </summary>
    public static class LineClamp
    {
        public static void Apply(
            double invert, double diam, double bedding, double thkZash,
            double designRaw, double earthRaw,
            out double trench, out double sand, out double pipe,
            out double zash, out double earth, out double design)
        {
            double wall = 0.0;
            Apply(invert, diam, bedding, thkZash, designRaw, earthRaw, wall,
                out trench, out sand, out pipe, out zash, out earth, out design);
        }

        public static void Apply(
            double invert, double diam, double bedding, double thkZash,
            double designRaw, double earthRaw, double wall,
            out double trench, out double sand, out double pipe,
            out double zash, out double earth, out double design)
        {
            if (wall < 0.0) wall = 0.0;
            sand = invert - wall;
            trench = sand - Math.Max(0.0, bedding);
            // Civil diam = внутренний. Оболочка: L2 = лоток − стенка, верх = лоток + ID + стенка.
            pipe = invert + Math.Max(0.0, diam) + wall;
            zash = pipe + Math.Max(0.0, thkZash);
            design = designRaw;
            earth = earthRaw;
            // L4 не ниже L2. Не выше L6, если L6 ≥ L2. Если L6 < L2 — L4 лежит на L2.
            if (earth < sand)
                zash = sand;
            else
            {
                if (zash < sand) zash = sand;
                if (zash > earth) zash = earth;
            }
        }

        /// <summary>После рандома: L4 на L2 если L6&lt;L2, иначе L2≤L4≤L6.</summary>
        public static double AfterScatterZash(double zash, double sand, double earth)
        {
            if (earth < sand) return sand;
            if (zash < sand) zash = sand;
            if (zash > earth) zash = earth;
            return zash;
        }

        /// <summary>После рандома L1 не выше L2.</summary>
        public static double AfterScatterTrench(double trench, double sand)
        {
            return trench > sand ? sand : trench;
        }

        /// <summary>После рандома L6 не ниже L1.</summary>
        public static double AfterScatterEarth(double earth, double trench)
        {
            return earth < trench ? trench : earth;
        }

        /// <summary>L5 (засыпка) не ниже L4 даже с рандомом.</summary>
        public static double AfterScatterBackfill(double surf, double zash)
        {
            return surf < zash ? zash : surf;
        }
    }
}
