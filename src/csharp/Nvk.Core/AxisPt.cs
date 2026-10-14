using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Точка оси участка: пикетаж + план + лоток. Surface заполняет Cad из TIN.</summary>
    public sealed class AxisPt
    {
        public double Ch { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Invert { get; set; }
        public double Diam { get; set; }
        public double? Surface { get; set; }
        /// <summary>Проектное покрытие — сырой TIN.</summary>
        public double? DesignZ { get; set; }
        /// <summary>Черная земля или подошва одежды (TIN − толщина зоны).</summary>
        public double? EarthZ { get; set; }

        public void ClampToExcavation(SectionConfig cfg)
        {
            if (cfg == null) cfg = new SectionConfig();
            double dRaw = DesignZ ?? Surface ?? Invert;
            double eRaw = EarthZ ?? dRaw;
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(Invert, Diam, cfg.Bedding, cfg.ThkZash, dRaw, eRaw, cfg.WallAt(Diam),
                out trench, out sand, out pipe, out zash, out earth, out design);
            DesignZ = design;
            EarthZ = earth;
            Surface = earth;
        }

        public double NamedZ(NamedLine line, SectionConfig cfg)
        {
            if (cfg == null) cfg = new SectionConfig();
            double dRaw = DesignZ ?? Surface ?? Invert;
            double eRaw = EarthZ ?? dRaw;
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(Invert, Diam, cfg.Bedding, cfg.ThkZash, dRaw, eRaw, cfg.WallAt(Diam),
                out trench, out sand, out pipe, out zash, out earth, out design);
            switch (line)
            {
                case NamedLine.TrenchBottom: return trench;
                case NamedLine.SandTop:      return sand;
                case NamedLine.PipeTop:      return pipe;
                case NamedLine.ZashTop:      return zash;
                case NamedLine.Surface:      return earth;
                default: throw new ArgumentOutOfRangeException(nameof(line));
            }
        }
    }

    /// <summary>
    /// Вершины оси: края труб + шаг. Скачок лотка на колодце — две точки с одним пикетажем.
    /// </summary>
    public static class AxisBuilder
    {
        public const double DefaultStep = 2.0;

        public static List<AxisPt> FromChain(Chain chain, double step)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (step <= 0.0) step = DefaultStep;
            var pts = new List<AxisPt>();
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var seg = chain.Segs[i];
                double len = seg.Length2d;
                if (i == 0)
                    pts.Add(At(chain, ch, seg.InvertStart, seg.Diam));

                if (len > step * 1.5)
                {
                    for (double s = ch + step; s < ch + len - 0.05; s += step)
                    {
                        var hit = chain.Locate(s);
                        pts.Add(FromHit(hit));
                    }
                }

                pts.Add(At(chain, ch + len, seg.InvertEnd, seg.Diam));

                if (i + 1 < n)
                {
                    var next = chain.Segs[i + 1];
                    if (Math.Abs(next.InvertStart - seg.InvertEnd) > 0.001)
                        pts.Add(At(chain, ch + len, next.InvertStart, next.Diam));
                }
                ch += len;
            }
            return pts;
        }

        static AxisPt FromHit(StationHit hit)
        {
            return new AxisPt
            {
                Ch = hit.Station, X = hit.X, Y = hit.Y,
                Invert = hit.Invert, Diam = hit.Diam
            };
        }

        static AxisPt At(Chain chain, double s, double invert, double diam)
        {
            var hit = chain.Locate(s);
            return new AxisPt
            {
                Ch = s, X = hit.X, Y = hit.Y,
                Invert = invert, Diam = diam
            };
        }

        public static double Osh(IList<AxisPt> pts, SectionConfig cfg)
        {
            double min = double.MaxValue;
            foreach (var p in pts)
            {
                double z = p.NamedZ(NamedLine.TrenchBottom, cfg);
                if (z < min) min = z;
            }
            if (min > 1e20) min = 0.0;
            return Math.Floor(min);
        }
    }
}
