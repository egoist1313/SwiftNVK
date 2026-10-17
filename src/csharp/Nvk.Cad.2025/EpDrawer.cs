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
    static class EpDrawer
    {
        public static string Draw(Database db, Transaction tr, Point3d origin, double lScale, double hScale, bool asBlock)
        {
            var runs = NvkSession.AllRuns();
            bool any = false;
            foreach (var r in runs)
                if (r != null && r.Axis != null && r.Axis.Count >= 2) { any = true; break; }
            if (!any)
                return "[НВК] EP: нет оси. Сначала НВК-ДАМП.";

            foreach (var r in runs)
                if (r != null) EraseOld(db, tr, r.SegId);
            EnsureLayer(db, tr, "NVK-INS", 7);
            EnsureLayer(db, tr, "NVK-EP-RAZR", 4);
            EnsureLayer(db, tr, "NVK-EP-PESOK", 2);
            EnsureLayer(db, tr, "NVK-EP-TRUBA", 5);
            EnsureLayer(db, tr, "NVK-EP-ZASH", 6);
            EnsureLayer(db, tr, "NVK-EP-ZAS", 3);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var keep = NvkSession.Last;
            int nIns = 0, nMark = 0, nRun = 0, markOff = NvkSites.GeomOffset();
            double y = origin.Y;
            double osh0 = 0, rowGap0 = 0, lenSum = 0;
            foreach (var run in runs)
            {
                if (run == null || run.Axis == null || run.Axis.Count < 2) continue;
                NvkSession.Last = run;
                var cfg = run.Cfg ?? new SectionConfig();
                double osh = AxisBuilder.Osh(run.Axis, cfg);
                double zMax = osh + 1.0;
                foreach (var p in run.Axis)
                {
                    double zs = p.NamedZ(NamedLine.Surface, cfg);
                    if (zs > zMax) zMax = zs;
                }
                double rowGap = (zMax - osh) * hScale + 12.0;
                if (rowGap < 25.0) rowGap = 25.0;
                ProfileSheet.DrawAll(db, tr, ms, run, new Point3d(origin.X, y, 0.0),
                    lScale, hScale, false, nRun, markOff);
                int nk = NvkSession.LastEp != null && NvkSession.LastEp.Kinds != null
                    ? NvkSession.LastEp.Kinds.Count : 5;
                nIns += nk;
                nMark += run.Geoms == null ? 0 : run.Geoms.Count * Math.Max(nk, 1);
                if (nRun == 0) { osh0 = osh; rowGap0 = NvkSession.LastEp != null ? NvkSession.LastEp.RowGap : rowGap; }
                lenSum += run.Chain != null ? run.Chain.Total : 0;
                if (run.Geoms != null) markOff += run.Geoms.Count;
                y -= (NvkSession.LastEp != null ? NvkSession.LastEp.RowGap : rowGap) * Math.Max(nk, 1) + 20.0;
                nRun++;
            }
            if (keep != null) NvkSession.Last = keep;

            return "[НВК] EP: участков " + nRun
                 + "  листов " + nIns
                 + "  россыпью"
                 + "  OSH=" + osh0.ToString("0.000")
                 + "  L=" + lenSum.ToString("0.000") + " м"
                 + "  шагY=" + rowGap0.ToString("0.0")
                 + "  меток " + nMark;
        }

        static int DrawSectionMarks(Database db, Transaction tr, BlockTableRecord ms, DomainResult last,
            Point3d origin, double lSc, double hSc, double osh, double rowGap, List<ProfileKind> kinds)
        {
            if (last.Geoms == null || last.Geoms.Count == 0 || kinds == null || kinds.Count == 0)
                return 0;
            EnsureLayer(db, tr, "NVK-MARK", 8);
            int n = 0;
            var cfg = last.Cfg ?? new SectionConfig();
            for (int i = 0; i < last.Geoms.Count; i++)
            {
                var g = last.Geoms[i];
                double x = origin.X + g.Elev.Station * lSc;
                for (int row = 0; row < kinds.Count; row++)
                {
                    double yBase = origin.Y - row * rowGap;
                    double zS = g.Elev.Surface;
                    if (last.Axis != null)
                    {
                        foreach (var a in last.Axis)
                        {
                            if (Math.Abs(a.Ch - g.Elev.Station) < 0.05)
                            {
                                zS = a.NamedZ(NamedLine.Surface, cfg);
                                break;
                            }
                        }
                    }
                    double yTop = yBase + (zS - osh) * hSc;
                    double yBot = yBase - NvkSession.Tbl().TextH;
                    var ln = new Line(new Point3d(x, yBot, 0.0), new Point3d(x, yTop, 0.0));
                    ln.SetDatabaseDefaults();
                    ln.Layer = "NVK-MARK";
                    ln.Color = Aci(8);
                    ms.AppendEntity(ln);
                    tr.AddNewlyCreatedDBObject(ln, true);
                    var mt = new MText();
                    mt.SetDatabaseDefaults();
                    mt.Layer = "NVK-MARK";
                    mt.Contents = CsAnnot.SectionMark(i);
                    mt.Location = new Point3d(x, yBot, 0.0);
                    mt.TextHeight = 1.6;
                    mt.Attachment = AttachmentPoint.BottomLeft;
                    mt.Rotation = Math.PI * 0.5;
                    mt.Color = Aci(7);
                    ms.AppendEntity(mt);
                    tr.AddNewlyCreatedDBObject(mt, true);
                    n++;
                }
            }
            return n;
        }

        static bool HasBackfill(List<AxisPt> axis, SectionConfig cfg)
        {
            foreach (var p in axis)
            {
                if (p.NamedZ(NamedLine.Surface, cfg) > p.NamedZ(NamedLine.ZashTop, cfg) + 0.001)
                    return true;
            }
            return false;
        }

        static string LayerOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "NVK-EP-RAZR";
                case ProfileKind.Pesok:      return "NVK-EP-PESOK";
                case ProfileKind.Truba:      return "NVK-EP-TRUBA";
                case ProfileKind.Zashita:    return "NVK-EP-ZASH";
                case ProfileKind.Zasypka:    return "NVK-EP-ZAS";
                default: return "NVK-INS";
            }
        }

        static short ColorOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return 4;
                case ProfileKind.Pesok:      return 2;
                case ProfileKind.Truba:      return 5;
                case ProfileKind.Zashita:    return 6;
                case ProfileKind.Zasypka:    return 3;
                default: return 7;
            }
        }

        static string TitleOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "Разработка";
                case ProfileKind.Pesok:      return "Песок";
                case ProfileKind.Truba:      return "Труба (лоток)";
                case ProfileKind.Zashita:    return "Защитный слой";
                case ProfileKind.Zasypka:    return "Обратная засыпка";
                default: return kind.ToString();
            }
        }

        static void DrawKind(BlockTableRecord btr, Transaction tr, List<AxisPt> axis, SectionConfig cfg,
            ProfileKind kind, double osh, double lSc, double hSc, double ox, double oy)
        {
            var lo = LayerStack.Lower(kind);
            var hi = LayerStack.Upper(kind);
            var lowPts = Line(axis, lo, cfg, osh, lSc, hSc, ox, oy);
            var upPts = Line(axis, hi, cfg, osh, lSc, hSc, ox, oy);
            short col = ColorOf(kind);
            string layer = LayerOf(kind);

            if (kind != ProfileKind.Truba && lowPts.Count >= 2 && upPts.Count >= 2)
            {
                var closed = new List<Point2d>(lowPts);
                for (int i = upPts.Count - 1; i >= 0; i--)
                    closed.Add(upPts[i]);
                AddPline(btr, tr, closed.ToArray(), Aci(col), true, 0, layer);
                TryHatch(btr, tr, closed.ToArray(), "ANSI31", 0.8, Aci(col), layer);
            }

            AddPline(btr, tr, lowPts.ToArray(), Aci(col), false, 30, layer);
            if (kind != ProfileKind.Truba)
                AddPline(btr, tr, upPts.ToArray(), Aci(8), false, 0, layer);
            else
                AddPline(btr, tr, upPts.ToArray(), Aci(5), false, 0, layer);

            double w = axis[axis.Count - 1].Ch * lSc;
            double yTop = oy;
            foreach (var p in upPts)
                if (p.Y > yTop) yTop = p.Y;
            AddLabel(btr, tr, TitleOf(kind), ox + w * 0.5, yTop + 2.5, 2.0, layer);
        }

        static List<Point2d> Line(List<AxisPt> axis, NamedLine line, SectionConfig cfg, double osh, double lSc, double hSc, double ox, double oy)
        {
            var pts = new List<Point2d>(axis.Count);
            foreach (var p in axis)
            {
                double x = ox + p.Ch * lSc;
                double y = oy + (p.NamedZ(line, cfg) - osh) * hSc;
                pts.Add(new Point2d(x, y));
            }
            return pts;
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, Point2d[] pts, AcadColor color, bool closed, int lw, string layer)
        {
            if (pts == null || pts.Length < 2) return;
            var pl = new Polyline(pts.Length);
            pl.SetDatabaseDefaults();
            pl.Layer = layer;
            pl.Closed = closed;
            pl.Color = color;
            if (lw > 0) pl.LineWeight = (LineWeight)lw;
            for (int i = 0; i < pts.Length; i++)
                pl.AddVertexAt(i, pts[i], 0, 0, 0);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            NvkTag.Stamp(tr, pl);
        }

        static void TryHatch(BlockTableRecord btr, Transaction tr, Point2d[] pts, string pat, double patScale, AcadColor color, string layer)
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
                h.Color = color;
                btr.AppendEntity(h);
                tr.AddNewlyCreatedDBObject(h, true);
                h.SetHatchPattern(HatchPatternType.PreDefined, pat);
                h.PatternScale = patScale < 0.05 ? 0.05 : patScale;
                h.Associative = false;
                var ids = new ObjectIdCollection();
                ids.Add(pl.ObjectId);
                h.AppendLoop(HatchLoopTypes.Outermost, ids);
                h.EvaluateHatch(true);
                NvkAnno.Apply(h);
            }
            catch (Exc) { }
        }

        static void AddLabel(BlockTableRecord btr, Transaction tr, string text, double x, double y, double h, string layer)
        {
            var mt = new MText();
            mt.SetDatabaseDefaults();
            mt.Layer = layer;
            mt.Contents = text;
            mt.Location = new Point3d(x, y, 0.0);
            mt.TextHeight = h;
            mt.Attachment = AttachmentPoint.BottomCenter;
            mt.Color = Aci(7);
            btr.AppendEntity(mt);
            tr.AddNewlyCreatedDBObject(mt, true);
        }

        static AcadColor Aci(short i)
        {
            return AcadColor.FromColorIndex(ColorMethod.ByAci, i);
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord();
            ltr.Name = name;
            ltr.Color = Aci(color);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static bool IsEpLayer(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return false;
            return layer.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase);
        }

        static void EraseOld(Database db, Transaction tr, string seg)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var erase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                var br = ent as BlockReference;
                string n = null;
                if (br != null)
                {
                    try { n = br.Name; } catch (Exc) { }
                }
                if (n != null && n.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase)
                    && NvkTag.MatchesBlock(n, seg))
                    erase.Add(id);
                else if ((IsEpLayer(ent.Layer)
                          || string.Equals(ent.Layer, "NVK-MARK", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(ent.Layer, "NVK-EP-VIS", StringComparison.OrdinalIgnoreCase))
                         && NvkTag.Matches(ent, seg))
                    erase.Add(id);
                else if (string.Equals(ent.Layer, "NVK-TBL", StringComparison.OrdinalIgnoreCase)
                         && string.Equals(NvkTag.Get(ent), seg, StringComparison.OrdinalIgnoreCase))
                    erase.Add(id);
            }
            foreach (var id in erase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                ent.Erase();
            }
            bt.UpgradeOpen();
            var blkErase = new List<ObjectId>();
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsAnonymous || btr.IsLayout) continue;
                if (btr.Name != null && btr.Name.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase)
                    && NvkTag.MatchesBlock(btr.Name, seg))
                    blkErase.Add(id);
            }
            foreach (var id in blkErase)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (btr.GetBlockReferenceIds(true, true).Count == 0)
                    btr.Erase();
            }
            EraseGroups(db, tr, seg);
        }

        static void EraseGroups(Database db, Transaction tr, string seg)
        {
            try
            {
                var dict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);
                var kill = new List<ObjectId>();
                foreach (DBDictionaryEntry de in dict)
                {
                    string k = de.Key;
                    if (k == null || !k.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(seg) && k.IndexOf("-" + seg + "-", StringComparison.OrdinalIgnoreCase) < 0
                        && !k.EndsWith("-" + seg, StringComparison.OrdinalIgnoreCase))
                        continue;
                    kill.Add(de.Value);
                }
                foreach (var id in kill)
                {
                    var g = tr.GetObject(id, OpenMode.ForWrite) as Group;
                    if (g != null) g.Erase();
                }
            }
            catch (Exc) { }
        }
    }
}
