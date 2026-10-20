using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Лист продольного профиля сети: графика + таблица как PipeBuild/PoperSwift.</summary>
    static class ProfileSheet
    {
        // Нарисованные линии: проект (без рандома) и то, что реально легло на лист (с рандомом, если включён).
        // Ключ: "kind:src". Заполняются в DrawBody/DrawL1Body, читаются в DrawTable.
        static readonly Dictionary<string, List<Point2d>> _drawnPts = new Dictionary<string, List<Point2d>>();
        static readonly Dictionary<string, List<Point2d>> _drawnProjPts = new Dictionary<string, List<Point2d>>();
        static readonly Dictionary<string, List<Point2d>> _drawnFactPts = new Dictionary<string, List<Point2d>>();
        static readonly Dictionary<ProfileKind, Point3d> _sheetOrigin = new Dictionary<ProfileKind, Point3d>();
        const string LayerTbl = "NVK-TBL";
        static ProfileKind _liveKind;
        static HeightSrc? _liveSrc;
        static Point3d _liveO;
        static double _liveLSc, _liveHSc, _liveOsh;
        static string _liveSeg;

        public static double DrawAll(Database db, Transaction tr, BlockTableRecord ms,
            DomainResult last, Point3d origin, double lSc, double hSc, bool asBlock,
            int runIndex = 0, int markOffset = 0)
        {
            _drawnPts.Clear();
            _drawnProjPts.Clear();
            _drawnFactPts.Clear();
            _sheetOrigin.Clear();
            var cfg = last.Cfg ?? new SectionConfig();
            double osh = NvkUiState.OshOverride.HasValue
                ? NvkUiState.OshOverride.Value
                : AxisBuilder.Osh(last.Axis, cfg);
            double zMax = osh + 1;
            foreach (var p in last.Axis)
            {
                double z = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
                if (z > zMax) zMax = z;
            }
            var tbl = NvkSession.Tbl();
            double th = tbl.TextH > 0 ? tbl.TextH : 2.5;
            double sheetH = (zMax - osh) * hSc + TableH(ProfileKind.Razrabotka, tbl) + 20.0;
            if (sheetH < 80) sheetH = 80;

            var kinds = new[]
            {
                ProfileKind.Razrabotka, ProfileKind.Pesok, ProfileKind.Truba,
                ProfileKind.Zashita, ProfileKind.Zasypka
            };
            int n = 0;
            int row = 0;
            var drawn = new List<ProfileKind>();
            foreach (var kind in kinds)
            {
                if (kind == ProfileKind.Zasypka && !HasBackfill(last.Axis, cfg)) continue;
                if (!WantKind(kind)) continue;
                Point3d ins;
                if (!(runIndex == 0 && NvkSession.TryEp(kind, out ins)))
                    ins = new Point3d(origin.X, origin.Y - row * sheetH, 0.0);
                DrawOne(db, tr, ms, last, ins, lSc, hSc, osh, kind, th, markOffset);
                drawn.Add(kind);
                n++;
                row++;
            }
            NvkSession.LastEp = new EpLayout
            {
                Origin = origin, LScale = lSc, HScale = hSc, Osh = osh,
                RowGap = sheetH, Kinds = drawn
            };
            return sheetH;
        }

        static bool WantKind(ProfileKind kind)
        {
            return true;
        }

        static bool HasBackfill(IList<AxisPt> axis, SectionConfig cfg)
        {
            foreach (var p in axis)
                if (p.NamedZ(NamedLine.Surface, cfg) > p.NamedZ(NamedLine.ZashTop, cfg) + 1e-4)
                    return true;
            return false;
        }

        static double TableH(ProfileKind kind, TableCfg tbl)
        {
            var rows = ProfileRows.For(kind, tbl);
            int nh = 0, nl = 3, ns = 0;
            foreach (var r in rows)
            {
                if (!r.Show) continue;
                if (r.Kind == SheetRowKind.Height) nh = 6;
                if (r.Kind == SheetRowKind.Length) nl = 4;
                if (r.Kind == SheetRowKind.Slope) ns++;
            }
            double h = 0;
            foreach (var r in rows)
            {
                if (!r.Show) continue;
                if (r.Kind == SheetRowKind.Height)
                    h += TableLayout.RowHeight(TableRowKind.Height, tbl.TextH, nh < 4 ? 6 : nh, nl);
                else if (r.Kind == SheetRowKind.Length)
                    h += TableLayout.RowHeight(TableRowKind.Length, tbl.TextH, 6, nl);
                else if (r.Kind == SheetRowKind.Slope)
                    h += TableLayout.RowHeight(TableRowKind.Slope, tbl.TextH, 6, nl);
                else
                    h += tbl.TextH * 3.0;
            }
            return h + tbl.TextH;
        }

        /// <summary>Зазор таблицы: поле L1 «Таблица ниже низа, м».</summary>
        static double TableGapM(TableCfg tbl)
        {
            double off = NvkUiState.TableOffsetM;
            if (NvkSession.L1 != null)
                off = NvkSession.L1.TableOffsetM;
            return TableLayout.TableBelowM(off);
        }

        public static void DrawOne(Database db, Transaction tr, BlockTableRecord ms,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, double th, int markOffset = 0)
        {
            string layer = LayerOf(kind);
            EnsureLayer(db, tr, layer, ColorOf(kind));
            EnsureLayer(db, tr, "NVK-EP-VIS", 7);
            EnsureLayer(db, tr, LayerTbl, 7);
            DrawOneContent(db, tr, ms, last, o, lSc, hSc, osh, kind, th, markOffset);
        }

        static void DrawOneContent(Database db, Transaction tr, BlockTableRecord ms,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, double th, int markOffset = 0)
        {
            string layer = LayerOf(kind);
            _sheetOrigin[kind] = o;
            _liveKind = kind;
            _liveO = o;
            _liveLSc = lSc;
            _liveHSc = hSc;
            _liveOsh = osh;
            _liveSeg = last != null ? last.SegId : null;
            _liveSrc = null;
            var cfg = last.Cfg ?? new SectionConfig();
            var axis = last.Axis;
            double L = last.Chain.Total;
            double x0 = o.X, x1 = o.X + L * lSc, y0 = o.Y;

            if (kind == ProfileKind.Razrabotka)
            {
                var l1 = NvkSession.L1 ?? L1Style.Default();
                DrawL1Body(ms, tr, layer, last, o, lSc, hSc, osh, cfg, l1, l1.ShowManholes);
                if (l1.ShowManholes)
                    DrawManholes(ms, tr, layer, last, o, lSc, hSc, osh, cfg, kind);
            }
            else
            {
                var sh = NvkSession.Sheet(kind);
                bool gapOther = sh == null || sh.ShowManholes;
                DrawBody(ms, tr, layer, last, o, lSc, hSc, osh, kind, cfg, sh, gapOther);
                if (gapOther)
                    DrawManholes(ms, tr, layer, last, o, lSc, hSc, osh, cfg, kind);
                var cover = PtsFor(HeightSrc.RoadTop, last, cfg, o, lSc, hSc, osh);
                StylePline(ms, tr, layer, cover, sh != null && sh.Cover != null ? sh.Cover : LineStyle.Rgb(255, 0, 0, "DASHED", 18));
                LabelPts(ms, tr, layer, cover, CsAnnot.LineTag(HeightSrc.RoadTop), th);
                Remember(kind, HeightSrc.RoadTop, cover, cover);
            }
            var tbl = NvkSession.Tbl();
            double annGapM = TableGapM(tbl);
            double tableTop = y0 - annGapM * hSc;
            double ticksBot = tableTop;
            // oshDisplay: legend shows effective datum shifted down by the gap
            double oshDisplay = annGapM > 1e-9 ? osh - annGapM : osh;
            DrawTicks(ms, tr, layer, x0, y0, osh, hSc, axis, cfg, th, ticksBot);
            DrawTitle(ms, tr, layer, last, o, L * lSc, hSc, osh, cfg, th, kind);
            DrawSectionMarks(ms, tr, last, o, lSc, hSc, osh, th, ticksBot, markOffset, kind);
            DrawTable(ms, tr, LayerTbl, last, o, lSc, hSc, osh, kind, cfg, th, (double?)tableTop, oshDisplay);
            if (tbl.DrawProjections)
                DrawProjections(ms, tr, layer, last, o, lSc, hSc, osh, cfg, th, tbl);
            RecordSheetWin(last, o, lSc, hSc, osh, kind, cfg, th);
        }

        static void RecordSheetWin(DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg, double th)
        {
            if (last == null || last.Chain == null) return;
            var tbl = NvkSession.Tbl();
            var spec = ProfileRows.For(kind, tbl);
            var labels = new List<string>();
            foreach (var r in spec)
                if (r.Show) labels.Add(r.Label);
            double labW = TableLayout.LabelColW(tbl, labels);
            double L = last.Chain.Total;
            double zMax = osh + 1.0;
            if (last.Axis != null)
            {
                foreach (var p in last.Axis)
                {
                    double z = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
                    if (z > zMax) zMax = z;
                }
            }
            double tableH = TableH(kind, tbl);
            double annGapM = TableGapM(tbl);
            double pad = th * 3.0;
            double x0 = o.X - labW - th - pad;
            double x1 = o.X + L * lSc + pad;
            double y1 = o.Y + (zMax - osh) * hSc + 4.0 * th;
            double y0 = o.Y - annGapM * hSc - tableH - pad;
            NvkSession.AddEpWin(kind, x0, y0, x1, y1);
        }

        static List<Point2d> DrawL1Body(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh, SectionConfig cfg, L1Style st,
            bool gapAtMh)
        {
            var road = SurfacePts(last.Axis, cfg, o, lSc, hSc, osh, HeightSrc.RoadTop);
            var earth = EarthPts(last.Axis, cfg, o, lSc, hSc, osh);
            var bot = TrenchBottomPts(last.Chain, cfg, o, lSc, hSc, osh);
            var botProj = bot;
            double ampL1 = RowScatter.Amp(HeightSrc.TrenchBottom);
            if (ampL1 > 0 && RowScatter.Ready(last, HeightSrc.TrenchBottom))
            {
                bot = OffsetScatter(bot, last, o, lSc, hSc, ampL1, HeightSrc.TrenchBottom);
                var sand = PtsFor(HeightSrc.SandTop, last, cfg, o, lSc, hSc, osh);
                if (sand.Count >= 2)
                    bot = ClipPtsBelow(bot, sand);
                RememberFact(ProfileKind.Razrabotka, HeightSrc.TrenchBottom, bot);
            }
            // L6 не ниже L1 (вершина на пересечении).
            if (earth.Count >= 2 && bot.Count >= 2)
                earth = ClipPtsAbove(earth, bot);
            var hatchTop = earth;

            if (hatchTop.Count >= 2 && bot.Count >= 2)
            {
                var hx = HatchPat.ForKind(ProfileKind.Razrabotka, st, null)
                    ?? FillStyle.Rgb("ANSI31", 0, 0, 255, 1);
                double hxSc = HatchPat.Scale(hx);
                if (gapAtMh)
                    HatchBandGapped(ms, tr, layer, hatchTop, bot, last, o, lSc, hx.Pattern, hxSc, CadColor.Of(hx));
                else
                    Hatch(ms, tr, layer, Band(hatchTop, bot), hx.Pattern, hxSc, CadColor.Of(hx));
            }
            if (gapAtMh)
            {
                Live(HeightSrc.BlackEarth, () => DrawGappedStyled(ms, tr, layer, earth, last, o, lSc, st.Earth));
                Live(HeightSrc.TrenchBottom, () => DrawGappedStyled(ms, tr, layer, bot, last, o, lSc, st.Bottom));
                Live(HeightSrc.RoadTop, () => DrawGappedStyled(ms, tr, layer, road, last, o, lSc, st.Road));
            }
            else
            {
                Live(HeightSrc.BlackEarth, () => StylePline(ms, tr, layer, earth, st.Earth));
                Live(HeightSrc.TrenchBottom, () => StylePline(ms, tr, layer, bot, st.Bottom));
                Live(HeightSrc.RoadTop, () => StylePline(ms, tr, layer, road, st.Road));
            }
            double thLab = NvkSession.Tbl().TextH;
            LabelPts(ms, tr, layer, road, CsAnnot.LineTag(HeightSrc.RoadTop), thLab);
            LabelPts(ms, tr, layer, earth, CsAnnot.LineTag(HeightSrc.BlackEarth), thLab);
            LabelPts(ms, tr, layer, bot, CsAnnot.LineTag(HeightSrc.TrenchBottom), thLab);
            Remember(ProfileKind.Razrabotka, HeightSrc.RoadTop, road, road);
            Remember(ProfileKind.Razrabotka, HeightSrc.BlackEarth, earth, earth);
            Remember(ProfileKind.Razrabotka, HeightSrc.TrenchBottom, botProj, bot);
            DrawZoneCoverage(ms, tr, layer, last, o, lSc, hSc, osh, cfg, road);
            return bot;
        }

        static void DrawZoneCoverage(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh, SectionConfig cfg, List<Point2d> road)
        {
            if (!NvkUiState.ShowLineTags) return;
            if (cfg == null || cfg.Zones == null || cfg.Zones.Count == 0 || road == null || road.Count < 2)
                return;
            foreach (var z in cfg.Zones)
            {
                double x0 = o.X + z.Ch0 * lSc;
                double x1 = o.X + z.Ch1 * lSc;
                var clip = ClipPts(road, x0, x1);
                if (clip.Count < 2)
                {
                    double z0 = ProfileRows.ZAt(HeightSrc.RoadTop, AtCh(last.Axis, z.Ch0), cfg);
                    double z1 = ProfileRows.ZAt(HeightSrc.RoadTop, AtCh(last.Axis, z.Ch1), cfg);
                    clip = new List<Point2d> { XY(o, lSc, hSc, osh, z.Ch0, z0), XY(o, lSc, hSc, osh, z.Ch1, z1) };
                }
                short aci = 1;
                if (z.Type == "GAZN") aci = 3;
                else if (z.Type == "TROT") aci = 2;
                else if (z.Type == "OTHER") aci = 6;
                AddPline(ms, tr, layer, clip, Aci(aci), false, 50, "Continuous");
            }
        }

        static bool PavementGap(IList<AxisPt> axis, SectionConfig cfg)
        {
            if (axis == null) return false;
            foreach (var p in axis)
            {
                double r = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
                double e = ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
                if (r - e > 1e-3) return true;
            }
            return false;
        }

        // Сырая L6 (TIN−одежда). Клип к L1 — ClipPtsAbove, иначе излом на пикете, не на пересечении.
        static List<Point2d> EarthPts(IList<AxisPt> axis, SectionConfig cfg, Point3d o, double lSc, double hSc, double osh)
        {
            var pts = new List<Point2d>();
            if (axis == null) return pts;
            double prevCh = -1e9;
            double prevZ = double.NaN;

            foreach (var p in axis)
            {
                double z = p.EarthZ.HasValue
                    ? p.EarthZ.Value
                    : ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
                bool sameCh = !double.IsNaN(prevCh) && Math.Abs(p.Ch - prevCh) < 1e-6;

                if (sameCh && !double.IsNaN(prevZ) && Math.Abs(z - prevZ) > 1e-4)
                    pts.Add(XY(o, lSc, hSc, osh, p.Ch, z));
                else if (!sameCh || pts.Count == 0)
                    pts.Add(XY(o, lSc, hSc, osh, p.Ch, z));
                else if (Math.Abs(z - prevZ) > 1e-4)
                {
                    pts.Add(XY(o, lSc, hSc, osh, p.Ch, prevZ));
                    pts.Add(XY(o, lSc, hSc, osh, p.Ch, z));
                }

                prevZ = z;
                prevCh = p.Ch;
            }
            return pts;
        }

        static double AvgY(IList<Point2d> pts)
        {
            if (pts == null || pts.Count == 0) return 0;
            double s = 0;
            for (int i = 0; i < pts.Count; i++) s += pts[i].Y;
            return s / pts.Count;
        }

        static List<Point2d> ClipPtsAbove(List<Point2d> pts1, List<Point2d> pts2)
        {
            return ClipPtsBound(pts1, pts2, true);
        }

        static List<Point2d> ClipPtsBelow(List<Point2d> pts1, List<Point2d> pts2, bool insertBound = true)
        {
            return ClipPtsBound(pts1, pts2, false, insertBound);
        }

        static List<Point2d> ClipPtsBound(List<Point2d> pts1, List<Point2d> pts2, bool above, bool insertBound = true)
        {
            var a = new List<PolylineClip.Pt>(pts1.Count);
            var b = new List<PolylineClip.Pt>(pts2.Count);
            foreach (var p in pts1) a.Add(new PolylineClip.Pt(p.X, p.Y));
            foreach (var p in pts2) b.Add(new PolylineClip.Pt(p.X, p.Y));
            var c = above ? PolylineClip.KeepAbove(a, b) : PolylineClip.KeepBelow(a, b, insertBound);
            var r = new List<Point2d>(c.Count);
            foreach (var p in c) r.Add(new Point2d(p.X, p.Y));
            return r;
        }

        static Point2d[] Band(List<Point2d> top, List<Point2d> bot)
        {
            var a = new List<Point2d>(top.Count + bot.Count);
            a.AddRange(top);
            for (int i = bot.Count - 1; i >= 0; i--)
                a.Add(bot[i]);
            return a.ToArray();
        }

        static void Live(HeightSrc src, Action a)
        {
            var prev = _liveSrc;
            _liveSrc = src;
            try { if (a != null) a(); }
            finally { _liveSrc = prev; }
        }

        static void StylePline(BlockTableRecord ms, Transaction tr, string layer, IList<Point2d> pts, LineStyle st)
        {
            if (st == null) st = LineStyle.Of(7, "Continuous", 30);
            AddPline(ms, tr, layer, pts, CadColor.Of(st), false, st.Weight, st.Linetype);
        }

        static void DrawGappedStyled(BlockTableRecord ms, Transaction tr, string layer,
            List<Point2d> pts, DomainResult last, Point3d o, double lSc, LineStyle st)
        {
            if (st == null) st = LineStyle.Of(7, "Continuous", 30);
            double ch = 0.0;
            foreach (var sg in last.Chain.Segs)
            {
                double x1 = o.X + (ch + GapR(sg, true)) * lSc;
                double x2 = o.X + (ch + sg.Length2d - GapR(sg, false)) * lSc;
                if (x2 > x1 + 1e-3)
                {
                    var clip = ClipPts(pts, x1, x2);
                    if (clip.Count >= 2) StylePline(ms, tr, layer, clip, st);
                }
                ch += sg.Length2d;
            }
        }

        static void HatchBandGapped(BlockTableRecord ms, Transaction tr, string layer,
            List<Point2d> top, List<Point2d> bot, DomainResult last, Point3d o, double lSc,
            string pat, double scale, AcadColor col)
        {
            if (top == null || top.Count < 2 || bot == null || bot.Count < 2) return;
            double ch = 0.0;
            foreach (var sg in last.Chain.Segs)
            {
                double x1 = o.X + (ch + GapR(sg, true)) * lSc;
                double x2 = o.X + (ch + sg.Length2d - GapR(sg, false)) * lSc;
                if (x2 > x1 + 1e-3)
                {
                    var topClip = ClipPts(top, x1, x2);
                    var botClip = ClipPts(bot, x1, x2);
                    if (topClip.Count >= 2 && botClip.Count >= 2)
                        Hatch(ms, tr, layer, Band(topClip, botClip), pat, scale, col);
                }
                ch += sg.Length2d;
            }
        }

        static List<Point2d> OffsetScatter(List<Point2d> pts, DomainResult last, Point3d o,
            double lSc, double hSc, double amp, HeightSrc src)
        {
            var r = new List<Point2d>(pts.Count);
            double total = last.Chain != null ? last.Chain.Total : 1.0;
            var vals = RowScatter.Vals(last, src);
            foreach (var p in pts)
            {
                double ch = lSc > 1e-9 ? (p.X - o.X) / lSc : 0.0;
                double dz = Scatter.Meters(ch, total, vals, amp);
                r.Add(new Point2d(p.X, p.Y + dz * hSc));
            }
            return r;
        }

        static List<Point2d> ReclampScatter(ProfileKind kind, List<Point2d> fact,
            List<Point2d> topPts, List<Point2d> botPts, DomainResult last, SectionConfig cfg,
            Point3d o, double lSc, double hSc, double osh)
        {
            if (fact == null || fact.Count < 2) return fact;
            if (kind == ProfileKind.Pesok || kind == ProfileKind.Razrabotka)
            {
                if (topPts != null && topPts.Count >= 2)
                    fact = ClipPtsBelow(fact, topPts);
            }
            else if (kind == ProfileKind.Zashita)
            {
                var earth = EarthPts(last.Axis, cfg, o, lSc, hSc, osh);
                fact = ClampZashPolyline(fact, botPts, earth);
            }
            else if (kind == ProfileKind.Zasypka)
            {
                if (botPts != null && botPts.Count >= 2)
                    fact = ClipPtsAbove(fact, botPts);
            }
            return fact;
        }

        /// <summary>L4 ≥ L2. Потолок — max(L6, L2): если L6 &lt; L2, L4 лежит на L2.</summary>
        static List<Point2d> ClampZashPolyline(List<Point2d> zash, List<Point2d> sand, List<Point2d> earth)
        {
            if (zash == null || zash.Count < 2) return zash;
            if (sand != null && sand.Count >= 2)
                zash = ClipPtsAbove(zash, sand);
            if (earth != null && earth.Count >= 2)
            {
                var ceil = (sand != null && sand.Count >= 2) ? ClipPtsAbove(earth, sand) : earth;
                zash = ClipPtsBelow(zash, ceil);
            }
            return zash;
        }

        static List<Point2d> RelocatePts(List<Point2d> pts, ProfileKind fromKind, Point3d toO)
        {
            if (pts == null) return null;
            Point3d fromO;
            if (!_sheetOrigin.TryGetValue(fromKind, out fromO))
                return new List<Point2d>(pts);
            double dx = toO.X - fromO.X;
            double dy = toO.Y - fromO.Y;
            var r = new List<Point2d>(pts.Count);
            if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
            {
                r.AddRange(pts);
                return r;
            }
            foreach (var p in pts)
                r.Add(new Point2d(p.X + dx, p.Y + dy));
            return r;
        }

        /// <summary>
        /// Общая кромка слоёв: верх песка = низ защиты, дно разработки = низ песка,
        /// верх защиты = низ засыпки. Берём уже нарисованную линию листа-хозяина.
        /// </summary>
        static bool TryAdopt(ProfileKind kind, HeightSrc src, Point3d o,
            out List<Point2d> drawn, out List<Point2d> proj, out List<Point2d> fact)
        {
            drawn = proj = fact = null;
            var owner = Scatter.SeriesOf(src);
            if (owner == kind) return false;
            var d = GetDrawn(owner, src);
            if (d == null || d.Count < 2) return false;
            drawn = RelocatePts(d, owner, o);
            var p = GetProj(owner, src);
            proj = p != null ? RelocatePts(p, owner, o) : drawn;
            List<Point2d> f;
            if (_drawnFactPts.TryGetValue(LineKey(owner, src), out f) && f != null && f.Count >= 2)
                fact = RelocatePts(f, owner, o);
            return true;
        }

        static void DrawProjections(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            SectionConfig cfg, double th, TableCfg tbl)
        {
            // projection lines at raw manhole centres (0, L1, ..., Total)
            var mhPositions = new System.Collections.Generic.List<double>();
            double mhCh0 = 0.0;
            for (int mi = 0; mi <= last.Chain.Segs.Count; mi++)
            {
                mhPositions.Add(mhCh0);
                if (mi < last.Chain.Segs.Count) mhCh0 += last.Chain.Segs[mi].Length2d;
            }
            double yBot = o.Y - TableGapM(tbl) * hSc;
            short col = tbl.ProjectionColor == 256 ? (short)8 : tbl.ProjectionColor;
            foreach (var ch in mhPositions)
            {
                var ap = AtCh(last.Axis, ch);
                double z = ProfileRows.ZAt(HeightSrc.RoadTop, ap, cfg);
                double x = o.X + ch * lSc;
                double yTop = o.Y + (z - osh) * hSc;
                AddLine(ms, tr, layer, x, yTop, x, yBot, Aci(col), 0);
            }
        }

        static void DrawTicks(BlockTableRecord ms, Transaction tr, string layer,
            double x0, double y0, double osh, double hSc, IList<AxisPt> axis, SectionConfig cfg, double th,
            double? tableBottom = null)
        {
            double zMax = osh;
            foreach (var p in axis)
            {
                double z = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
                if (z > zMax) zMax = z;
            }
            double xL = x0 - 2.0 * th;
            double yStart = tableBottom ?? y0;
            AddLine(ms, tr, layer, xL, yStart, xL, y0 + (zMax - osh) * hSc + th, Aci(7), 0);
            // Ticks fill the full scale line, skipping OSH and oshDisplay (both shown as legend labels).
            double zFrom = tableBottom.HasValue ? osh - (y0 - tableBottom.Value) / hSc : osh;
            double tickZ = Math.Ceiling(zFrom);
            while (tickZ <= zMax + 1e-6)
            {
                // Skip only the oshDisplay level (shown in legend). When annGap=0 this equals osh.
                if (Math.Abs(tickZ - zFrom) > 1e-9)
                {
                    double y = y0 + (tickZ - osh) * hSc;
                    AddLine(ms, tr, layer, xL - 0.25 * th, y, xL + 0.25 * th, y, Aci(3), 0);
                    NvkText.Make(ms, tr, layer, xL - 0.4 * th, y, TableLayout.Rtos(tickZ, 0),
                        th, AttachmentPoint.MiddleRight, 0.0, 6 * th, false);
                }
                tickZ += 1.0;
            }
        }

        static void DrawBody(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg)
        {
            var sh = NvkSession.Sheet(kind);
            DrawBody(ms, tr, layer, last, o, lSc, hSc, osh, kind, cfg, sh, sh == null || sh.ShowManholes);
        }

        static void DrawBody(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg, SheetStyle sh, bool gapAtMh)
        {
            HeightSrc topSrc, botSrc;
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    topSrc = HeightSrc.Surface; botSrc = HeightSrc.TrenchBottom; break;
                case ProfileKind.Pesok:
                    topSrc = HeightSrc.SandTop; botSrc = HeightSrc.TrenchBottom; break;
                case ProfileKind.Truba:
                    topSrc = HeightSrc.Invert; botSrc = HeightSrc.Invert; break;
                case ProfileKind.Zashita:
                    topSrc = HeightSrc.ZashTop; botSrc = HeightSrc.SandTop; break;
                default:
                    topSrc = HeightSrc.Surface; botSrc = HeightSrc.ZashTop; break;
            }
            var hx = HatchPat.ForKind(kind, NvkSession.L1, sh);
            string pat = hx != null ? hx.Pattern : null;

            var topPts = PtsFor(topSrc, last, cfg, o, lSc, hSc, osh);
            var botPts = PtsFor(botSrc, last, cfg, o, lSc, hSc, osh);
            List<Point2d> topAdoptFact = null, botAdoptFact = null;
            List<Point2d> topAdoptProj = null, botAdoptProj = null;
            List<Point2d> topAd = null, botAd = null;
            bool topAdopted = TryAdopt(kind, topSrc, o, out topAd, out topAdoptProj, out topAdoptFact);
            bool botAdopted = botSrc != topSrc
                && TryAdopt(kind, botSrc, o, out botAd, out botAdoptProj, out botAdoptFact);
            if (topAdopted) topPts = topAd;
            if (botAdopted) botPts = botAd;
            if (kind == ProfileKind.Zasypka && topPts.Count >= 2 && botPts.Count >= 2)
                topPts = ClipPtsAbove(topPts, botPts);
            var topProj = topAdopted && topAdoptProj != null ? topAdoptProj : topPts;
            var botProj = botAdopted && botAdoptProj != null ? botAdoptProj : botPts;
            if (topAdopted && topAdoptFact != null)
                RememberFact(kind, topSrc, topAdoptFact);
            if (botAdopted && botAdoptFact != null)
                RememberFact(kind, botSrc, botAdoptFact);
            double ampTop = RowScatter.Amp(topSrc);
            double ampBot = botSrc != topSrc ? RowScatter.Amp(botSrc) : 0.0;
            bool doScTop = !topAdopted && ampTop > 0.0 && CanScatter(topSrc) && RowScatter.Ready(last, topSrc);
            bool doScBot = !botAdopted && ampBot > 0.0 && CanScatter(botSrc) && botSrc != topSrc && RowScatter.Ready(last, botSrc);
            if (doScTop || doScBot)
            {
                List<Point2d> topFact = topPts;
                List<Point2d> botFact = botPts;
                if (doScTop)
                {
                    var srcTop = topPts;
                    // Zashita: scatter on raw (pre-clip) pipe points so boundary clip points don't pin scatter to zero
                    if (kind == ProfileKind.Zashita)
                        srcTop = PipePts(last.Chain, HeightSrc.ZashTop, cfg, o, lSc, hSc, osh, last.Axis);
                    topFact = OffsetScatter(srcTop, last, o, lSc, hSc, ampTop, topSrc);
                }
                if (doScBot)
                    botFact = OffsetScatter(botPts, last, o, lSc, hSc, ampBot, botSrc);
                if (kind == ProfileKind.Pesok || kind == ProfileKind.Razrabotka)
                {
                    if (botFact != botPts && topFact != null && topFact.Count >= 2)
                        botFact = ClipPtsBelow(botFact, topFact);
                    if (topFact != topPts && botFact != null && botFact.Count >= 2)
                        topFact = ClipPtsAbove(topFact, botFact);
                }
                else
                {
                    if (doScTop)
                        topFact = ReclampScatter(kind, topFact, topPts, botPts, last, cfg, o, lSc, hSc, osh);
                }
                if (topFact != topPts)
                    RememberFact(kind, topSrc, topFact);
                if (botSrc != topSrc && botFact != botPts)
                    RememberFact(kind, botSrc, botFact);
                if (cfg.SingleScatter)
                {
                    topPts = topFact;
                    botPts = botFact;
                }
                else if (cfg.ProjFact)
                {
                    if (topFact != topPts)
                        Live(topSrc, () => AddPline(ms, tr, layer, topFact, Aci(6), false, 18));
                    if (botSrc != topSrc && botFact != botPts)
                        Live(botSrc, () => AddPline(ms, tr, layer, botFact, Aci(6), false, 18));
                }
            }
            else if (cfg.ProjFact && !cfg.SingleScatter)
            {
                if (topAdopted && topAdoptFact != null && topAdoptFact != topPts)
                    AddPline(ms, tr, layer, topAdoptFact, Aci(6), false, 18);
                if (botAdopted && botAdoptFact != null && botAdoptFact != botPts)
                    AddPline(ms, tr, layer, botAdoptFact, Aci(6), false, 18);
            }

            if (kind == ProfileKind.Zasypka)
                HatchBackfill(ms, tr, layer, sh, cfg, last, o, lSc, topPts, botPts, gapAtMh);
            else if (pat != null && topPts.Count >= 2 && botPts.Count >= 2
                && AvgY(topPts) > AvgY(botPts) + 1e-4)
            {
                var col = hx != null ? CadColor.Of(hx) : Aci(5);
                double sc = HatchPat.Scale(hx);
                if (kind == ProfileKind.Zashita)
                {
                    if (gapAtMh)
                        HatchPipesInner(ms, tr, last, o, lSc, topPts, botPts, pat, layer, col, sc);
                    else
                        Hatch(ms, tr, layer, Band(topPts, botPts), pat, sc, col);
                }
                else
                {
                    if (gapAtMh)
                        HatchDiamGroups(ms, tr, last, o, lSc, topPts, botPts, pat, layer, col, sc);
                    else
                        Hatch(ms, tr, layer, Band(topPts, botPts), pat, sc, col);
                }
            }

            foreach (var row in ProfileRows.For(kind, NvkSession.Tbl()))
            {
                if (!row.Show || row.Kind != SheetRowKind.Height) continue;
                if (row.Src == HeightSrc.DeltaZash) continue;
                if (row.Src == topSrc || row.Src == botSrc) continue;
                if (row.Src == HeightSrc.RoadTop) continue;
                var extra = PtsFor(row.Src, last, cfg, o, lSc, hSc, osh);
                var extraProj = extra;
                List<Point2d> extraAdoptFact;
                if (TryAdopt(kind, row.Src, o, out var extraAd, out var extraAdProj, out extraAdoptFact))
                {
                    extra = extraAd;
                    extraProj = extraAdProj != null ? extraAdProj : extraAd;
                    if (extraAdoptFact != null)
                    {
                        RememberFact(kind, row.Src, extraAdoptFact);
                        if (cfg.ProjFact && !cfg.SingleScatter)
                            AddPline(ms, tr, layer, extraAdoptFact, Aci(6), false, 18);
                    }
                }
                else
                {
                    double extraAmp = RowScatter.Amp(row.Src);
                    if (extraAmp > 0.0 && CanScatter(row.Src) && extra.Count >= 2 && RowScatter.Ready(last, row.Src))
                    {
                        var extraFact = OffsetScatter(extra, last, o, lSc, hSc, extraAmp, row.Src);
                        RememberFact(kind, row.Src, extraFact);
                        if (cfg.SingleScatter)
                            extra = extraFact;
                        else if (cfg.ProjFact)
                            AddPline(ms, tr, layer, extraFact, Aci(6), false, 18);
                    }
                }
                Live(row.Src, () => AddPline(ms, tr, layer, extra, Aci(ColorOfSrc(row.Src)), false, 18));
                LabelPts(ms, tr, layer, extra, CsAnnot.LineTag(row.Src), NvkSession.Tbl().TextH);
                Remember(kind, row.Src, extraProj, extra);
            }

            if (kind == ProfileKind.Truba)
                Live(topSrc, () => DrawVisGapped(ms, tr, layer, last, o, lSc, hSc, osh, topPts, botPts, 1, 1, true));
            else
            {
                var topSt = sh != null && sh.Top != null ? sh.Top : LineStyle.Of(1, "Continuous", 30);
                var botSt = sh != null && sh.Bot != null ? sh.Bot : LineStyle.Of(5, "Continuous", 30);
                if (gapAtMh)
                {
                    Live(topSrc, () => DrawGappedStyled(ms, tr, layer, topPts, last, o, lSc, topSt));
                    Live(botSrc, () => DrawGappedStyled(ms, tr, layer, botPts, last, o, lSc, botSt));
                }
                else
                {
                    Live(topSrc, () => StylePline(ms, tr, layer, topPts, topSt));
                    Live(botSrc, () => StylePline(ms, tr, layer, botPts, botSt));
                }
            }
            if (kind == ProfileKind.Truba && sh != null && sh.Top != null)
            {
                if (gapAtMh)
                    Live(topSrc, () => DrawGappedStyled(ms, tr, layer, topPts, last, o, lSc, sh.Top));
                else
                    Live(topSrc, () => StylePline(ms, tr, layer, topPts, sh.Top));
            }
            double thLab = NvkSession.Tbl().TextH;
            LabelPts(ms, tr, layer, topPts, CsAnnot.LineTag(topSrc), thLab);
            if (botSrc != topSrc)
                LabelPts(ms, tr, layer, botPts, CsAnnot.LineTag(botSrc), thLab);
            Remember(kind, topSrc, topProj, topPts);
            if (botSrc != topSrc)
                Remember(kind, botSrc, botProj, botPts);
        }

        static string LineKey(ProfileKind kind, HeightSrc src)
        {
            return ((int)kind).ToString() + ":" + ((int)src).ToString();
        }

        static void Remember(ProfileKind kind, HeightSrc src, List<Point2d> proj, List<Point2d> drawn)
        {
            string k = LineKey(kind, src);
            if (proj != null && proj.Count >= 2) _drawnProjPts[k] = proj;
            if (drawn != null && drawn.Count >= 2) _drawnPts[k] = drawn;
        }

        static void RememberFact(ProfileKind kind, HeightSrc src, List<Point2d> fact)
        {
            if (fact != null && fact.Count >= 2)
                _drawnFactPts[LineKey(kind, src)] = fact;
        }

        static List<Point2d> GetDrawn(ProfileKind kind, HeightSrc src)
        {
            List<Point2d> p;
            if (_drawnPts.TryGetValue(LineKey(kind, src), out p) && p != null && p.Count >= 2)
                return p;
            if (_drawnProjPts.TryGetValue(LineKey(kind, src), out p) && p != null && p.Count >= 2)
                return p;
            return null;
        }

        static List<Point2d> GetProj(ProfileKind kind, HeightSrc src)
        {
            List<Point2d> p;
            if (_drawnProjPts.TryGetValue(LineKey(kind, src), out p) && p != null && p.Count >= 2)
                return p;
            return GetDrawn(kind, src);
        }

        static List<Point2d> GetFact(ProfileKind kind, HeightSrc src)
        {
            List<Point2d> p;
            if (_drawnFactPts.TryGetValue(LineKey(kind, src), out p) && p != null && p.Count >= 2)
                return p;
            return GetDrawn(kind, src);
        }

        static double ZFromLine(List<Point2d> pts, double x, Point3d o, double hSc, double osh)
        {
            if (pts == null || pts.Count < 2 || hSc < 1e-12)
                return osh;
            return osh + (YAt(pts, x) - o.Y) / hSc;
        }

        static void LabelPts(BlockTableRecord ms, Transaction tr, string layer, IList<Point2d> pts, string tag, double th)
        {
            if (!NvkUiState.ShowLineTags) return;
            if (pts == null || pts.Count < 1 || string.IsNullOrEmpty(tag)) return;
            var p = pts[pts.Count - 1];
            double h = th > 0 ? th * 0.9 : 2.2;
            NvkText.Make(ms, tr, layer, p.X + 0.35 * h, p.Y, tag, h,
                AttachmentPoint.MiddleLeft, 0.0, 8 * h, true);
        }

        static bool IsTinSrc(HeightSrc src)
        {
            return src == HeightSrc.Surface || src == HeightSrc.RoadTop || src == HeightSrc.BlackEarth;
        }

        static bool CanScatter(HeightSrc src)
        {
            return src != HeightSrc.RoadTop && src != HeightSrc.BlackEarth && src != HeightSrc.DeltaZash;
        }

        static short ColorOfSrc(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.RoadTop: return 1;
                case HeightSrc.Surface: return 5;
                case HeightSrc.ZashTop: return 6;
                case HeightSrc.SandTop: return 2;
                case HeightSrc.PipeTop: return 5;
                case HeightSrc.TrenchBottom: return 1;
                default: return 1;
            }
        }

        static List<Point2d> PtsFor(HeightSrc src, DomainResult last, SectionConfig cfg,
            Point3d o, double lSc, double hSc, double osh)
        {
            if (IsTinSrc(src))
                return SurfacePts(last.Axis, cfg, o, lSc, hSc, osh, src);
            var pipe = PipePts(last.Chain, src, cfg, o, lSc, hSc, osh, last.Axis);
            if (src == HeightSrc.ZashTop && pipe.Count >= 2)
            {
                var sand = PipePts(last.Chain, HeightSrc.SandTop, cfg, o, lSc, hSc, osh, last.Axis);
                List<Point2d> earth = null;
                if (last.Axis != null)
                    earth = EarthPts(last.Axis, cfg, o, lSc, hSc, osh);
                pipe = ClampZashPolyline(pipe, sand, earth);
                pipe = SimplifyLine(pipe, 0.001);
            }
            return pipe;
        }

        // Убирает почти коллинеарные промежуточные точки: расстояние от точки до прямой < tol.
        static List<Point2d> SimplifyLine(List<Point2d> pts, double tol)
        {
            if (pts.Count <= 2) return pts;
            var r = new List<Point2d> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var a = r[r.Count - 1];
                var b = pts[i];
                var c = pts[i + 1];
                double dx = c.X - a.X, dy = c.Y - a.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-12) { r.Add(b); continue; }
                double dist = Math.Abs((b.X - a.X) * dy - (b.Y - a.Y) * dx) / len;
                if (dist > tol) r.Add(b);
            }
            r.Add(pts[pts.Count - 1]);
            return r;
        }

        // Возвращает true если хотя бы одна точка pts выше соответствующей точки ceil (ZashTop превышает землю).
        static bool AnyAbove(List<Point2d> pts, List<Point2d> ceil)
        {
            foreach (var p in pts)
                if (p.Y > YAt(ceil, p.X) + 1e-4) return true;
            foreach (var c in ceil)
            {
                if (c.X < pts[0].X - 1e-9 || c.X > pts[pts.Count - 1].X + 1e-9) continue;
                if (YAt(pts, c.X) > c.Y + 1e-4) return true;
            }
            return false;
        }

        static double PipeEndZ(PipeSeg s, bool start, HeightSrc src, SectionConfig cfg)
        {
            double inv = start ? s.InvertStart : s.InvertEnd;
            double d = s.Diam;  // Civil InnerDiameter; стенка даёт наружный габарит
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

        static double ClampZashBand(double z, IList<AxisPt> axis, SectionConfig cfg, double ch)
        {
            if (axis == null || axis.Count == 0) return z;
            var p = AtCh(axis, ch);
            double sand = p.NamedZ(NamedLine.SandTop, cfg);
            double earth = ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
            if (earth < sand)
                z = sand;
            else
            {
                if (z < sand) z = sand;
                if (z > earth) z = earth;
            }
            return z;
        }

        static List<Point2d> PipePts(Chain chain, HeightSrc src, SectionConfig cfg,
            Point3d o, double lSc, double hSc, double osh, IList<AxisPt> axis)
        {
            var pts = new List<Point2d>();
            if (chain == null || chain.Segs == null) return pts;
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var s = chain.Segs[i];
                if (i == 0)
                {
                    double z0 = PipeEndZ(s, true, src, cfg);
                    if (src == HeightSrc.ZashTop) z0 = ClampZashBand(z0, axis, cfg, ch);
                    pts.Add(XY(o, lSc, hSc, osh, ch, z0));
                }
                ch += s.Length2d;
                double z1 = PipeEndZ(s, false, src, cfg);
                if (src == HeightSrc.ZashTop) z1 = ClampZashBand(z1, axis, cfg, ch);
                pts.Add(XY(o, lSc, hSc, osh, ch, z1));
                if (i + 1 < n)
                {
                    var nx = chain.Segs[i + 1];
                    double zn = PipeEndZ(nx, true, src, cfg);
                    if (src == HeightSrc.ZashTop) zn = ClampZashBand(zn, axis, cfg, ch);
                    if (Math.Abs(zn - z1) > 0.001)
                        pts.Add(XY(o, lSc, hSc, osh, ch, zn));
                }
            }
            return pts;
        }

        static Point2d XY(Point3d o, double lSc, double hSc, double osh, double ch, double z)
        {
            return new Point2d(o.X + ch * lSc, o.Y + (z - osh) * hSc);
        }

        /// <summary>Дно траншеи как pb-build-razrabotka-pts: только концы труб + ступенька лотка.</summary>
        static List<Point2d> TrenchBottomPts(Chain chain, SectionConfig cfg, Point3d o, double lSc, double hSc, double osh)
        {
            var pts = new List<Point2d>();
            if (chain == null || chain.Segs == null) return pts;
            double bed = cfg != null ? cfg.Bedding : 0.1;
            double ch = 0.0;
            int n = chain.Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var s = chain.Segs[i];
                double wall = cfg != null ? cfg.WallAt(s.Diam) : 0.0;
                if (i == 0)
                    pts.Add(XY(o, lSc, hSc, osh, ch, s.InvertStart - wall - bed));
                ch += s.Length2d;
                pts.Add(XY(o, lSc, hSc, osh, ch, s.InvertEnd - wall - bed));
                if (i + 1 < n)
                {
                    var nx = chain.Segs[i + 1];
                    if (Math.Abs(nx.InvertStart - s.InvertEnd) > 0.001)
                    {
                        double wallNx = cfg != null ? cfg.WallAt(nx.Diam) : 0.0;
                        pts.Add(XY(o, lSc, hSc, osh, ch, nx.InvertStart - wallNx - bed));
                    }
                }
            }
            return pts;
        }

        /// <summary>
        /// Линия по оси: одинаковые (пикет, Z) схлопываются, чтобы ступенька лотка
        /// не ломала землю. Разная Z на одном пикете — вертикальный шаг (перелив).
        /// </summary>
        static List<Point2d> SurfacePts(IList<AxisPt> axis, SectionConfig cfg, Point3d o, double lSc, double hSc, double osh, HeightSrc src)
        {
            var pts = new List<Point2d>();
            if (axis == null) return pts;
            double lastCh = -1e9, lastZ = 0.0;
            foreach (var p in axis)
            {
                double z = ProfileRows.ZAt(src, p, cfg);
                if (pts.Count > 0 && AxisBuilder.IsDupVertex(p.Ch, z, lastCh, lastZ))
                    continue;
                pts.Add(XY(o, lSc, hSc, osh, p.Ch, z));
                lastCh = p.Ch;
                lastZ = z;
            }
            return pts;
        }

        static List<Point2d> LinePts(IList<AxisPt> axis, HeightSrc src, SectionConfig cfg,
            Point3d o, double lSc, double hSc, double osh)
        {
            return SurfacePts(axis, cfg, o, lSc, hSc, osh, src);
        }

        static void DrawManholes(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh, SectionConfig cfg, ProfileKind kind)
        {
            EnsureLayer(ms.Database, tr, "NVK-EP-VIS", 7);
            var segs = last.Chain.Segs;
            int n = segs.Count;
            double ch = 0.0;
            var seen = new HashSet<string>();
            for (int i = 0; i <= n; i++)
            {
                double r, sump;
                string nm;
                if (i == 0)
                {
                    r = segs[0].ShaftR(true); sump = segs[0].StartSump; nm = segs[0].StartName;
                }
                else if (i == n)
                {
                    r = segs[n - 1].ShaftR(false); sump = segs[n - 1].EndSump; nm = segs[n - 1].EndName;
                    ch = last.Chain.Total;
                }
                else
                {
                    r = segs[i - 1].ShaftR(false); sump = segs[i - 1].EndSump; nm = segs[i - 1].EndName;
                }
                string key = (nm ?? "") + "@" + ch.ToString("0.00");
                if (seen.Contains(key)) { if (i < n) ch += segs[i].Length2d; continue; }
                seen.Add(key);
                if (r < 0.05) { if (i < n) ch += segs[i].Length2d; continue; }
                double xL, xR;
                if (i == 0) { xL = o.X + ch * lSc; xR = o.X + (ch + r) * lSc; }
                else if (i == n) { xL = o.X + (ch - r) * lSc; xR = o.X + ch * lSc; }
                else { xL = o.X + (ch - r) * lSc; xR = o.X + (ch + r) * lSc; }
                var ap = AtCh(last.Axis, i == 0 ? 0.0 : ch);
                double zTop = ProfileRows.ZAt(HeightSrc.RoadTop, ap, cfg);
                if (zTop < sump + 0.2) zTop = sump + 1.0;
                double yTop = o.Y + (zTop - osh) * hSc;
                double yBot = o.Y + (sump - osh) * hSc;
                if (yTop < yBot) { double t = yTop; yTop = yBot; yBot = t; }
                if (xR > xL + 1e-6)
                {
                    var rec = new[]
                    {
                        new Point2d(xL, yBot), new Point2d(xR, yBot),
                        new Point2d(xR, yTop), new Point2d(xL, yTop)
                    };
                    AddPline(ms, tr, "NVK-EP-VIS", rec, Aci(7), true, 30);
                }
                if (i < n) ch += segs[i].Length2d;
            }
        }

        /// <summary>VIS с разрывом у стенок колодца: x = (ch+ssR)…(ch+L−esR).</summary>
        static void DrawVisGapped(BlockTableRecord ms, Transaction tr, string layer, DomainResult last,
            Point3d o, double lSc, double hSc, double osh,
            List<Point2d> topPts, List<Point2d> botPts, short topCol, short botCol, bool sameLine)
        {
            double ch = 0.0;
            foreach (var sg in last.Chain.Segs)
            {
                double x1 = o.X + (ch + GapR(sg, true)) * lSc;
                double x2 = o.X + (ch + sg.Length2d - GapR(sg, false)) * lSc;
                if (x2 > x1 + 1e-3)
                {
                    var top = ClipPts(topPts, x1, x2);
                    var bot = ClipPts(botPts, x1, x2);
                    if (top.Count >= 2) AddPline(ms, tr, layer, top, Aci(topCol), false, 30);
                    if (!sameLine && bot.Count >= 2) AddPline(ms, tr, layer, bot, Aci(botCol), false, 30);
                }
                ch += sg.Length2d;
            }
        }

        static void HatchDiamGroups(BlockTableRecord ms, Transaction tr, DomainResult last,
            Point3d o, double lSc, List<Point2d> top, List<Point2d> bot, string pat, string visLayer)
        {
            HatchDiamGroups(ms, tr, last, o, lSc, top, bot, pat, visLayer, Aci(5),
                HatchPat.Scale(FillStyle.Of(pat, 8, 1)));
        }

        static void HatchDiamGroups(BlockTableRecord ms, Transaction tr, DomainResult last,
            Point3d o, double lSc, List<Point2d> top, List<Point2d> bot, string pat, string visLayer,
            AcadColor col, double scale)
        {
            foreach (var seg in DiamSegs(last.Chain, o.X, lSc))
            {
                double ch = 0.0;
                foreach (var sg in last.Chain.Segs)
                {
                    double mx1 = Math.Max(seg[0], o.X + (ch + GapR(sg, true)) * lSc);
                    double mx2 = Math.Min(seg[1], o.X + (ch + sg.Length2d - GapR(sg, false)) * lSc);
                    if (mx2 > mx1 + 1e-3)
                    {
                        var tc = ClipPts(top, mx1, mx2);
                        var bc = ClipPts(bot, mx1, mx2);
                        if (tc.Count >= 2 && bc.Count >= 2)
                        {
                            var closed = new List<Point2d>(tc);
                            for (int i = bc.Count - 1; i >= 0; i--) closed.Add(bc[i]);
                            Hatch(ms, tr, visLayer, closed.ToArray(), pat, scale, col);
                        }
                    }
                    ch += sg.Length2d;
                }
            }
        }

        static void HatchPipesInner(BlockTableRecord ms, Transaction tr, DomainResult last,
            Point3d o, double lSc, List<Point2d> top, List<Point2d> bot, string pat, string visLayer)
        {
            HatchPipesInner(ms, tr, last, o, lSc, top, bot, pat, visLayer, Aci(5),
                HatchPat.Scale(FillStyle.Of(pat, 8, 1)));
        }

        static void HatchPipesInner(BlockTableRecord ms, Transaction tr, DomainResult last,
            Point3d o, double lSc, List<Point2d> top, List<Point2d> bot, string pat, string visLayer,
            AcadColor col, double scale)
        {
            double ch = 0.0;
            foreach (var sg in last.Chain.Segs)
            {
                double x1 = o.X + (ch + GapR(sg, true)) * lSc;
                double x2 = o.X + (ch + sg.Length2d - GapR(sg, false)) * lSc;
                if (x2 > x1 + 1e-3)
                {
                    var tc = ClipPts(top, x1, x2);
                    var bc = ClipPts(bot, x1, x2);
                    if (tc.Count >= 2 && bc.Count >= 2)
                    {
                        var closed = new List<Point2d>(tc);
                        for (int i = bc.Count - 1; i >= 0; i--) closed.Add(bc[i]);
                        Hatch(ms, tr, visLayer, closed.ToArray(), pat, scale, col);
                    }
                }
                ch += sg.Length2d;
            }
        }

        static List<double[]> DiamSegs(Chain chain, double x0, double lSc)
        {
            var segs = new List<double[]>();
            double ch = 0.0, prev = -1.0, xStart = x0;
            foreach (var s in chain.Segs)
            {
                if (prev < 0.0) { xStart = x0 + ch * lSc; prev = s.Diam; }
                else if (Math.Abs(s.Diam - prev) > 1e-4)
                {
                    segs.Add(new[] { xStart, x0 + ch * lSc, prev });
                    xStart = x0 + ch * lSc;
                    prev = s.Diam;
                }
                ch += s.Length2d;
            }
            if (prev >= 0.0) segs.Add(new[] { xStart, x0 + ch * lSc, prev });
            return segs;
        }

        // DiamSegs grouped to net spans (inner-wall boundaries per segment).
        static List<double[]> DiamSegsNet(Chain chain, double x0, double lSc,
            List<double> netStarts, List<double> netEnds)
        {
            var result = new List<double[]>();
            var cs = chain.Segs;
            if (cs.Count == 0) return result;
            int groupStart = 0;
            double groupDiam = cs[0].Diam;
            for (int i = 1; i <= cs.Count; i++)
            {
                bool flush = i == cs.Count || Math.Abs(cs[i].Diam - groupDiam) > 1e-4;
                if (flush)
                {
                    result.Add(new[] { x0 + netStarts[groupStart] * lSc,
                                       x0 + netEnds[i - 1] * lSc, groupDiam });
                    if (i < cs.Count) { groupStart = i; groupDiam = cs[i].Diam; }
                }
            }
            return result;
        }

        static List<Point2d> ClipPts(List<Point2d> pts, double x1, double x2)
        {
            var r = new List<Point2d>();
            if (pts == null || pts.Count < 2) return r;
            if (x2 < x1) { double t = x1; x1 = x2; x2 = t; }
            double p0 = pts[0].X, p1 = pts[pts.Count - 1].X;
            if (p1 < p0) { double t = p0; p0 = p1; p1 = t; }
            if (x2 < p0 + 1e-9 || x1 > p1 - 1e-9) return r;
            if (x1 < p0) x1 = p0;
            if (x2 > p1) x2 = p1;
            if (x2 < x1 + 1e-9) return r;
            r.Add(new Point2d(x1, YAt(pts, x1, true)));
            foreach (var p in pts)
                if (p.X > x1 + 1e-9 && p.X < x2 - 1e-9) r.Add(p);
            r.Add(new Point2d(x2, YAt(pts, x2, false)));
            return r;
        }

        static double YAt(List<Point2d> pts, double x)
        {
            return YAt(pts, x, false);
        }

        static double YAt(List<Point2d> pts, double x, bool atEnd)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            var tmp = new PolylineClip.Pt[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                tmp[i] = new PolylineClip.Pt(pts[i].X, pts[i].Y);
            return PolylineClip.YAt(tmp, x, atEnd);
        }

        static void DrawTitle(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double w, double hSc, double osh, SectionConfig cfg, double th,
            ProfileKind kind)
        {
            double zMax = osh;
            foreach (var p in last.Axis)
            {
                double z = ProfileRows.ZAt(HeightSrc.RoadTop, p, cfg);
                if (z > zMax) zMax = z;
            }
            string title = ProfileAnnot.SheetTitle(last.NetName);
            double topY = o.Y + (zMax - osh) * hSc;
            NvkText.Make(ms, tr, layer, o.X + 0.5 * w, topY + 2.2 * th,
                title, th, AttachmentPoint.BottomCenter, 0.0, 50 * th, false);
        }

        static void DrawLegend(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg, double th, double xIns, double yIns)
        {
            double paper = NvkUiState.VpScaleEp >= 1.0 ? NvkUiState.VpScaleEp
                : (NvkSession.Cannoscale > 0 ? NvkSession.Cannoscale : 1000);
            bool gapAtMh = kind == ProfileKind.Razrabotka
                ? (NvkSession.L1 == null || NvkSession.L1.ShowManholes)
                : (NvkSession.Sheet(kind) == null || NvkSession.Sheet(kind).ShowManholes);
            var sb = new System.Text.StringBuilder();
            sb.Append("\\pql;");
            if (kind == ProfileKind.Truba)
            {
                if (last.Chain != null)
                {
                    var rows = VolumeCalc.ForLevel(last.Chain, last.Axis, ProfileKind.Truba,
                        cfg ?? new SectionConfig(), null);
                    foreach (var r in rows)
                        sb.Append("Длина ∅").Append(TableLayout.DiamMm(r.Diam)).Append(" мм:  ")
                            .Append(r.Length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                            .Append(" м\\P");
                }
            }
            else if (kind == ProfileKind.Zasypka)
            {
                var zrows = BodyAreasZasypka(last, cfg, gapAtMh);
                double sand = 0, soil = 0;
                foreach (var r in zrows)
                {
                    sb.Append("S" + TableLayout.DiamSub(r.Item1, r.Item2)
                        + " = " + r.Item3.ToString("0.00") + " м²\\P");
                    if (r.Item2.IndexOf("песок", System.StringComparison.Ordinal) >= 0) sand += r.Item3;
                    else soil += r.Item3;
                }
                if (sand > 1e-3) sb.Append("Sпеска = " + sand.ToString("0.00") + " м²\\P");
                if (soil > 1e-3) sb.Append("Sместн.грунта = " + soil.ToString("0.00") + " м²\\P");
                if (zrows.Count == 0) sb.Append("S = 0.00 м²");
            }
            else
            {
                var areas = BodyAreasByDiam(last, kind, cfg, gapAtMh);
                if (areas.Count >= 2)
                {
                    bool firstArea = true;
                    foreach (var kv in areas)
                    {
                        if (!firstArea) sb.Append("\\P");
                        sb.Append(TableLayout.SDiam(kv.Key) + " = " + kv.Value.ToString("0.00") + " м²");
                        firstArea = false;
                    }
                }
                else
                {
                    double totalArea = 0.0;
                    foreach (var kv in areas) totalArea += kv.Value;
                    sb.Append("S = " + totalArea.ToString("0.00") + " м²");
                }
            }
            sb.Append("\\P");
            sb.Append(TableLayout.ScaleLegend(
                TableLayout.LegendDenom(paper, lSc),
                TableLayout.LegendDenom(paper, hSc), osh));
            NvkText.Make(ms, tr, layer, xIns, yIns, sb.ToString(),
                0.7 * th, AttachmentPoint.BottomLeft, 0.0, 22 * th, false);
        }

        // (ch, z) pair — used for exact band area matching drawn hatching
        struct ChZ { public double Ch; public double Z; public ChZ(double c, double z) { Ch = c; Z = z; } }

        static Dictionary<int, double> BodyAreasByDiam(DomainResult last, ProfileKind kind, SectionConfig cfg, bool gapAtMh = false)
        {
            var result = new Dictionary<int, double>();
            if (kind == ProfileKind.Truba || last == null || last.Chain == null) return result;
            List<ChZ> topChZ, botChZ;
            BuildDrawnBand(last, kind, cfg, out topChZ, out botChZ);
            SumBandByDiam(last, topChZ, botChZ, gapAtMh, result);
            return result;
        }

        static List<System.Tuple<int, string, double>> BodyAreasZasypka(DomainResult last, SectionConfig cfg, bool gapAtMh)
        {
            var acc = new Dictionary<string, System.Tuple<int, string, double>>();
            if (last == null || last.Chain == null) return new List<System.Tuple<int, string, double>>();
            List<ChZ> topChZ, botChZ;
            BuildDrawnBand(last, ProfileKind.Zasypka, cfg, out topChZ, out botChZ);
            double ch = 0.0;
            foreach (var seg in last.Chain.Segs)
            {
                double ch0 = gapAtMh ? ch + GapR(seg, true) : ch;
                double ch1 = gapAtMh ? ch + seg.Length2d - GapR(seg, false) : ch + seg.Length2d;
                int dmm = seg.DiamMm;
                double x = ch0;
                while (x < ch1 - 1e-6)
                {
                    double x1 = RoadZones.EndAt(x, cfg != null ? cfg.Zones : null, ch1);
                    if (x1 <= x + 1e-9) x1 = ch1;
                    double a = BandAreaInRange(topChZ, botChZ, x, x1);
                    string zt = cfg != null ? cfg.ZoneAt(x) : null;
                    string ru = BackfillMap.Ru(BackfillMap.Of(zt, cfg));
                    string key = dmm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + ru;
                    if (!acc.ContainsKey(key))
                        acc[key] = System.Tuple.Create(dmm, ru, 0.0);
                    var old = acc[key];
                    acc[key] = System.Tuple.Create(old.Item1, old.Item2, old.Item3 + a);
                    x = x1;
                }
                ch += seg.Length2d;
            }
            var list = new List<System.Tuple<int, string, double>>(acc.Values);
            list.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : string.CompareOrdinal(a.Item2, b.Item2));
            return list;
        }

        /// <summary>Те же верх/низ, что у штриховки продольника (клип L2/L4/L6 + рандом).</summary>
        static void BuildDrawnBand(DomainResult last, ProfileKind kind, SectionConfig cfg,
            out List<ChZ> top, out List<ChZ> bot)
        {
            var earth = BuildEarthChZ(last.Axis, cfg);
            var sand = BuildPipeChZ(last.Chain, HeightSrc.SandTop, cfg);
            var trench = BuildBotChZ(last.Chain, cfg);

            if (kind == ProfileKind.Razrabotka)
            {
                bot = trench;
                top = earth;
                double amp = RowScatter.Amp(HeightSrc.TrenchBottom);
                if (amp > 1e-9)
                {
                    bot = ScatterChZ(bot, last, amp, HeightSrc.TrenchBottom);
                    bot = ClipChZBelow(bot, sand);
                }
                top = ClipChZAbove(top, bot);
                return;
            }
            if (kind == ProfileKind.Pesok)
            {
                top = sand;
                bot = trench;
                double aTop = RowScatter.Amp(HeightSrc.SandTop);
                double aBot = RowScatter.Amp(HeightSrc.TrenchBottom);
                if (aTop > 1e-9)
                    top = ScatterChZ(top, last, aTop, HeightSrc.SandTop);
                if (aBot > 1e-9)
                {
                    bot = ScatterChZ(bot, last, aBot, HeightSrc.TrenchBottom);
                    bot = ClipChZBelow(bot, top);
                }
                if (top != null && top.Count >= 2 && bot != null && bot.Count >= 2)
                    top = ClipChZAbove(top, bot);
                return;
            }
            if (kind == ProfileKind.Zashita)
            {
                List<ChZ> pesokTop, pesokBot;
                BuildDrawnBand(last, ProfileKind.Pesok, cfg, out pesokTop, out pesokBot);
                bot = pesokTop;
                top = BuildPipeChZ(last.Chain, HeightSrc.ZashTop, cfg);
                double amp = RowScatter.Amp(HeightSrc.ZashTop);
                if (amp > 1e-9)
                    top = ScatterChZ(top, last, amp, HeightSrc.ZashTop);
                top = ClipChZAbove(top, bot);
                var ceil = ClipChZAbove(earth, bot);
                top = ClipChZBelow(top, ceil);
                return;
            }
            // L5: низ засыпки = верх защиты
            List<ChZ> zashTop, zashBot;
            BuildDrawnBand(last, ProfileKind.Zashita, cfg, out zashTop, out zashBot);
            bot = zashTop;
            top = BuildSurfChZ(last.Axis, cfg, HeightSrc.Surface);
            top = ClipChZAbove(top, bot);
            double amp5 = RowScatter.Amp(HeightSrc.Surface);
            if (amp5 > 1e-9)
            {
                top = ScatterChZ(top, last, amp5, HeightSrc.Surface);
                top = ClipChZAbove(top, bot);
            }
        }

        static void SumBandByDiam(DomainResult last, List<ChZ> top, List<ChZ> bot, bool gapAtMh, Dictionary<int, double> result)
        {
            double ch = 0.0;
            foreach (var seg in last.Chain.Segs)
            {
                double ch0 = gapAtMh ? ch + GapR(seg, true) : ch;
                double ch1 = gapAtMh ? ch + seg.Length2d - GapR(seg, false) : ch + seg.Length2d;
                double a = (ch1 > ch0 + 1e-6) ? BandAreaInRange(top, bot, ch0, ch1) : 0.0;
                int dmm = seg.DiamMm;
                if (!result.ContainsKey(dmm)) result[dmm] = 0.0;
                result[dmm] += a;
                ch += seg.Length2d;
            }
        }

        static List<ChZ> BuildZashChZ(Chain chain, IList<AxisPt> axis, SectionConfig cfg)
        {
            var raw = BuildPipeChZ(chain, HeightSrc.ZashTop, cfg);
            var sand = BuildPipeChZ(chain, HeightSrc.SandTop, cfg);
            var earth = BuildEarthChZ(axis, cfg);
            var pts = new List<ChZ>(raw.Count);
            foreach (var p in raw)
            {
                double s = InterpChZ(sand, p.Ch);
                double e = InterpChZ(earth, p.Ch);
                pts.Add(new ChZ(p.Ch, LineClamp.AfterScatterZash(p.Z, s, e)));
            }
            pts = ClipChZAbove(pts, sand);
            var ceil = ClipChZAbove(earth, sand);
            pts = ClipChZBelow(pts, ceil);
            return pts;
        }

        static List<ChZ> ScatterChZ(List<ChZ> src, DomainResult last, double amp, HeightSrc line)
        {
            double total = last.Chain != null ? last.Chain.Total : 1.0;
            var vals = RowScatter.Vals(last, line);
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

        static double AvgZ(List<ChZ> pts)
        {
            if (pts == null || pts.Count == 0) return 0;
            double s = 0;
            foreach (var p in pts) s += p.Z;
            return s / pts.Count;
        }

        // Mirrors EarthPts: axis-dense points using BlackEarth (= TIN-pav)
        static List<ChZ> BuildEarthChZ(IList<AxisPt> axis, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
            double prevZ = double.NaN, prevCh = -1e9;
            foreach (var p in axis)
            {
                double z = ProfileRows.ZAt(HeightSrc.BlackEarth, p, cfg);
                if (!double.IsNaN(prevZ) && Math.Abs(p.Ch - prevCh) < 1e-6 && Math.Abs(z - prevZ) > 1e-4)
                    pts.Add(new ChZ(p.Ch, z));
                else if (Math.Abs(p.Ch - prevCh) > 1e-6 || pts.Count == 0)
                    pts.Add(new ChZ(p.Ch, z));
                else if (Math.Abs(z - prevZ) > 1e-4) { pts.Add(new ChZ(p.Ch, prevZ)); pts.Add(new ChZ(p.Ch, z)); }
                prevZ = z; prevCh = p.Ch;
            }
            return pts;
        }

        // Mirrors SurfacePts: axis-dense for TIN sources
        static List<ChZ> BuildSurfChZ(IList<AxisPt> axis, SectionConfig cfg, HeightSrc src)
        {
            var pts = new List<ChZ>();
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

        // Mirrors TrenchBottomPts: manhole-only
        static List<ChZ> BuildBotChZ(Chain chain, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
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

        // Mirrors PipePts: manhole-only for pipe-based sources
        static List<ChZ> BuildPipeChZ(Chain chain, HeightSrc src, SectionConfig cfg)
        {
            var pts = new List<ChZ>();
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

        static double InterpChZ(List<ChZ> pts, double ch)
        {
            if (pts.Count == 0) return 0.0;
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

        static double BandAreaInRange(List<ChZ> top, List<ChZ> bot, double ch0, double ch1)
        {
            // Exact band area: merge all breakpoints in range, trapezoid rule
            var breaks = new System.Collections.Generic.SortedSet<double> { ch0, ch1 };
            foreach (var p in top) if (p.Ch > ch0 + 1e-9 && p.Ch < ch1 - 1e-9) breaks.Add(p.Ch);
            foreach (var p in bot) if (p.Ch > ch0 + 1e-9 && p.Ch < ch1 - 1e-9) breaks.Add(p.Ch);
            double area = 0.0, prevCh = double.NaN, prevH = double.NaN;
            foreach (double brk in breaks)
            {
                double h = InterpChZ(top, brk) - InterpChZ(bot, brk);
                if (h < 0.0) h = 0.0;
                if (!double.IsNaN(prevCh) && brk > prevCh + 1e-9)
                    area += 0.5 * (h + prevH) * (brk - prevCh);
                prevCh = brk; prevH = h;
            }
            return area < 0 ? 0 : area;
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double BodyAreaM2(IList<AxisPt> axis, ProfileKind kind, SectionConfig cfg, double lSc, double hSc)
        {
            HeightSrc top = HeightSrc.Surface, bot = HeightSrc.TrenchBottom;
            if (kind == ProfileKind.Pesok) { top = HeightSrc.SandTop; bot = HeightSrc.TrenchBottom; }
            if (kind == ProfileKind.Zashita) { top = HeightSrc.ZashTop; bot = HeightSrc.SandTop; }
            if (kind == ProfileKind.Zasypka) { top = HeightSrc.Surface; bot = HeightSrc.ZashTop; }
            if (kind == ProfileKind.Truba) return 0;
            double a = 0;
            for (int i = 0; i < axis.Count - 1; i++)
            {
                double t0 = ProfileRows.ZAt(top, axis[i], cfg);
                double t1 = ProfileRows.ZAt(top, axis[i + 1], cfg);
                double b0 = ProfileRows.ZAt(bot, axis[i], cfg);
                double b1 = ProfileRows.ZAt(bot, axis[i + 1], cfg);
                double dch = axis[i + 1].Ch - axis[i].Ch;
                if (dch < 1e-9) continue;
                a += 0.5 * ((t0 - b0) + (t1 - b1)) * dch;
            }
            return a < 0 ? 0 : a;
        }

        static void DrawSectionMarks(BlockTableRecord ms, Transaction tr, DomainResult last,
            Point3d o, double lSc, double hSc, double osh, double th, double? tableBottom = null,
            int markOffset = 0, ProfileKind kind = ProfileKind.Razrabotka)
        {
            if (last.Geoms == null) return;
            var cfg = last.Cfg ?? new SectionConfig();
            EnsureLayer(ms.Database, tr, "NVK-MARK", 7);
            var tbl = NvkSession.Tbl();
            double markH = CsAnnot.SectionMarkH;
            var col = CadColor.CsMark(kind);
            var stationX = new List<double>(last.Geoms.Count);
            for (int i = 0; i < last.Geoms.Count; i++)
                stationX.Add(o.X + last.Geoms[i].Elev.Station * lSc);
            var labelX = CsAnnot.PlaceMarks(stationX, markH);
            for (int i = 0; i < last.Geoms.Count; i++)
            {
                var g = last.Geoms[i];
                double x = stationX[i];
                var ap = AtCh(last.Axis, g.Elev.Station);
                double yTop = o.Y + (ProfileRows.ZAt(HeightSrc.RoadTop, ap, cfg) - osh) * hSc;
                double yBot = tableBottom ?? (o.Y - th);
                AddLine(ms, tr, "NVK-MARK", x, yBot, x, yTop, col, 0);
                double yLabel = yBot + (tbl != null ? tbl.OffsetPct : 0.0) * th / 100.0;
                double lx = i < labelX.Count ? labelX[i] : x;
                NvkText.Make(ms, tr, "NVK-MARK", lx, yLabel, CsAnnot.SectionMark(markOffset + i),
                    markH, AttachmentPoint.BottomLeft, Math.PI * 0.5, 0, false);
            }
        }

        static List<SheetRow> InjectFactRows(List<SheetRow> spec, ProfileKind kind, SectionConfig cfg, DomainResult last)
        {
            if (spec == null) return spec;
            if (cfg == null || !cfg.ProjFact) return spec;
            bool anyAmp = false;
            foreach (var r0 in spec)
            {
                if (r0.Kind == SheetRowKind.Height && RowScatter.Ready(last, r0.Src))
                { anyAmp = true; break; }
            }
            if (!anyAmp) return spec;
            var factSrcs = new List<HeightSrc>();
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    factSrcs.Add(HeightSrc.TrenchBottom);
                    break;
                case ProfileKind.Pesok:
                    factSrcs.Add(HeightSrc.SandTop);
                    factSrcs.Add(HeightSrc.TrenchBottom);
                    break;
                case ProfileKind.Truba:
                    factSrcs.Add(HeightSrc.Invert);
                    break;
                case ProfileKind.Zashita:
                    factSrcs.Add(HeightSrc.ZashTop);
                    break;
                case ProfileKind.Zasypka:
                    factSrcs.Add(HeightSrc.Surface);
                    break;
                default: return spec;
            }
            var result = new List<SheetRow>();
            foreach (var r in spec)
            {
                bool want = r.Kind == SheetRowKind.Height && !r.IsFact && factSrcs.Contains(r.Src);
                if (want)
                {
                    bool hasPair = false;
                    foreach (var oth in spec)
                    {
                        if (oth != r && oth.Kind == SheetRowKind.Height && oth.Src == r.Src && oth.IsFact)
                        { hasPair = true; break; }
                    }
                    if (hasPair) { result.Add(r); continue; }
                    string baseLab = r.Label ?? "";
                    if (baseLab.IndexOf(", проект", StringComparison.Ordinal) >= 0
                        || baseLab.IndexOf(", факт", StringComparison.Ordinal) >= 0)
                    { result.Add(r); continue; }
                    result.Add(new SheetRow { Kind = r.Kind, Src = r.Src,
                        Label = baseLab + ", проект", Show = r.Show, RoundTo = r.RoundTo });
                    result.Add(new SheetRow { Kind = SheetRowKind.Height, Src = r.Src,
                        Label = baseLab + ", факт", Show = r.Show, RoundTo = r.RoundTo, IsFact = true });
                }
                else
                {
                    result.Add(r);
                }
            }
            return result;
        }

        static void DrawTable(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg, double th)
        {
            DrawTable(ms, tr, layer, last, o, lSc, hSc, osh, kind, cfg, th, null, osh);
        }

        static void DrawTable(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc, double hSc, double osh,
            ProfileKind kind, SectionConfig cfg, double th, double? tableTop, double oshLegend)
        {
            var tbl = NvkSession.Tbl(kind);

            // per-level конфигурация строк: если задана — заменяет умолчание
            var customDefs = NvkSession.GetLevelRows(kind);
            bool useCustom = customDefs != null && customDefs.Count > 0;
            List<SheetRow> spec;
            if (useCustom)
                spec = ProfileRows.FromDefs(customDefs);
            else
            {
                spec = kind == ProfileKind.Razrabotka
                    ? ProfileRows.ForL1(NvkSession.L1, tbl)
                    : ProfileRows.For(kind, tbl);
            }
            spec = InjectFactRows(spec, kind, cfg, last);

            int segN = last.Chain.Segs.Count;
            bool mhLayout = SheetManholes(kind);
            var (netStarts, netEnds) = NetSpans(last.Chain, mhLayout);
            var lenSpans = TableLayout.LengthIntervals(netStarts, netEnds, last.Chain.Total);

            // Высоты: при колодцах на листе — у внутренних стенок (как разрыв линии).
            // Плюс станции поперечных сечений — отметка в той же строке, где стоит риска сечения.
            var hxs = new List<double>();
            if (mhLayout)
            {
                for (int si = 0; si < segN; si++)
                {
                    if (hxs.Count == 0 || netStarts[si] - hxs[hxs.Count - 1] > 1e-6) hxs.Add(netStarts[si]);
                    if (netEnds[si] - hxs[hxs.Count - 1] > 1e-6) hxs.Add(netEnds[si]);
                }
            }
            else
            {
                double rawCh = 0.0;
                for (int si = 0; si <= segN; si++)
                {
                    hxs.Add(rawCh);
                    if (si < segN) rawCh += last.Chain.Segs[si].Length2d;
                }
            }
            if (last.Geoms != null)
            {
                foreach (var g in last.Geoms)
                {
                    if (g == null || g.Elev == null) continue;
                    AddUniqueCh(hxs, g.Elev.Station);
                }
            }
            var zs = new List<double>();
            foreach (var s in last.Axis) zs.Add(s.Invert);
            var lens = new List<double>();
            foreach (var sg in last.Chain.Segs) lens.Add(sg.Length2d);
            int lenPrec = 2, slpPrec = tbl.SlopeRoundTo;
            foreach (var r in spec)
            {
                if (r.Kind == SheetRowKind.Length) lenPrec = TableLayout.Prec(r.RoundTo, lenPrec);
                if (r.Kind == SheetRowKind.Slope) slpPrec = TableLayout.Prec(r.RoundTo, slpPrec);
            }
            int maxH = TableLayout.MaxChars(zs, tbl.RoundTo, 6);
            int maxL = TableLayout.MaxChars(lens, lenPrec, 4);
            double hH = TableLayout.RowHeight(TableRowKind.Height, th, maxH, maxL);
            // для custom-строк всегда считаем высоту Длины/Уклона (Show уже задан per-row)
            double hL = (useCustom || tbl.ShowLengths) ? TableLayout.RowHeight(TableRowKind.Length, th, maxH, maxL) : 0.0;
            double hS = (useCustom || tbl.ShowSlopes)  ? TableLayout.RowHeight(TableRowKind.Slope,  th, maxH, maxL) : 0.0;
            double hX = th * 3.0;
            var labels = new List<string>();
            foreach (var r in spec)
            {
                bool vis = useCustom ? r.Show : rowShow(r, tbl);
                if (!vis) continue;
                labels.Add(r.Label);
            }
            double labW = TableLayout.LabelColW(tbl, labels);
            double xLft = o.X;
            double xRgt = o.X + last.Chain.Total * lSc;
            double dataL = xLft - th, dataR = xRgt + th, xLbl = dataL - labW;
            double yTop = tableTop ?? (o.Y - TableGapM(tbl) * hSc);
            double tblH = 0.0;
            foreach (var row in spec)
            {
                bool vis = useCustom ? row.Show : rowShow(row, tbl);
                if (!vis) continue;
                tblH += RowH(row.Kind, hH, hL, hS, hX);
            }
            double yBot = yTop - tblH;
            var by = AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
            AddLine(ms, tr, layer, xLbl, yTop, dataR, yTop, by, 0);
            AddLine(ms, tr, layer, xLbl, yBot, dataR, yBot, by, 0);
            AddLine(ms, tr, layer, xLbl, yTop, xLbl, yBot, by, 0);
            AddLine(ms, tr, layer, dataL, yTop, dataL, yBot, by, 0);
            AddLine(ms, tr, layer, dataR, yTop, dataR, yBot, by, 0);
            DrawLegend(ms, tr, layer, last, lSc, hSc, oshLegend, kind, cfg, th, xLbl, yTop);

            double y = yTop;
            double off = tbl.Offset;
            foreach (var row in spec)
            {
                bool vis = useCustom ? row.Show : rowShow(row, tbl);
                if (!vis) continue;
                double rh = RowH(row.Kind, hH, hL, hS, hX);
                double yB = y - rh;
                AddLine(ms, tr, layer, xLbl, yB, dataR, yB, by, 0);
                NvkText.Make(ms, tr, layer, 0.5 * (xLbl + dataL), 0.5 * (y + yB), row.Label,
                    th, AttachmentPoint.MiddleCenter, 0.0);
                if (row.Kind == SheetRowKind.Height)
                {
                    int rowRound = TableLayout.Prec(row.RoundTo, tbl.RoundTo);
                    bool hasFactPair = false;
                    foreach (var oth in spec)
                    {
                        if (oth != row && oth.Kind == SheetRowKind.Height && oth.Src == row.Src && oth.IsFact)
                        { hasFactPair = true; break; }
                    }
                    List<Point2d> srcPts;
                    if (row.Src == HeightSrc.DeltaZash)
                        srcPts = null;
                    else if (row.IsFact)
                    {
                        srcPts = GetFact(kind, row.Src);
                        if (srcPts == null)
                        {
                            var proj = GetProj(kind, row.Src)
                                ?? PtsFor(row.Src, last, cfg, o, lSc, hSc, osh);
                            double scAmp = RowScatter.Amp(row.Src);
                            if (scAmp > 0.0 && RowScatter.Ready(last, row.Src)
                                && CanScatter(row.Src) && proj != null && proj.Count >= 2)
                                srcPts = OffsetScatter(proj, last, o, lSc, hSc, scAmp, row.Src);
                            else
                                srcPts = proj;
                        }
                    }
                    else if (hasFactPair)
                        srcPts = GetProj(kind, row.Src);
                    else
                        srcPts = GetDrawn(kind, row.Src);
                    if (srcPts == null && row.Src != HeightSrc.DeltaZash)
                        srcPts = PtsFor(row.Src, last, cfg, o, lSc, hSc, osh);
                    double prevX = double.NaN;
                    for (int i = 0; i < hxs.Count; i++)
                    {
                        double x = o.X + hxs[i] * lSc;
                        double zv;
                        if (row.Src == HeightSrc.DeltaZash)
                        {
                            var zTop = GetDrawn(kind, HeightSrc.ZashTop) ?? PtsFor(HeightSrc.ZashTop, last, cfg, o, lSc, hSc, osh);
                            var zPipe = GetDrawn(kind, HeightSrc.PipeTop) ?? PtsFor(HeightSrc.PipeTop, last, cfg, o, lSc, hSc, osh);
                            zv = ProfileRows.DeltaZashThickness(
                                ZFromLine(zTop, x, o, hSc, osh),
                                ZFromLine(zPipe, x, o, hSc, osh));
                        }
                        else
                            zv = ZFromLine(srcPts, x, o, hSc, osh);
                        double disp = x;
                        if (!double.IsNaN(prevX) && (disp - prevX) < th)
                            disp = prevX + th;
                        prevX = disp;
                        NvkText.Make(ms, tr, layer, disp, 0.5 * (y + yB),
                            TableLayout.Rtos(zv, rowRound), th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
                    }
                }
                else if (row.Kind == SheetRowKind.Length)
                {
                    // Нетто трубы плюс пролёты колодцев (0→стенка, стенка→стенка, стенка→конец).
                    int spanI = 0;
                    foreach (var sp in lenSpans)
                    {
                        double xaCh = sp[0];
                        double xbCh = sp[1];
                        if (xbCh <= xaCh + 1e-6) continue;
                        double xa = o.X + xaCh * lSc;
                        double xb = o.X + xbCh * lSc;
                        AddLine(ms, tr, layer, xa, y, xa, yB, by, 0);
                        AddLine(ms, tr, layer, xb, y, xb, yB, by, 0);
                        double dist = xbCh - xaCh;
                        double w = xb - xa;
                        string lenStr = TableLayout.Rtos(dist, TableLayout.Prec(row.RoundTo, lenPrec));
                        double textW = lenStr.Length * 0.65 * th;
                        if (w < th)
                        {
                            double ymidL = 0.5 * (y + yB);
                            bool left = TableLayout.LengthLeaderToLeft(spanI);
                            double tx = TableLayout.LengthLeaderTextX(spanI, xa, xb, th);
                            AddMLeader(ms, tr, layer, 0.5 * (xa + xb), ymidL, tx, ymidL, lenStr, th, 0.0, left);
                        }
                        else if (w < textW)
                            NvkText.Make(ms, tr, layer, 0.5 * (xa + xb), 0.5 * (y + yB),
                                lenStr, th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
                        else
                            NvkText.Make(ms, tr, layer, 0.5 * (xa + xb), 0.5 * (y + yB),
                                lenStr, th, AttachmentPoint.MiddleCenter, 0.0);
                        spanI++;
                    }
                }
                else if (row.Kind == SheetRowKind.Slope)
                {
                    for (int i = 0; i < segN; i++)
                    {
                        var sg = last.Chain.Segs[i];
                        double xa = o.X + netStarts[i] * lSc;
                        double xb = o.X + netEnds[i] * lSc;
                        AddLine(ms, tr, layer, xa, y, xa, yB, by, 0);
                        AddLine(ms, tr, layer, xb, y, xb, yB, by, 0);
                        double dz = sg.InvertEnd - sg.InvertStart;
                        if (dz > 0.0)
                            AddLine(ms, tr, layer, xb, y, xa, yB, by, 0);
                        else
                            AddLine(ms, tr, layer, xa, y, xb, yB, by, 0);
                        string st = TableLayout.SlopeText(dz, sg.Length2d, tbl, TableLayout.Prec(row.RoundTo, slpPrec));
                        double sx = dz > 0.0 ? xa + off : xb - off;
                        var att = dz > 0.0 ? AttachmentPoint.TopLeft : AttachmentPoint.TopRight;
                        NvkText.Make(ms, tr, layer, sx, y - off, st, th, att, 0.0);
                    }
                }
                else if (row.Kind == SheetRowKind.Diam)
                {
                    // Диаметры — всегда по полной длине трубы (колодцы как выключенные).
                    foreach (var ds in DiamSegs(last.Chain, o.X, lSc))
                    {
                        int mm = (int)Math.Floor(ds[2] * 1000.0 + 0.5);
                        NvkText.Make(ms, tr, layer, 0.5 * (ds[0] + ds[1]), 0.5 * (y + yB),
                            "%%c" + mm.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            th, AttachmentPoint.MiddleCenter, 0.0);
                        if (ds[0] > xLft + 1e-6)
                            AddLine(ms, tr, layer, ds[0], y, ds[0], yB, by, 0);
                        if (ds[1] < xRgt - 1e-6)
                            AddLine(ms, tr, layer, ds[1], y, ds[1], yB, by, 0);
                    }
                }
                else if (row.Kind == SheetRowKind.Manhole)
                {
                    var cs = last.Chain.Segs;
                    double mhCh = 0.0;
                    for (int i = 0; i <= cs.Count; i++)
                    {
                        string nm = i == 0 ? cs[0].StartName
                            : i == cs.Count ? cs[cs.Count - 1].EndName
                            : cs[i].StartName;
                        nm = SiteTitle.Label(nm, NvkUiState.MhStripParen);
                        if (!string.IsNullOrEmpty(nm))
                            NvkText.Make(ms, tr, layer, o.X + mhCh * lSc, 0.5 * (y + yB),
                                nm, th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
                        if (i < cs.Count) mhCh += cs[i].Length2d;
                    }
                }
                y = yB;
            }
        }

        static bool rowShow(SheetRow row, TableCfg tbl)
        {
            if (row == null || !row.Show) return false;
            if (row.Kind == SheetRowKind.Length && (tbl == null || !tbl.ShowLengths)) return false;
            if (row.Kind == SheetRowKind.Slope && (tbl == null || !tbl.ShowSlopes)) return false;
            return true;
        }

        static double RowH(SheetRowKind kind, double hH, double hL, double hS, double hX)
        {
            if (kind == SheetRowKind.Length) return hL;
            if (kind == SheetRowKind.Slope) return hS;
            if (kind == SheetRowKind.Diam || kind == SheetRowKind.Manhole) return hX;
            return hH;
        }

        static void AddUniqueCh(List<double> xs, double ch)
        {
            if (xs == null) return;
            for (int i = 0; i < xs.Count; i++)
            {
                if (Math.Abs(xs[i] - ch) <= 1e-6) return;
                if (xs[i] > ch)
                {
                    xs.Insert(i, ch);
                    return;
                }
            }
            xs.Add(ch);
        }

        static bool SheetManholes(ProfileKind kind)
        {
            if (kind == ProfileKind.Razrabotka)
                return NvkSession.L1 == null || NvkSession.L1.ShowManholes;
            var sh = NvkSession.Sheet(kind);
            return sh == null || sh.ShowManholes;
        }

        /// <summary>Разрыв профиля только у шахты с Ø. Нулевой колодец — без зазора.</summary>
        static double GapR(PipeSeg s, bool start)
        {
            return s == null ? 0.0 : s.ShaftR(start);
        }

        // netStarts/netEnds: внутренние стенки, тот же радиус что у разрыва линии и прямоугольника колодца.
        static (List<double>, List<double>) NetSpans(Chain chain, bool manholes)
        {
            var starts = new List<double>();
            var ends   = new List<double>();
            if (chain == null) return (starts, ends);
            double ch = 0.0;
            foreach (var s in chain.Segs)
            {
                double rs = manholes ? GapR(s, true) : 0.0;
                double re = manholes ? GapR(s, false) : 0.0;
                double a = ch + rs;
                double b = ch + s.Length2d - re;
                if (b < a) b = a;
                starts.Add(a);
                ends.Add(b);
                ch += s.Length2d;
            }
            return (starts, ends);
        }

        static AxisPt AtCh(IList<AxisPt> axis, double ch)
        {
            if (axis == null || axis.Count == 0) return new AxisPt();
            if (ch <= axis[0].Ch) return axis[0];
            if (ch >= axis[axis.Count - 1].Ch) return axis[axis.Count - 1];
            for (int i = 0; i < axis.Count - 1; i++)
            {
                if (ch >= axis[i].Ch - 1e-9 && ch <= axis[i + 1].Ch + 1e-9)
                {
                    double dt = axis[i + 1].Ch - axis[i].Ch;
                    double u = dt < 1e-12 ? 0 : (ch - axis[i].Ch) / dt;
                    return new AxisPt
                    {
                        Ch = ch,
                        Invert = axis[i].Invert * (1 - u) + axis[i + 1].Invert * u,
                        Diam = axis[i].Diam * (1 - u) + axis[i + 1].Diam * u,
                        Surface = MixN(axis[i].Surface, axis[i + 1].Surface, u),
                        X = axis[i].X * (1 - u) + axis[i + 1].X * u,
                        Y = axis[i].Y * (1 - u) + axis[i + 1].Y * u
                    };
                }
            }
            return axis[axis.Count - 1];
        }

        static double? MixN(double? a, double? b, double u)
        {
            if (!a.HasValue && !b.HasValue) return null;
            double aa = a ?? b.Value, bb = b ?? a.Value;
            return aa * (1 - u) + bb * u;
        }

        static string LayerOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "NVK-EP-RAZR";
                case ProfileKind.Pesok: return "NVK-EP-PESOK";
                case ProfileKind.Truba: return "NVK-EP-TRUBA";
                case ProfileKind.Zashita: return "NVK-EP-ZASH";
                default: return "NVK-EP-ZAS";
            }
        }

        static short ColorOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return 4;
                case ProfileKind.Pesok: return 2;
                case ProfileKind.Truba: return 5;
                case ProfileKind.Zashita: return 6;
                default: return 3;
            }
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = name };
            ltr.Color = Aci(color);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static AcadColor Aci(short i) { return AcadColor.FromColorIndex(ColorMethod.ByAci, i); }

        static void AddLine(BlockTableRecord btr, Transaction tr, string layer,
            double x1, double y1, double x2, double y2, AcadColor col, int lw)
        {
            var ln = new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));
            ln.SetDatabaseDefaults();
            ln.Layer = layer;
            ln.Color = col;
            ln.LinetypeScale = 1.0;
            ln.LineWeight = LineWeight.LineWeight015;
            btr.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
            NvkTag.Stamp(tr, ln);
        }

        static void AddMLeader(BlockTableRecord btr, Transaction tr, string layer,
            double arrowX, double arrowY, double textX, double textY,
            string contents, double th, double textRot, bool textLeft = false)
        {
            var ml = new MLeader();
            ml.SetDatabaseDefaults();
            if (!string.IsNullOrEmpty(layer)) ml.Layer = layer;
            ml.ContentType = ContentType.MTextContent;
            double asz = NvkLeader.TextH * 0.5;
            ml.ArrowSize = asz;
            double dogLen = NvkLeader.ShelfLen;
            var dog = textLeft ? new Vector3d(-1, 0, 0) : new Vector3d(1, 0, 0);
            var textPt = new Point3d(textX, textY, 0);
            var land = textPt - dog * dogLen;
            try { ml.EnableLanding = true; } catch (Exc) { }
            try { ml.EnableDogleg = true; } catch (Exc) { }
            try { ml.DoglegLength = dogLen; } catch (Exc) { }
            try { ml.LandingGap = 0.05; } catch (Exc) { }
            try { ml.LeaderLineType = LeaderType.StraightLeader; } catch (Exc) { }
            try
            {
                ml.SetTextAttachmentType(TextAttachmentType.AttachmentMiddle,
                    textLeft ? LeaderDirectionType.LeftLeader : LeaderDirectionType.RightLeader);
            }
            catch (Exc) { }

            int li = ml.AddLeader();
            int lni = ml.AddLeaderLine(li);
            ml.AddFirstVertex(lni, new Point3d(arrowX, arrowY, 0));
            ml.AddLastVertex(lni, land);
            try { ml.SetDogleg(li, dog); } catch (Exc) { }

            var mt = new MText();
            mt.SetDatabaseDefaults();
            mt.Contents = contents ?? "";
            mt.TextHeight = NvkLeader.TextH;
            mt.Attachment = textLeft ? AttachmentPoint.MiddleRight : AttachmentPoint.MiddleLeft;
            mt.Location = textPt;
            mt.Rotation = textRot;
            try { mt.TextStyleId = btr.Database.Textstyle; } catch (Exc) { }
            ml.MText = mt;
            try { ml.TextLocation = textPt; } catch (Exc) { }

            btr.AppendEntity(ml);
            tr.AddNewlyCreatedDBObject(ml, true);
            try
            {
                var mt2 = ml.MText;
                if (mt2 != null)
                {
                    mt2.Attachment = textLeft ? AttachmentPoint.MiddleRight : AttachmentPoint.MiddleLeft;
                    mt2.Location = textPt;
                    mt2.Rotation = textRot;
                    ml.MText = mt2;
                }
                ml.TextLocation = textPt;
            }
            catch (Exc) { }
            NvkTag.Stamp(tr, ml);
            NvkAnno.ApplyLeader(ml);
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, string layer,
            IList<Point2d> pts, AcadColor col, bool closed, int lw)
        {
            AddPline(btr, tr, layer, pts, col, closed, lw, null);
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, string layer,
            IList<Point2d> pts, AcadColor col, bool closed, int lw, string ltype)
        {
            if (pts == null || pts.Count < 2) return;
            var pl = new Polyline(pts.Count);
            pl.SetDatabaseDefaults();
            pl.Layer = layer;
            pl.Closed = closed;
            pl.Color = col;
            pl.LineWeight = LineWeight.LineWeight015;
            pl.Plinegen = true;
            pl.LinetypeScale = 1.0;
            if (!string.IsNullOrEmpty(ltype) && !string.Equals(ltype, "Continuous", StringComparison.OrdinalIgnoreCase))
            {
                EnsureLinetype(btr.Database, tr, ltype);
                try { pl.Linetype = ltype; } catch (Exc) { }
            }
            for (int i = 0; i < pts.Count; i++)
                pl.AddVertexAt(i, pts[i], 0, 0, 0);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            if (!closed && _liveSrc.HasValue)
            {
                var ch = new double[pts.Count];
                double lsc = _liveLSc > 1e-12 ? _liveLSc : 1.0;
                for (int i = 0; i < pts.Count; i++)
                    ch[i] = (pts[i].X - _liveO.X) / lsc;
                NvkTag.StampLive(tr, pl, new EpLive
                {
                    Seg = _liveSeg,
                    Kind = _liveKind,
                    Src = _liveSrc.Value,
                    Ox = _liveO.X, Oy = _liveO.Y,
                    LSc = _liveLSc, HSc = _liveHSc, Osh = _liveOsh,
                    Y0 = pts[0].Y,
                    Ch = ch
                });
            }
            else
                NvkTag.Stamp(tr, pl);
        }

        static void EnsureLinetype(Database db, Transaction tr, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            try { db.LoadLineTypeFile(name, "acad.lin"); } catch (Exc) { }
            if (!((LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead)).Has(name))
            {
                try { db.LoadLineTypeFile(name, "acadiso.lin"); } catch (Exc) { }
            }
            if (!((LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead)).Has(name)
                && string.Equals(name, "DASHED", StringComparison.OrdinalIgnoreCase))
            {
                try { db.LoadLineTypeFile("ACAD_ISO02W100", "acadiso.lin"); } catch (Exc) { }
            }
        }

        static void Hatch(BlockTableRecord btr, Transaction tr, string layer,
            Point2d[] pts, string pat, double scale, AcadColor col)
        {
            if (pts == null || pts.Length < 3) return;
            try
            {
                var pl = new Polyline(pts.Length);
                pl.SetDatabaseDefaults();
                pl.Layer = layer;
                pl.Closed = true;
                pl.Visible = false;
                for (int i = 0; i < pts.Length; i++)
                    pl.AddVertexAt(i, pts[i], 0, 0, 0);
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                var h = new Hatch();
                h.SetDatabaseDefaults();
                h.Layer = layer;
                h.Color = col;
                btr.AppendEntity(h);
                tr.AddNewlyCreatedDBObject(h, true);
                NvkAnno.HatchStart(h);
                NvkTag.Stamp(tr, pl);
                NvkTag.Stamp(tr, h);
                try { h.SetHatchPattern(HatchPatternType.PreDefined, pat); }
                catch (Exc) { h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31"); }
                h.PatternScale = scale < 0.001 ? 0.001 : scale;
                h.Associative = false;
                var ids = new ObjectIdCollection();
                ids.Add(pl.ObjectId);
                h.AppendLoop(HatchLoopTypes.Outermost, ids);
                h.EvaluateHatch(true);
                NvkAnno.HatchEnd(h, scale);
            }
            catch (Exc) { }
        }

        static void HatchBackfill(BlockTableRecord ms, Transaction tr, string layer,
            SheetStyle sh, SectionConfig cfg, DomainResult last, Point3d o, double lSc,
            List<Point2d> topPts, List<Point2d> botPts, bool gapAtMh)
        {
            if (topPts == null || botPts == null || topPts.Count < 2 || botPts.Count < 2) return;
            if (AvgY(topPts) <= AvgY(botPts) + 1e-4) return;
            double total = last != null && last.Chain != null ? last.Chain.Total : 0.0;
            if (cfg == null || cfg.Zones == null || cfg.Zones.Count == 0 || total < 1e-6)
            {
                string zt = cfg != null ? cfg.ZoneType : null;
                HatchFillBand(ms, tr, layer, last, o, lSc, topPts, botPts, gapAtMh,
                    HatchPat.ForBackfill(sh, zt, cfg));
                return;
            }
            for (int i = 0; i < cfg.Zones.Count; i++)
            {
                var z = cfg.Zones[i];
                if (z == null || z.Ch1 <= z.Ch0 + 1e-9) continue;
                double x1 = o.X + z.Ch0 * lSc;
                double x2 = o.X + z.Ch1 * lSc;
                var top = ClipPts(topPts, x1, x2);
                var bot = ClipPts(botPts, x1, x2);
                if (!BandHasHeight(top, bot)) continue;
                if (z.Ch0 > 1e-6)
                {
                    double yt = YAt(topPts, x1);
                    double yb = YAt(botPts, x1);
                    if (yt > yb + 1e-3)
                        AddLine(ms, tr, layer, x1, yb, x1, yt, Aci(7), 0);
                }
                HatchFillBand(ms, tr, layer, last, o, lSc, top, bot, gapAtMh,
                    HatchPat.ForBackfill(sh, z.Type, cfg));
            }
        }

        static bool BandHasHeight(List<Point2d> top, List<Point2d> bot)
        {
            if (top == null || bot == null || top.Count < 2 || bot.Count < 2) return false;
            for (int i = 0; i < top.Count; i++)
            {
                if (top[i].Y > YAt(bot, top[i].X) + 1e-3) return true;
            }
            for (int i = 0; i < bot.Count; i++)
            {
                if (YAt(top, bot[i].X) > bot[i].Y + 1e-3) return true;
            }
            return false;
        }

        static void HatchFillBand(BlockTableRecord ms, Transaction tr, string layer,
            DomainResult last, Point3d o, double lSc,
            List<Point2d> top, List<Point2d> bot, bool gapAtMh, FillStyle hx)
        {
            if (hx == null || string.IsNullOrEmpty(hx.Pattern)) return;
            if (!BandHasHeight(top, bot)) return;
            var col = CadColor.Of(hx);
            double sc = HatchPat.Scale(hx);
            if (gapAtMh)
                HatchPipesInner(ms, tr, last, o, lSc, top, bot, hx.Pattern, layer, col, sc);
            else
                Hatch(ms, tr, layer, Band(top, bot), hx.Pattern, sc, col);
        }
    }
}
