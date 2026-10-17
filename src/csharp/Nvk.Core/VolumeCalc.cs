using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nvk.Core
{
    /// <summary>Один член объёма: (S₀+S₁)/2·ΔL либо S·ΔL на торце.</summary>
    public sealed class VolTerm
    {
        public double A0 { get; set; }
        public double A1 { get; set; }
        public double DL { get; set; }
        public bool Average { get; set; }
        public double Volume { get { return Average ? 0.5 * (A0 + A1) * DL : A0 * DL; } }
    }

    public sealed class VolRow
    {
        public double Diam { get; set; }
        public double Length { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
        public double Width { get; set; }
        public double PipeVol { get; set; }
        public BackfillKind? Fill { get; set; }
        public List<VolTerm> Terms { get; set; }
    }

    public sealed class VolLevel
    {
        public ProfileKind Kind { get; set; }
        public string Title { get; set; }
        public List<VolRow> Rows { get; set; }
        public double TotalVolume { get; set; }
    }

    /// <summary>
    /// Объёмы земляных работ.
    /// Вертикальные стенки (m=0): V = Sпрофиля × Bдна (как pb-vt-vol-one-level).
    /// Откос (m&gt;0): V = Σ (Sᵢ+Sᵢ₊₁)/2 · ΔL по поперечникам.
    /// L4: из объёма вычитается пересечение трубы со слоем [L2, L4].
    /// </summary>
    public static class VolumeCalc
    {
        public static bool VerticalWalls(SectionConfig cfg)
        {
            if (cfg == null) return true;
            return cfg.SlopeLeft() <= 1e-9 && cfg.SlopeRight() <= 1e-9;
        }

        public static List<VolLevel> All(Chain chain, IList<AxisPt> axis, SectionConfig cfg)
        {
            return All(chain, axis, cfg, null);
        }

        public static List<VolLevel> All(Chain chain, IList<AxisPt> axis, SectionConfig cfg, IList<SectionGeom> geoms)
        {
            var list = new List<VolLevel>();
            Add(list, chain, axis, cfg, geoms, ProfileKind.Razrabotka, "Разработка");
            Add(list, chain, axis, cfg, geoms, ProfileKind.Pesok, "Песок");
            Add(list, chain, axis, cfg, geoms, ProfileKind.Truba, "Труба");
            Add(list, chain, axis, cfg, geoms, ProfileKind.Zashita, "Защитный слой");
            Add(list, chain, axis, cfg, geoms, ProfileKind.Zasypka, "Обратная засыпка");
            return list;
        }

        /// <summary>Один набор уровней: строки одного диаметра склеиваются, формула общая.</summary>
        public static List<VolLevel> MergeAll(IList<List<VolLevel>> sets)
        {
            var map = new Dictionary<ProfileKind, VolLevel>();
            if (sets != null)
            {
                for (int i = 0; i < sets.Count; i++)
                {
                    var set = sets[i];
                    if (set == null) continue;
                    for (int j = 0; j < set.Count; j++)
                        MergeInto(map, set[j]);
                }
            }
            var list = new List<VolLevel>();
            AddIf(map, list, ProfileKind.Razrabotka);
            AddIf(map, list, ProfileKind.Pesok);
            AddIf(map, list, ProfileKind.Truba);
            AddIf(map, list, ProfileKind.Zashita);
            AddIf(map, list, ProfileKind.Zasypka);
            return list;
        }

        static void AddIf(Dictionary<ProfileKind, VolLevel> map, List<VolLevel> list, ProfileKind kind)
        {
            VolLevel lv;
            if (map.TryGetValue(kind, out lv) && lv != null) list.Add(lv);
        }

        static void MergeInto(Dictionary<ProfileKind, VolLevel> map, VolLevel src)
        {
            if (src == null) return;
            VolLevel dst;
            if (!map.TryGetValue(src.Kind, out dst) || dst == null)
            {
                map[src.Kind] = CloneLevel(src);
                return;
            }
            dst.TotalVolume += src.TotalVolume;
            if (src.Rows == null) return;
            if (dst.Rows == null) dst.Rows = new List<VolRow>();
            foreach (var row in src.Rows)
            {
                if (row == null) continue;
                int hit = -1;
                for (int i = 0; i < dst.Rows.Count; i++)
                {
                    if (SameRow(dst.Rows[i], row)) { hit = i; break; }
                }
                if (hit < 0) dst.Rows.Add(CloneRow(row));
                else MergeRow(dst.Rows[hit], row);
            }
            dst.Rows.Sort(CompareRows);
        }

        static bool SameRow(VolRow a, VolRow b)
        {
            if (a == null || b == null) return false;
            if (TableLayout.DiamMm(a.Diam) != TableLayout.DiamMm(b.Diam)) return false;
            if (a.Fill.HasValue != b.Fill.HasValue) return false;
            if (a.Fill.HasValue && a.Fill.Value != b.Fill.Value) return false;
            return true;
        }

        static void MergeRow(VolRow a, VolRow b)
        {
            double l0 = a.Length, l1 = b.Length;
            a.Length = l0 + l1;
            a.Area += b.Area;
            a.Volume += b.Volume;
            a.PipeVol += b.PipeVol;
            if (l0 + l1 > 1e-9)
                a.Width = (a.Width * l0 + b.Width * l1) / (l0 + l1);
            if (b.Terms != null && b.Terms.Count > 0)
            {
                if (a.Terms == null) a.Terms = new List<VolTerm>();
                a.Terms.AddRange(b.Terms);
            }
        }

        static VolLevel CloneLevel(VolLevel src)
        {
            var rows = new List<VolRow>();
            if (src.Rows != null)
            {
                for (int i = 0; i < src.Rows.Count; i++)
                    if (src.Rows[i] != null) rows.Add(CloneRow(src.Rows[i]));
            }
            return new VolLevel
            {
                Kind = src.Kind,
                Title = src.Title,
                Rows = rows,
                TotalVolume = src.TotalVolume
            };
        }

        static VolRow CloneRow(VolRow src)
        {
            var terms = new List<VolTerm>();
            if (src.Terms != null)
            {
                for (int i = 0; i < src.Terms.Count; i++)
                {
                    var t = src.Terms[i];
                    if (t == null) continue;
                    terms.Add(new VolTerm { A0 = t.A0, A1 = t.A1, DL = t.DL, Average = t.Average });
                }
            }
            return new VolRow
            {
                Diam = src.Diam,
                Length = src.Length,
                Area = src.Area,
                Volume = src.Volume,
                Width = src.Width,
                PipeVol = src.PipeVol,
                Fill = src.Fill,
                Terms = terms
            };
        }

        static void Add(List<VolLevel> list, Chain chain, IList<AxisPt> axis, SectionConfig cfg,
            IList<SectionGeom> geoms, ProfileKind kind, string title)
        {
            var rows = ForLevel(chain, axis, kind, cfg, geoms);
            double t = 0.0;
            foreach (var r in rows) t += r.Volume;
            list.Add(new VolLevel { Kind = kind, Title = title, Rows = rows, TotalVolume = t });
        }

        public static List<VolRow> ForLevel(Chain chain, IList<AxisPt> axis, ProfileKind kind, SectionConfig cfg)
        {
            return ForLevel(chain, axis, kind, cfg, null);
        }

        public static List<VolRow> ForLevel(Chain chain, IList<AxisPt> axis, ProfileKind kind, SectionConfig cfg,
            IList<SectionGeom> geoms)
        {
            var map = new Dictionary<string, VolRow>();
            bool vertical = VerticalWalls(cfg);
            bool splitZas = kind == ProfileKind.Zasypka && cfg != null
                && cfg.Zones != null && cfg.Zones.Count > 0;
            double ch = 0.0;
            foreach (var seg in chain.Segs)
            {
                double gross = seg.Length2d;
                double net = seg.NetLength;
                double B = cfg != null ? cfg.BottomFull(seg.Diam) : TrenchWidth.For(seg.Diam, JointType.Socket);
                double a0 = kind == ProfileKind.Razrabotka ? ch : ch + seg.StartR;
                double a1 = kind == ProfileKind.Razrabotka ? ch + gross : ch + gross - seg.EndR;

                if (kind == ProfileKind.Truba)
                {
                    Acc(map, seg.Diam, null, net, 0.0, 0.0, B, 0.0, null);
                }
                else if (splitZas)
                {
                    double t = a0;
                    while (t < a1 - 1e-9)
                    {
                        double t2 = RoadZones.EndAt(t, cfg.Zones, a1);
                        if (t2 <= t + 1e-6) t2 = a1;
                        var fill = BackfillMap.Of(cfg.ZoneAt((t + t2) * 0.5), cfg);
                        AccSpan(map, seg, axis, cfg, geoms, kind, vertical, t, t2, B, fill);
                        t = t2;
                    }
                }
                else
                {
                    AccSpan(map, seg, axis, cfg, geoms, kind, vertical, a0, a1, B, null);
                }
                ch += gross;
            }
            if (!vertical)
            {
                foreach (var row in map.Values)
                    row.Area = row.Length > 1e-9 ? row.Volume / row.Length : 0.0;
            }
            var list = new List<VolRow>(map.Values);
            list.Sort(CompareRows);
            return list;
        }

        static int CompareRows(VolRow a, VolRow b)
        {
            int c = a.Diam.CompareTo(b.Diam);
            if (c != 0) return c;
            int fa = a.Fill.HasValue ? (int)a.Fill.Value : -1;
            int fb = b.Fill.HasValue ? (int)b.Fill.Value : -1;
            return fa.CompareTo(fb);
        }

        static void AccSpan(Dictionary<string, VolRow> map, PipeSeg seg,
            IList<AxisPt> axis, SectionConfig cfg, IList<SectionGeom> geoms,
            ProfileKind kind, bool vertical, double a0, double a1, double B, BackfillKind? fill)
        {
            double useLen = a1 - a0;
            if (useLen < 1e-9) return;
            double area;
            double vol;
            double pipeVol = 0.0;
            List<VolTerm> terms = null;
            if (vertical)
            {
                area = IntegrateBand(axis, a0, a1, cfg, kind);
                vol = area * B;
                if (kind == ProfileKind.Zashita && area > 1e-9)
                {
                    pipeVol = IntegratePipeHole(axis, a0, a1, cfg);
                    vol -= pipeVol;
                    if (vol < 0.0) vol = 0.0;
                }
            }
            else
            {
                terms = new List<VolTerm>();
                vol = AverageEndArea(geoms, axis, a0, a1, cfg, kind, seg.Diam, terms);
                area = useLen > 1e-9 ? vol / useLen : 0.0;
            }
            Acc(map, seg.Diam, fill, useLen, area, vol, B, pipeVol, terms);
        }

        static void Acc(Dictionary<string, VolRow> map, double diam, BackfillKind? fill,
            double len, double area, double vol, double B, double pipeVol, List<VolTerm> terms)
        {
            string key = TableLayout.DiamMm(diam).ToString() + ":" + (fill.HasValue ? BackfillMap.Code(fill.Value) : "-");
            VolRow row;
            if (!map.TryGetValue(key, out row))
            {
                row = new VolRow
                {
                    Diam = diam,
                    Fill = fill,
                    Width = B,
                    Terms = new List<VolTerm>()
                };
                map[key] = row;
            }
            row.Length += len;
            row.Area += area;
            row.Volume += vol;
            row.PipeVol += pipeVol;
            if (terms != null && terms.Count > 0)
                row.Terms.AddRange(terms);
        }

        /// <summary>Площадь полосы на продольнике, м²: ∫ толщины слоя dch.</summary>
        static double IntegrateBand(IList<AxisPt> axis, double ch0, double ch1, SectionConfig cfg, ProfileKind kind)
        {
            if (axis == null || axis.Count < 2 || ch1 <= ch0 + 1e-9)
                return 0.0;
            var xs = SampleCh(axis, ch0, ch1);
            double area = 0.0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double t1 = ThicknessAt(axis, xs[i], cfg, kind);
                double t2 = ThicknessAt(axis, xs[i + 1], cfg, kind);
                area += 0.5 * (t1 + t2) * (xs[i + 1] - xs[i]);
            }
            return area;
        }

        static double ThicknessAt(IList<AxisPt> axis, double ch, SectionConfig cfg, ProfileKind kind)
        {
            AxisPt a, b;
            double t;
            Bracket(axis, ch, out a, out b, out t);
            double trench = Ler(a.NamedZ(NamedLine.TrenchBottom, cfg), b.NamedZ(NamedLine.TrenchBottom, cfg), t);
            double sand = Ler(a.NamedZ(NamedLine.SandTop, cfg), b.NamedZ(NamedLine.SandTop, cfg), t);
            double zash = Ler(a.NamedZ(NamedLine.ZashTop, cfg), b.NamedZ(NamedLine.ZashTop, cfg), t);
            double earth = Ler(a.NamedZ(NamedLine.Surface, cfg), b.NamedZ(NamedLine.Surface, cfg), t);
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    return Math.Max(0.0, earth - trench);
                case ProfileKind.Pesok:
                    return Math.Max(0.0, sand - trench);
                case ProfileKind.Zashita:
                    return Math.Max(0.0, zash - sand);
                case ProfileKind.Zasypka:
                    {
                        double z5 = LineClamp.AfterScatterBackfill(earth, zash);
                        return Math.Max(0.0, z5 - zash);
                    }
                default:
                    return 0.0;
            }
        }

        static double Ler(double a, double b, double t)
        {
            return a * (1.0 - t) + b * t;
        }

        /// <summary>Объём трубы внутри слоя [L2, L4]: ∫ площади сегмента dch.</summary>
        static double IntegratePipeHole(IList<AxisPt> axis, double ch0, double ch1, SectionConfig cfg)
        {
            if (axis == null || axis.Count < 2 || ch1 <= ch0 + 1e-9)
                return 0.0;
            var xs = SampleCh(axis, ch0, ch1);
            double vol = 0.0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double h1 = PipeHoleAt(axis, xs[i], cfg);
                double h2 = PipeHoleAt(axis, xs[i + 1], cfg);
                vol += 0.5 * (h1 + h2) * (xs[i + 1] - xs[i]);
            }
            return vol;
        }

        static double PipeHoleAt(IList<AxisPt> axis, double ch, SectionConfig cfg)
        {
            AxisPt a, b;
            double t;
            Bracket(axis, ch, out a, out b, out t);
            double diam = Ler(a.Diam, b.Diam, t);
            double inv = Ler(a.Invert, b.Invert, t);
            double sand = Ler(a.NamedZ(NamedLine.SandTop, cfg), b.NamedZ(NamedLine.SandTop, cfg), t);
            double zash = Ler(a.NamedZ(NamedLine.ZashTop, cfg), b.NamedZ(NamedLine.ZashTop, cfg), t);
            return SectionArea.PipeInLayer(inv, diam, 0.0, sand, zash);
        }

        /// <summary>
        /// Метод средних площадей по нарисованным поперечникам.
        /// Сечения в колодцах (чуть вне net-интервала) тоже участвуют: площадь
        /// в ch0/ch1 берётся интерполяцией, объём только по длине трубы.
        /// Нет сечений этого диаметра — интеграл по оси.
        /// </summary>
        static double AverageEndArea(IList<SectionGeom> geoms, IList<AxisPt> axis,
            double ch0, double ch1, SectionConfig cfg, ProfileKind kind, double diam, List<VolTerm> terms)
        {
            if (ch1 <= ch0 + 1e-9) return 0.0;
            double mL = cfg == null ? 0.0 : cfg.SlopeLeft();
            double mR = cfg == null ? 0.0 : cfg.SlopeRight();
            var all = new List<KeyValuePair<double, double>>();
            if (geoms != null)
            {
                foreach (var g in geoms)
                {
                    if (g == null || g.Elev == null || g.InManhole) continue;
                    if (diam > 1e-9 && Math.Abs(g.Elev.Diam - diam) > 0.002) continue;
                    double? a = SectionArea.For(g, kind, mL, mR);
                    all.Add(new KeyValuePair<double, double>(g.Elev.Station, a ?? 0.0));
                }
                all.Sort((p, q) => p.Key.CompareTo(q.Key));
            }
            if (all.Count == 0)
                return IntegrateCs(axis, ch0, ch1, cfg, kind, terms);

            var samples = ClipAreaSamples(all, ch0, ch1);
            if (samples.Count < 2)
                return IntegrateCs(axis, ch0, ch1, cfg, kind, terms);

            double vol = 0.0;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                double dL = samples[i + 1].Key - samples[i].Key;
                if (dL > 1e-9)
                {
                    vol += 0.5 * (samples[i].Value + samples[i + 1].Value) * dL;
                    AddTerm(terms, samples[i].Value, samples[i + 1].Value, dL, true);
                }
            }
            return vol;
        }

        static List<KeyValuePair<double, double>> ClipAreaSamples(
            List<KeyValuePair<double, double>> all, double ch0, double ch1)
        {
            var result = new List<KeyValuePair<double, double>>();
            result.Add(new KeyValuePair<double, double>(ch0, AreaAt(all, ch0)));
            foreach (var p in all)
            {
                if (p.Key > ch0 + 1e-6 && p.Key < ch1 - 1e-6)
                    result.Add(p);
            }
            result.Add(new KeyValuePair<double, double>(ch1, AreaAt(all, ch1)));
            return result;
        }

        static double AreaAt(List<KeyValuePair<double, double>> all, double ch)
        {
            if (all == null || all.Count == 0) return 0.0;
            if (ch <= all[0].Key) return all[0].Value;
            var last = all[all.Count - 1];
            if (ch >= last.Key) return last.Value;
            for (int i = 0; i < all.Count - 1; i++)
            {
                if (ch >= all[i].Key - 1e-12 && ch <= all[i + 1].Key + 1e-12)
                {
                    double span = all[i + 1].Key - all[i].Key;
                    double u = span < 1e-12 ? 0.0 : (ch - all[i].Key) / span;
                    return all[i].Value * (1.0 - u) + all[i + 1].Value * u;
                }
            }
            return last.Value;
        }

        static void AddTerm(List<VolTerm> terms, double a0, double a1, double dL, bool average)
        {
            if (terms == null || dL <= 1e-9) return;
            terms.Add(new VolTerm { A0 = a0, A1 = a1, DL = dL, Average = average });
        }

        static List<double> SampleCh(IList<AxisPt> axis, double ch0, double ch1)
        {
            var xs = new List<double> { ch0 };
            foreach (var p in axis)
            {
                if (p.Ch > ch0 + 1e-6 && p.Ch < ch1 - 1e-6)
                    xs.Add(p.Ch);
            }
            xs.Add(ch1);
            xs.Sort();
            return xs;
        }

        static void Bracket(IList<AxisPt> axis, double ch, out AxisPt a, out AxisPt b, out double t)
        {
            a = axis[0];
            b = axis[axis.Count - 1];
            for (int i = 0; i < axis.Count - 1; i++)
            {
                if (ch >= axis[i].Ch - 1e-9 && ch <= axis[i + 1].Ch + 1e-9)
                {
                    a = axis[i];
                    b = axis[i + 1];
                    break;
                }
            }
            t = Math.Abs(b.Ch - a.Ch) < 1e-9 ? 0.0 : (ch - a.Ch) / (b.Ch - a.Ch);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
        }

        /// <summary>Объём: ∫ площади поперечника dch. Запасной путь при откосе без сечений.</summary>
        static double IntegrateCs(IList<AxisPt> axis, double ch0, double ch1, SectionConfig cfg, ProfileKind kind,
            List<VolTerm> terms)
        {
            if (axis == null || axis.Count < 2 || ch1 <= ch0 + 1e-9)
                return 0.0;
            var xs = new List<double> { ch0 };
            foreach (var p in axis)
            {
                if (p.Ch > ch0 + 1e-6 && p.Ch < ch1 - 1e-6)
                    xs.Add(p.Ch);
            }
            xs.Add(ch1);
            xs.Sort();
            double vol = 0.0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double a1 = CsSlice(axis, xs[i], cfg, kind);
                double a2 = CsSlice(axis, xs[i + 1], cfg, kind);
                double dL = xs[i + 1] - xs[i];
                vol += 0.5 * (a1 + a2) * dL;
                AddTerm(terms, a1, a2, dL, true);
            }
            return vol;
        }

        static double CsSlice(IList<AxisPt> axis, double ch, SectionConfig cfg, ProfileKind kind)
        {
            AxisPt a = axis[0], b = axis[axis.Count - 1];
            for (int i = 0; i < axis.Count - 1; i++)
            {
                if (ch >= axis[i].Ch - 1e-9 && ch <= axis[i + 1].Ch + 1e-9)
                {
                    a = axis[i];
                    b = axis[i + 1];
                    break;
                }
            }
            double t = Math.Abs(b.Ch - a.Ch) < 1e-9 ? 0.0 : (ch - a.Ch) / (b.Ch - a.Ch);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            double diam = a.Diam * (1.0 - t) + b.Diam * t;
            double inv = a.Invert * (1.0 - t) + b.Invert * t;
            double zEarth = a.NamedZ(NamedLine.Surface, cfg) * (1.0 - t) + b.NamedZ(NamedLine.Surface, cfg) * t;
            double zTin = zEarth + cfg.PavementAt(ch);
            if (a.DesignZ.HasValue && b.DesignZ.HasValue)
                zTin = a.DesignZ.Value * (1.0 - t) + b.DesignZ.Value * t;
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(inv, diam, cfg.Bedding, cfg.ThkZash, zTin, zEarth, cfg.WallAt(diam),
                out trench, out sand, out pipe, out zash, out earth, out design);
            double mL = cfg.SlopeLeft();
            double mR = cfg.SlopeRight();
            double bL = cfg.BottomLeft(diam);
            double bR = cfg.BottomRight(diam);
            if (kind == ProfileKind.Razrabotka)
            {
                double d = Math.Max(0.0, earth - trench);
                return SectionArea.Band(bL, bR, bL + mL * d, bR + mR * d, d);
            }
            if (kind == ProfileKind.Pesok)
            {
                double h = sand - trench;
                if (h <= 1e-9) return 0.0;
                return SectionArea.Band(bL, bR, bL + mL * h, bR + mR * h, h);
            }
            if (kind == ProfileKind.Zashita)
            {
                double h = zash - sand;
                if (h <= 1e-9) return 0.0;
                double xInvL = bL + mL * (sand - trench);
                double xInvR = bR + mR * (sand - trench);
                double xZL = bL + mL * (zash - trench);
                double xZR = bR + mR * (zash - trench);
                double hole = SectionArea.PipeInLayer(inv, diam, 0.0, sand, zash);
                double areaCs = SectionArea.Band(xInvL, xInvR, xZL, xZR, h) - hole;
                return areaCs < 0.0 ? 0.0 : areaCs;
            }
            double z5 = LineClamp.AfterScatterBackfill(earth, zash);
            double d5 = z5 - zash;
            if (d5 <= 1e-9) return 0.0;
            double xZ5L = bL + mL * Math.Max(0.0, zash - trench);
            double xZ5R = bR + mR * Math.Max(0.0, zash - trench);
            double xTL = bL + mL * Math.Max(0.0, z5 - trench);
            double xTR = bR + mR * Math.Max(0.0, z5 - trench);
            return SectionArea.Band(xZ5L, xZ5R, xTL, xTR, d5);
        }

        /// <summary>Площадь в продольном разрезе: ∫ max(0, Surface − base) dch.</summary>
        static double Integrate(IList<AxisPt> axis, double ch0, double ch1, SectionConfig cfg, NamedLine baseLine)
        {
            if (axis == null || axis.Count < 2 || ch1 <= ch0 + 1e-9)
                return 0.0;
            var xs = new List<double> { ch0 };
            foreach (var p in axis)
            {
                if (p.Ch > ch0 + 1e-6 && p.Ch < ch1 - 1e-6)
                    xs.Add(p.Ch);
            }
            xs.Add(ch1);
            xs.Sort();
            double area = 0.0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double d1 = Depth(axis, xs[i], cfg, baseLine);
                double d2 = Depth(axis, xs[i + 1], cfg, baseLine);
                area += 0.5 * (d1 + d2) * (xs[i + 1] - xs[i]);
            }
            return area;
        }

        static double Depth(IList<AxisPt> axis, double ch, SectionConfig cfg, NamedLine baseLine)
        {
            AxisPt a = axis[0], b = axis[axis.Count - 1];
            for (int i = 0; i < axis.Count - 1; i++)
            {
                if (ch >= axis[i].Ch - 1e-9 && ch <= axis[i + 1].Ch + 1e-9)
                {
                    a = axis[i];
                    b = axis[i + 1];
                    break;
                }
            }
            double t = Math.Abs(b.Ch - a.Ch) < 1e-9 ? 0.0 : (ch - a.Ch) / (b.Ch - a.Ch);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            double zSurf = a.NamedZ(NamedLine.Surface, cfg) * (1.0 - t) + b.NamedZ(NamedLine.Surface, cfg) * t;
            double zBase = a.NamedZ(baseLine, cfg) * (1.0 - t) + b.NamedZ(baseLine, cfg) * t;
            return Math.Max(0.0, zSurf - zBase);
        }

        public static string Format(IList<VolLevel> levels)
        {
            return Format(levels, null);
        }

        public static string Format(IList<VolLevel> levels, SectionConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Объёмы земляных работ (из домена)");
            if (levels == null) return sb.ToString();
            foreach (var lv in levels)
            {
                string t = FormatLevel(lv, cfg);
                if (string.IsNullOrEmpty(t)) continue;
                t = t.Replace("\\P", "\n").Replace("\\S^", "").Replace(";", "");
                sb.AppendLine(t);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>MTEXT полного расчёта, как pb-vt-format-text / pb-vt-entry-line.</summary>
        public static string FormatLevel(VolLevel lv, SectionConfig cfg)
        {
            if (lv == null || lv.Rows == null || lv.Rows.Count == 0) return null;
            if (lv.Kind == ProfileKind.Truba)
                return FormatPipeLen(lv.Rows);

            bool vert = VerticalWalls(cfg);
            if (lv.Kind == ProfileKind.Zasypka)
            {
                var sand = FilterFill(lv.Rows, BackfillKind.Sand);
                var soil = FilterFill(lv.Rows, BackfillKind.Soil);
                if (sand.Count > 0 || soil.Count > 0)
                {
                    var sb = new StringBuilder();
                    if (sand.Count > 0)
                    {
                        sb.Append(FormatBlock(sand, vert,
                            vert ? "V\\S^песка;=ΣS*B\\S^траншеи;:\\P"
                                 : "V\\S^песка;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P",
                            "Итого: {0} м³ - Обратная засыпка песком",
                            ProfileKind.Zasypka, cfg));
                    }
                    if (soil.Count > 0)
                    {
                        if (sb.Length > 0) sb.Append("\\P");
                        sb.Append(FormatBlock(soil, vert,
                            vert ? "V\\S^местного грунта;=ΣS*B\\S^траншеи;:\\P"
                                 : "V\\S^местного грунта;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P",
                            "Итого: {0} м³ - Обратная засыпка местным грунтом",
                            ProfileKind.Zasypka, cfg));
                    }
                    return sb.ToString();
                }
            }
            return FormatBlock(lv.Rows, vert, Header(lv.Kind, vert), Footer(lv.Kind), lv.Kind, cfg);
        }

        static List<VolRow> FilterFill(List<VolRow> rows, BackfillKind fill)
        {
            var list = new List<VolRow>();
            foreach (var r in rows)
            {
                if (r.Fill.HasValue && r.Fill.Value == fill)
                    list.Add(r);
            }
            return list;
        }

        static string Header(ProfileKind kind, bool vert)
        {
            if (vert)
            {
                switch (kind)
                {
                    case ProfileKind.Razrabotka: return "V\\S^разработки;=ΣS*B\\S^траншеи;:\\P";
                    case ProfileKind.Pesok: return "V\\S^основания;=ΣS*B\\S^траншеи;:\\P";
                    case ProfileKind.Zashita: return "V\\S^защитного слоя;=ΣS*B-V\\S^трубы;:\\P";
                    default: return "V\\S^засып;=ΣS*B\\S^тр;:\\P";
                }
            }
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "V\\S^разработки;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P";
                case ProfileKind.Pesok: return "V\\S^основания;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P";
                case ProfileKind.Zashita: return "V\\S^защитного слоя;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P";
                default: return "V\\S^засып;=Σ(S\\S^i;+S\\S^i+1;)/2·ΔL:\\P";
            }
        }

        static string Footer(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "Итого: {0} м³ - объем разработки грунта";
                case ProfileKind.Pesok: return "Итого: {0} м³ - объем песчаного основания";
                case ProfileKind.Zashita: return "Итого: {0} м³ - объем защитного слоя";
                default: return "Итого: {0} м³ - объем обратной засыпки";
            }
        }

        static string FormatPipeLen(List<VolRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("Общая длина участка трубопровода: ");
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("∅").Append(TableLayout.DiamMm(rows[i].Diam))
                  .Append(" - ").Append(R2s(rows[i].Length)).Append("м");
            }
            return sb.ToString();
        }

        static string FormatBlock(List<VolRow> rows, bool vert, string header, string footerFmt,
            ProfileKind kind, SectionConfig cfg)
        {
            var sb = new StringBuilder();
            sb.Append(header);
            double total = 0.0;
            foreach (var row in rows)
            {
                double shown;
                string line = FormatEntry(kind, row, cfg, out shown);
                if (string.IsNullOrEmpty(line)) continue;
                sb.Append(line).Append("\\P");
                total += shown;
            }
            sb.Append(string.Format(CultureInfo.InvariantCulture, footerFmt, R2s(total)));
            return sb.ToString();
        }

        public static string FormatEntry(ProfileKind kind, VolRow row, SectionConfig cfg, out double shownV)
        {
            string head = "на участке ∅" + TableLayout.DiamMm(row.Diam) + " L=" + R2s(row.Length) + "м";
            bool vert = VerticalWalls(cfg);
            if (vert)
            {
                double ad = R2n(row.Area);
                double bd = R2n(row.Width > 1e-9 ? row.Width
                    : (cfg != null ? cfg.BottomFull(row.Diam) : TrenchWidth.For(row.Diam, JointType.Socket)));
                if (ad <= 0.0)
                {
                    shownV = 0.0;
                    return null;
                }
                if (kind == ProfileKind.Zashita)
                {
                    double r = row.Diam * 0.5;
                    double pvd = row.PipeVol;
                    double fullCyl = Math.PI * r * r * row.Length;
                    double gross = ad * bd;
                    shownV = gross - R2n(pvd);
                    if (shownV < 0.0) shownV = 0.0;
                    if (shownV <= 0.0 || gross <= 1e-9)
                    {
                        shownV = 0.0;
                        return null;
                    }
                    bool full = fullCyl > 1e-12 && pvd >= fullCyl * 0.98
                        && Math.Abs(pvd - fullCyl) <= 0.02 * fullCyl;
                    if (full)
                    {
                        return head + ":\\P  V=" + R2s(ad) + "*" + R2s(bd)
                            + "-3,14*(" + R3s(r) + ")²*" + R2s(row.Length)
                            + "=" + R2s(shownV) + " м³";
                    }
                    return head + ":\\P  V=" + R2s(ad) + "*" + R2s(bd)
                        + "-" + R2s(pvd)
                        + "=" + R2s(shownV) + " м³";
                }
                shownV = ad * bd;
                return head + ": V=" + R2s(ad) + "*" + R2s(bd) + "=" + R2s(shownV) + " м³";
            }
            return FormatSlopedEntry(head, row, out shownV);
        }

        static string FormatSlopedEntry(string head, VolRow row, out double shownV)
        {
            if (row.Terms == null || row.Terms.Count == 0)
            {
                double ad = R2n(row.Area);
                double ld = R2n(row.Length);
                shownV = ad * ld;
                return head + ": V=" + R2s(ad) + "*" + R2s(ld) + "=" + R2s(shownV) + " м³";
            }
            var parts = new List<string>();
            double sum = 0.0;
            foreach (var t in row.Terms)
            {
                if (t.DL <= 1e-9) continue;
                double a0 = R2n(t.A0);
                double a1 = R2n(t.A1);
                double dl = R2n(t.DL);
                if (a0 <= 0.0 && a1 <= 0.0) continue;
                sum += t.Average ? 0.5 * (a0 + a1) * dl : a0 * dl;
                if (t.Average)
                    parts.Add("(" + R2s(a0) + "+" + R2s(a1) + ")/2*" + R2s(dl));
                else
                    parts.Add(R2s(a0) + "*" + R2s(dl));
            }
            shownV = sum;
            if (parts.Count == 0)
            {
                shownV = 0.0;
                return null;
            }
            var sb = new StringBuilder();
            sb.Append(head).Append(":\\P  V=");
            int lineLen = 2;
            for (int i = 0; i < parts.Count; i++)
            {
                string p = (i == 0 ? "" : "+") + parts[i];
                if (i > 0 && lineLen + p.Length > 70)
                {
                    sb.Append("\\P  ");
                    lineLen = 2;
                    p = "+" + parts[i];
                }
                sb.Append(p);
                lineLen += p.Length;
            }
            sb.Append("=").Append(R2s(shownV)).Append(" м³");
            return sb.ToString();
        }

        static double R2n(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
        static string R2s(double v) => R2n(v).ToString("0.00", CultureInfo.InvariantCulture);
        static string R3s(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero)
            .ToString("0.000", CultureInfo.InvariantCulture);
    }
}
