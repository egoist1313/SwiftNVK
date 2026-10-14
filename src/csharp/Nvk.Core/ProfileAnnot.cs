using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nvk.Core
{
    public sealed class AnnotPoint
    {
        public double Ch { get; set; }
        public double Z { get; set; }
        public string Text { get; set; }
    }

    public sealed class AnnotSeg
    {
        public double Ch0 { get; set; }
        public double Ch1 { get; set; }
        public double Mid { get { return 0.5 * (Ch0 + Ch1); } }
        public string LengthText { get; set; }
        public string SlopeText { get; set; }
    }

    /// <summary>
    /// Таблица продольника: отметки на стыках труб, длины и уклоны сегментов.
    /// Не блок, не реактор — пересчёт только этапом POPER.
    /// </summary>
    public static class ProfileAnnot
    {
        /// <summary>Как PipeBuild pb-format-profile-title: имя сети Civil, без внутреннего SegId.</summary>
        public static string SheetTitle(string netName)
        {
            if (string.IsNullOrEmpty(netName)) return "Продольный профиль";
            return "Продольный профиль сети " + netName;
        }

        public static NamedLine HeightLine(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return NamedLine.TrenchBottom;
                case ProfileKind.Pesok:      return NamedLine.SandTop;
                case ProfileKind.Truba:      return NamedLine.SandTop;
                case ProfileKind.Zashita:    return NamedLine.ZashTop;
                case ProfileKind.Zasypka:    return NamedLine.Surface;
                default: return NamedLine.SandTop;
            }
        }

        public static string HeightLabel(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "Дно траншеи, м";
                case ProfileKind.Pesok:      return "Верх песка (лоток), м";
                case ProfileKind.Truba:      return "Отметка лотка, м";
                case ProfileKind.Zashita:    return "Верх защиты, м";
                case ProfileKind.Zasypka:    return "Отметка земли, м";
                default: return "Отметка, м";
            }
        }

        public static string FmtZ(double z)
        {
            return z.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string FmtLen(double m)
        {
            return m.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Уклон в промилле. Падение вниз — отрицательный.</summary>
        public static string FmtSlope(double z0, double z1, double len)
        {
            if (len < 1e-6) return "—";
            double p = (z1 - z0) / len * 1000.0;
            return p.ToString("0.0", CultureInfo.InvariantCulture) + "‰";
        }

        public static List<AnnotPoint> Heights(Chain chain, ProfileKind kind, SectionConfig cfg, Func<double, double, double, double?> surfAtXy)
        {
            var line = HeightLine(kind);
            var pts = new List<AnnotPoint>();
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i <= n; i++)
            {
                var hit = i == 0 ? chain.Locate(0.0) : chain.Locate(ch);
                double z = ZAt(hit, line, cfg, surfAtXy);
                pts.Add(new AnnotPoint { Ch = hit.Station, Z = z, Text = FmtZ(z) });
                if (i < n) ch += chain.Segs[i].Length2d;
            }
            return pts;
        }

        public static List<AnnotSeg> Segments(Chain chain)
        {
            var list = new List<AnnotSeg>();
            double ch = 0.0;
            foreach (var seg in chain.Segs)
            {
                double len = seg.Length2d;
                list.Add(new AnnotSeg
                {
                    Ch0 = ch,
                    Ch1 = ch + len,
                    LengthText = FmtLen(len),
                    SlopeText = FmtSlope(seg.InvertStart, seg.InvertEnd, len)
                });
                ch += len;
            }
            return list;
        }

        static double ZAt(StationHit hit, NamedLine line, SectionConfig cfg, Func<double, double, double, double?> surfAtXy)
        {
            var p = new AxisPt
            {
                Ch = hit.Station, X = hit.X, Y = hit.Y,
                Invert = hit.Invert, Diam = hit.Diam
            };
            if (surfAtXy != null)
            {
                var z = surfAtXy(hit.X, hit.Y, hit.Invert);
                if (z.HasValue) p.Surface = z.Value;
            }
            return p.NamedZ(line, cfg);
        }
    }
}
