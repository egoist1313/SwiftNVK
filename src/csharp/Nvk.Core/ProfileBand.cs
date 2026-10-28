using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Вершина полосы продольника: пикет + отметка.</summary>
    public struct ChZ
    {
        public double Ch;
        public double Z;
        public ChZ(double c, double z) { Ch = c; Z = z; }
    }

    /// <summary>
    /// Рандом и разрывы колодцев той же полосы, что штриховка профиля.
    /// DomainResult живёт в Cad — амплитуды и ряды передаются сюда.
    /// </summary>
    public sealed class BandOpts
    {
        public double AmpTrench;
        public double AmpSand;
        public double AmpZash;
        public double AmpSurf;
        public IList<double> ValsTrench;
        public IList<double> ValsSand;
        public IList<double> ValsZash;
        public IList<double> ValsSurf;
        public bool GapMhRazr = true;
        public bool GapMhPesok = true;
        public bool GapMhZash = true;
        public bool GapMhZas = true;

        public bool GapMh(ProfileKind k)
        {
            switch (k)
            {
                case ProfileKind.Razrabotka: return GapMhRazr;
                case ProfileKind.Pesok: return GapMhPesok;
                case ProfileKind.Zashita: return GapMhZash;
                case ProfileKind.Zasypka: return GapMhZas;
                default: return true;
            }
        }

        public double Amp(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.TrenchBottom: return AmpTrench;
                case HeightSrc.SandTop: return AmpSand;
                case HeightSrc.ZashTop: return AmpZash;
                case HeightSrc.Surface: return AmpSurf;
                default: return 0.0;
            }
        }

        public IList<double> Vals(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.TrenchBottom: return ValsTrench;
                case HeightSrc.SandTop: return ValsSand;
                case HeightSrc.ZashTop: return ValsZash;
                case HeightSrc.Surface: return ValsSurf;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Площадь штриховки продольника. Легенда профиля и вертикальные
    /// объёмы V=S×B берут S отсюда — один контур, одни клипы и рандом.
    /// </summary>
    public static class ProfileBand
    {
        public static Dictionary<int, double> AreasByDiam(
            Chain chain, IList<AxisPt> axis, SectionConfig cfg, ProfileKind kind, BandOpts opts)
        {
            var result = new Dictionary<int, double>();
            if (kind == ProfileKind.Truba || chain == null) return result;
            List<ChZ> top, bot;
            Build(chain, axis, cfg, kind, opts, out top, out bot);
            SumByDiam(chain, top, bot, opts != null && opts.GapMh(kind), result);
            return result;
        }

        public static List<Tuple<int, string, double>> AreasZasypka(
            Chain chain, IList<AxisPt> axis, SectionConfig cfg, BandOpts opts)
        {
            var acc = new Dictionary<string, Tuple<int, string, double>>();
            if (chain == null) return new List<Tuple<int, string, double>>();
            List<ChZ> top, bot;
            Build(chain, axis, cfg, ProfileKind.Zasypka, opts, out top, out bot);
            bool gap = opts != null && opts.GapMh(ProfileKind.Zasypka);
            double ch = 0.0;
            foreach (var seg in chain.Segs)
            {
                double ch0 = gap ? ch + GapR(seg, true) : ch;
                double ch1 = gap ? ch + seg.Length2d - GapR(seg, false) : ch + seg.Length2d;
                int dmm = seg.DiamMm;
                double x = ch0;
                while (x < ch1 - 1e-6)
                {
                    double x1 = RoadZones.EndAt(x, cfg != null ? cfg.Zones : null, ch1);
                    if (x1 <= x + 1e-9) x1 = ch1;
                    double a = BandArea(top, bot, x, x1);
                    string zt = cfg != null ? cfg.ZoneAt(x) : null;
                    string ru = BackfillMap.Ru(BackfillMap.Of(zt, cfg));
                    string key = dmm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + ru;
                    Tuple<int, string, double> old;
                    if (!acc.TryGetValue(key, out old))
                        old = Tuple.Create(dmm, ru, 0.0);
                    acc[key] = Tuple.Create(old.Item1, old.Item2, old.Item3 + a);
                    x = x1;
                }
                ch += seg.Length2d;
            }
            var list = new List<Tuple<int, string, double>>(acc.Values);
            list.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : string.CompareOrdinal(a.Item2, b.Item2));
            return list;
        }

        /// <summary>Те же верх/низ, что у штриховки продольника (клип L2/L4/L6 + рандом).</summary>
        public static void Build(Chain chain, IList<AxisPt> axis, SectionConfig cfg, ProfileKind kind,
            BandOpts opts, out List<ChZ> top, out List<ChZ> bot)
        {
            var earth = BuildEarthChZ(axis, cfg);
            var sand = BuildPipeChZ(chain, HeightSrc.SandTop, cfg);
            var trench = BuildBotChZ(chain, cfg);
            double total = chain != null ? chain.Total : 1.0;

            if (kind == ProfileKind.Razrabotka)
            {
                bot = trench;
                top = earth;
                double amp = AmpOf(opts, HeightSrc.TrenchBottom);
                if (amp > 1e-9)
                {
                    bot = ScatterChZ(bot, total, opts, amp, HeightSrc.TrenchBottom);
                    bot = ClipChZBelow(bot, sand);
                }
                top = ClipChZAbove(top, bot);
                return;
            }
            if (kind == ProfileKind.Pesok)
            {
                top = sand;
                bot = trench;
                double aTop = AmpOf(opts, HeightSrc.SandTop);
                double aBot = AmpOf(opts, HeightSrc.TrenchBottom);
                if (aTop > 1e-9)
                    top = ScatterChZ(top, total, opts, aTop, HeightSrc.SandTop);
                if (aBot > 1e-9)
                {
                    bot = ScatterChZ(bot, total, opts, aBot, HeightSrc.TrenchBottom);
                    bot = ClipChZBelow(bot, top);
                }
                if (top != null && top.Count >= 2 && bot != null && bot.Count >= 2)
                    top = ClipChZAbove(top, bot);
                return;
            }
            if (kind == ProfileKind.Zashita)
            {
                List<ChZ> pesokTop, pesokBot;
                Build(chain, axis, cfg, ProfileKind.Pesok, opts, out pesokTop, out pesokBot);
                bot = pesokTop;
                top = BuildPipeChZ(chain, HeightSrc.ZashTop, cfg);
                double amp = AmpOf(opts, HeightSrc.ZashTop);
                if (amp > 1e-9)
                    top = ScatterChZ(top, total, opts, amp, HeightSrc.ZashTop);
                top = ClipChZAbove(top, bot);
                var ceil = ClipChZAbove(earth, bot);
                top = ClipChZBelow(top, ceil);
                return;
            }
            List<ChZ> zashTop, zashBot;
            Build(chain, axis, cfg, ProfileKind.Zashita, opts, out zashTop, out zashBot);
            bot = zashTop;
            top = BuildSurfChZ(axis, cfg, HeightSrc.Surface);
            top = ClipChZAbove(top, bot);
            double amp5 = AmpOf(opts, HeightSrc.Surface);
            if (amp5 > 1e-9)
            {
                top = ScatterChZ(top, total, opts, amp5, HeightSrc.Surface);
                top = ClipChZAbove(top, bot);
            }
        }

        public static double BandArea(List<ChZ> top, List<ChZ> bot, double ch0, double ch1)
        {
            if (top == null || bot == null || ch1 <= ch0 + 1e-9)
                return 0.0;
            var breaks = new SortedSet<double> { ch0, ch1 };
            foreach (var p in top)
                if (p.Ch > ch0 + 1e-9 && p.Ch < ch1 - 1e-9) breaks.Add(p.Ch);
            foreach (var p in bot)
                if (p.Ch > ch0 + 1e-9 && p.Ch < ch1 - 1e-9) breaks.Add(p.Ch);
            double area = 0.0, prevCh = double.NaN, prevH = double.NaN;
            foreach (double brk in breaks)
            {
                double h = Interp(top, brk) - Interp(bot, brk);
                if (h < 0.0) h = 0.0;
                if (!double.IsNaN(prevCh) && brk > prevCh + 1e-9)
                    area += 0.5 * (h + prevH) * (brk - prevCh);
                prevCh = brk;
                prevH = h;
            }
            return area < 0.0 ? 0.0 : area;
        }

        public static double PipeEndZ(PipeSeg s, bool start, HeightSrc src, SectionConfig cfg)
        {
            double inv = start ? s.InvertStart : s.InvertEnd;
            double d = s.Diam;
            double bed = cfg != null ? cfg.Bedding : 0.1;
            double thk = cfg != null ? cfg.ThkZash : 0.3;
            double wall = cfg != null ? cfg.WallAt(d) : 0.0;
            switch (src)
            {
                case HeightSrc.TrenchBottom: return inv - wall - bed;
                case HeightSrc.SandTop: return inv - wall;
                case HeightSrc.Invert: return inv;
                case HeightSrc.PipeTop: return inv + d + wall;
                case HeightSrc.ZashTop: return inv + d + wall + thk;
                case HeightSrc.DeltaZash:
                    return ProfileRows.DeltaZashThickness(inv + d + wall + thk, inv + d + wall);
                default: return inv;
            }
        }

        static void SumByDiam(Chain chain, List<ChZ> top, List<ChZ> bot, bool gapAtMh, Dictionary<int, double> result)
        {
            double ch = 0.0;
            foreach (var seg in chain.Segs)
            {
                double ch0 = gapAtMh ? ch + GapR(seg, true) : ch;
                double ch1 = gapAtMh ? ch + seg.Length2d - GapR(seg, false) : ch + seg.Length2d;
                double a = (ch1 > ch0 + 1e-6) ? BandArea(top, bot, ch0, ch1) : 0.0;
                int dmm = seg.DiamMm;
                if (!result.ContainsKey(dmm)) result[dmm] = 0.0;
                result[dmm] += a;
                ch += seg.Length2d;
            }
        }

        static double GapR(PipeSeg s, bool start)
        {
            return s == null ? 0.0 : s.ShaftR(start);
        }

        static double AmpOf(BandOpts opts, HeightSrc src)
        {
            return opts == null ? 0.0 : opts.Amp(src);
        }

        static List<ChZ> ScatterChZ(List<ChZ> src, double total, BandOpts opts, double amp, HeightSrc line)
        {
            var vals = opts != null ? opts.Vals(line) : null;
            var r = new List<ChZ>(src.Count);
            foreach (var p in src)
                r.Add(new ChZ(p.Ch, p.Z + Scatter.Meters(p.Ch, total, vals, amp)));
            return r;
        }

        static List<ChZ> ClipChZAbove(List<ChZ> pts, List<ChZ> floor)
        {
            return ClipChZ(pts, floor, true);
        }

        static List<ChZ> ClipChZBelow(List<ChZ> pts, List<ChZ> ceil)
        {
            return ClipChZ(pts, ceil, false);
        }

        static List<ChZ> ClipChZ(List<ChZ> pts, List<ChZ> bound, bool above)
        {
            if (pts == null || pts.Count < 2 || bound == null || bound.Count < 2) return pts;
            var a = new List<PolylineClip.Pt>(pts.Count);
            var b = new List<PolylineClip.Pt>(bound.Count);
            foreach (var p in pts) a.Add(new PolylineClip.Pt(p.Ch, p.Z));
            foreach (var p in bound) b.Add(new PolylineClip.Pt(p.Ch, p.Z));
            var c = above ? PolylineClip.KeepAbove(a, b) : PolylineClip.KeepBelow(a, b);
            var r = new List<ChZ>(c.Count);
            foreach (var p in c) r.Add(new ChZ(p.X, p.Y));
            return r;
        }

        static List<ChZ> BuildEarthChZ(IList<AxisPt> axis, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
            if (axis == null) return pts;
            double prevZ = double.NaN, prevCh = -1e9;
            foreach (var p in axis)
            {
                double z = ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
                if (!double.IsNaN(prevZ) && Math.Abs(p.Ch - prevCh) < 1e-6 && Math.Abs(z - prevZ) > 1e-4)
                    pts.Add(new ChZ(p.Ch, z));
                else if (Math.Abs(p.Ch - prevCh) > 1e-6 || pts.Count == 0)
                    pts.Add(new ChZ(p.Ch, z));
                else if (Math.Abs(z - prevZ) > 1e-4)
                {
                    pts.Add(new ChZ(p.Ch, prevZ));
                    pts.Add(new ChZ(p.Ch, z));
                }
                prevZ = z;
                prevCh = p.Ch;
            }
            return pts;
        }

        static List<ChZ> BuildSurfChZ(IList<AxisPt> axis, SectionConfig cfg, HeightSrc src)
        {
            var pts = new List<ChZ>();
            if (axis == null) return pts;
            double lastCh = -1e9, lastZ = 0.0;
            foreach (var p in axis)
            {
                double z = ProfileRows.ZAt(src, p, cfg);
                if (pts.Count > 0 && AxisBuilder.IsDupVertex(p.Ch, z, lastCh, lastZ))
                    continue;
                pts.Add(new ChZ(p.Ch, z));
                lastCh = p.Ch;
                lastZ = z;
            }
            return pts;
        }

        static List<ChZ> BuildBotChZ(Chain chain, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
            if (chain == null) return pts;
            double bed = cfg != null ? cfg.Bedding : 0.1;
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var s = chain.Segs[i];
                double wall = cfg != null ? cfg.WallAt(s.Diam) : 0.0;
                if (i == 0) pts.Add(new ChZ(ch, s.InvertStart - wall - bed));
                ch += s.Length2d;
                pts.Add(new ChZ(ch, s.InvertEnd - wall - bed));
                if (i + 1 < n && Math.Abs(chain.Segs[i + 1].InvertStart - s.InvertEnd) > 0.001)
                {
                    double wallNx = cfg != null ? cfg.WallAt(chain.Segs[i + 1].Diam) : 0.0;
                    pts.Add(new ChZ(ch, chain.Segs[i + 1].InvertStart - wallNx - bed));
                }
            }
            return pts;
        }

        static List<ChZ> BuildPipeChZ(Chain chain, HeightSrc src, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
            if (chain == null) return pts;
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var s = chain.Segs[i];
                if (i == 0) pts.Add(new ChZ(ch, PipeEndZ(s, true, src, cfg)));
                ch += s.Length2d;
                pts.Add(new ChZ(ch, PipeEndZ(s, false, src, cfg)));
                if (i + 1 < n)
                {
                    var nx = chain.Segs[i + 1];
                    if (Math.Abs(PipeEndZ(nx, true, src, cfg) - PipeEndZ(s, false, src, cfg)) > 0.001)
                        pts.Add(new ChZ(ch, PipeEndZ(nx, true, src, cfg)));
                }
            }
            return pts;
        }

        static double Interp(List<ChZ> pts, double ch)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            if (ch <= pts[0].Ch) return pts[0].Z;
            if (ch >= pts[pts.Count - 1].Ch) return pts[pts.Count - 1].Z;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i].Ch <= ch && pts[i + 1].Ch >= ch)
                {
                    double span = pts[i + 1].Ch - pts[i].Ch;
                    if (span < 1e-9) return pts[i + 1].Z;
                    return pts[i].Z + (ch - pts[i].Ch) / span * (pts[i + 1].Z - pts[i].Z);
                }
            }
            return pts[pts.Count - 1].Z;
        }
    }
}
