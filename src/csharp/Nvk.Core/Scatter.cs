using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// «Факт»: нормализованные [-1..+1] как pb-gen-scatter-vals / pb-interp-val-at-x.
    /// Смещение в метрах = val × амплитуда. На чертеже × h_scale.
    /// </summary>
    public static class Scatter
    {
        public static List<double> Generate(int n, int seed)
        {
            if (n < 2) n = 2;
            var r = new Random(seed);
            var vals = new List<double>(n);
            for (int i = 0; i < n; i++)
                vals.Add(r.NextDouble() * 2.0 - 1.0);
            return vals;
        }

        public static int DenseCount(double totalLen)
        {
            int n = 20;
            int byLen = (int)Math.Ceiling(totalLen / 2.0) + 1;
            if (byLen > n) n = byLen;
            if (n < 2) n = 2;
            return n;
        }

        public static double At(double ch, double total, IList<double> vals)
        {
            if (vals == null || vals.Count == 0) return 0.0;
            if (vals.Count == 1) return vals[0];
            if (total < 1e-9) return vals[0];
            double t = ch / total;
            if (t <= 0.0) return vals[0];
            if (t >= 1.0) return vals[vals.Count - 1];
            double x = t * (vals.Count - 1);
            int i = (int)Math.Floor(x);
            if (i >= vals.Count - 1) return vals[vals.Count - 1];
            double u = x - i;
            return vals[i] + u * (vals[i + 1] - vals[i]);
        }

        public static double Meters(double ch, double total, IList<double> vals, double amp)
        {
            if (amp <= 0.0 || vals == null) return 0.0;
            return At(ch, total, vals) * amp;
        }

        /// <summary>Нормализованный ряд из смещений в метрах: val = dz / amp.</summary>
        public static List<double> FromOffsets(IList<double> dzMeters, double amp)
        {
            var r = new List<double>();
            if (dzMeters == null || dzMeters.Count == 0) return r;
            double a = amp < 1e-9 ? 1.0 : amp;
            for (int i = 0; i < dzMeters.Count; i++)
                r.Add(dzMeters[i] / a);
            return r;
        }

        public static double DefaultAmp(ProfileKind kind)
        {
            return kind == ProfileKind.Truba ? 0.005 : 0.05;
        }

        /// <summary>
        /// Набор чисел принадлежит линии (L1 дно, L2 песок…), а не листу профиля.
        /// Дно траншеи на листе песка — тот же набор, что на разработке.
        /// </summary>
        public static ProfileKind SeriesOf(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.SandTop:    return ProfileKind.Pesok;
                case HeightSrc.Invert:
                case HeightSrc.PipeTop:    return ProfileKind.Truba;
                case HeightSrc.ZashTop:    return ProfileKind.Zashita;
                case HeightSrc.Surface:    return ProfileKind.Zasypka;
                default:                   return ProfileKind.Razrabotka;
            }
        }

        public static double Amp(ProfileKind kind, SectionConfig cfg)
        {
            if (cfg == null) return 0.0;
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                case ProfileKind.Pesok:
                    return cfg.UseScatterRazr ? cfg.ScatterRazr : 0.0;
                case ProfileKind.Truba:
                    return cfg.UseScatterTruba ? cfg.ScatterTruba : 0.0;
                case ProfileKind.Zashita:
                    return cfg.UseScatterZashita ? cfg.ScatterZashita : 0.0;
                case ProfileKind.Zasypka:
                    return cfg.UseScatterZasypka ? cfg.ScatterZasypka : 0.0;
                default:
                    return 0.0;
            }
        }

        public static double LatOff(double hw, int seed, int i)
        {
            if (hw <= 0.0) return 0.0;
            var r = new Random(seed + i * 997);
            return (r.NextDouble() * 2.0 - 1.0) * 0.60 * hw;
        }

        public static List<double> PlanTargets(double total, double planInt, int seed)
        {
            var t = new List<double>();
            if (total <= 1e-6) return t;
            if (planInt <= 1.0) planInt = 50.0;
            int nZones = Math.Max(1, (int)Math.Ceiling((total - 0.001) / planInt));
            var r = new Random(seed);
            for (int i = 0; i < nZones; i++)
            {
                double z0 = i * planInt;
                double z1 = Math.Min((i + 1) * planInt, total);
                double zlen = z1 - z0;
                double mid = 0.5 * (z0 + z1);
                double scatter = zlen * 0.30;
                double ch = mid + (r.NextDouble() * 2.0 - 1.0) * scatter;
                double lo = z0 + zlen * 0.10;
                double hi = z1 - zlen * 0.10;
                if (ch < lo) ch = lo;
                if (ch > hi) ch = hi;
                t.Add(ch);
            }
            return t;
        }

        public static double PlanLat(double hw, int seed, int i)
        {
            if (hw <= 0.0) return 0.3;
            var r = new Random(seed + 17 + i * 333);
            double u = 0.30 + r.NextDouble() * 0.30;
            double sign = r.NextDouble() < 0.5 ? -1.0 : 1.0;
            return sign * u * hw;
        }
    }
}
