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
    static class CsDrawer
    {
        const string LayerIns = "NVK-CS";
        const string LayerTbl = "NVK-CS-TBL";

        public static string Draw(Database db, Transaction tr, Point3d origin, double scale, double mSlope, bool asBlock)
        {
            var runs = NvkSession.AllRuns();
            int nGeom = 0;
            foreach (var r in runs)
                if (r != null && r.Geoms != null) nGeom += r.Geoms.Count;
            if (nGeom == 0)
                return "[НВК] CS-DRAW: нет домена. Сначала НВК-ДАМП.";

            EnsureLayer(db, tr, LayerIns, 1);
            EnsureLayer(db, tr, LayerTbl, 7);
            EraseAllCs(db, tr);
            NvkAnno.CaptureDim(db, tr);
            NvkAnno.LogDim();

            var last0 = NvkSession.Last;
            double hw = last0 != null && last0.Cfg != null && last0.Cfg.HalfWidth > 0.5
                ? last0.Cfg.HalfWidth : 3.0;
            var tbl = NvkSession.Tbl().ForCs();
            double th = tbl.TextH;
            if (tbl.Annotative)
                th = tbl.Scaled(th, NvkSession.Cannoscale > 0.0 ? NvkSession.Cannoscale : 1000.0);
            double labW = TableLayout.LabelColW(tbl, CsAnnot.LayoutLabels());
            double colGap = CsAnnot.ColGap(hw, scale, th, labW);
            double rowGap = Math.Max(40.0 * th, scale * 15.0 + 20.0 * th);
            if (NvkSession.CsYgap > rowGap) rowGap = NvkSession.CsYgap;
            foreach (var run in runs)
            {
                if (run == null || run.Geoms == null) continue;
                double mL0 = run.Cfg != null ? run.Cfg.SlopeLeft() : mSlope;
                double mR0 = run.Cfg != null ? run.Cfg.SlopeRight() : mSlope;
                foreach (var g in run.Geoms)
                {
                    double osh = TrenchGeom.Osh(g.Elev);
                    double need = (g.Elev.Surface - osh + 0.3) * scale
                        + CsAnnot.TableHeight(g, ProfileKind.Pesok, hw, mL0, mR0, tbl, run.Cfg)
                        + 3.5 * th;
                    if (need > rowGap) rowGap = need;
                }
            }
            if (colGap < 8.0) colGap = 8.0;
            if (rowGap < 8.0) rowGap = 8.0;
            double chainLen = 0.0;
            foreach (var r in runs)
                if (r != null && r.Chain != null && r.Chain.Total > chainLen)
                    chainLen = r.Chain.Total;
            double rowWidth = chainLen * (NvkUiState.LScale > 0.0 ? NvkUiState.LScale : 1.0);
            int maxPerRow = CsAnnot.PerRow(NvkUiState.CsCols, rowWidth, colGap);
            int nStationRows = (nGeom + maxPerRow - 1) / maxPerRow;
            if (nStationRows < 1) nStationRows = 1;
            double groupStep = nStationRows * rowGap + 5.0 * th;

            int nIns = 0;
            var kinds = new[]
            {
                ProfileKind.Razrabotka,
                ProfileKind.Pesok,
                ProfileKind.Truba,
                ProfileKind.Zashita,
                ProfileKind.Zasypka
            };

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int mark0 = NvkSites.GeomOffset();
            int num = 0;
            foreach (var run in runs)
            {
                if (run == null || run.Geoms == null) continue;
                NvkSession.Last = run;
                double mL = run.Cfg != null ? run.Cfg.SlopeLeft() : mSlope;
                double mR = run.Cfg != null ? run.Cfg.SlopeRight() : mSlope;
                for (int i = 0; i < run.Geoms.Count; i++, num++)
                {
                    var g = run.Geoms[i];
                    int mark = mark0 + num;
                    foreach (var kind in kinds)
                    {
                        if (!LayerStack.IsDrawn(kind, g.Elev))
                            continue;
                        int levelIdx = (int)kind - 1;
                        if (levelIdx < 0) levelIdx = 0;
                        int col, row;
                        CsAnnot.Grid(num, maxPerRow, out col, out row);
                        Point3d ins;
                        Point3d csBase;
                        if (NvkSession.TryCs(kind, out csBase))
                            ins = new Point3d(csBase.X + col * colGap, csBase.Y - row * rowGap, 0.0);
                        else
                            ins = new Point3d(origin.X + col * colGap, origin.Y - levelIdx * groupStep - row * rowGap, 0.0);
                        if (asBlock)
                        {
                            string bname = BlockName(run.SegId, kind, i);
                            ObjectId btrId = RecreateBlock(tr, bt, bname, btr => DrawLevel(btr, tr, g, kind, scale, mL, mR, mark, 0, 0, hw, false));
                            var br = new BlockReference(ins, btrId);
                            br.Layer = LayerIns;
                            ms.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                            NvkTag.Stamp(tr, br);
                            DrawDims(ms, tr, g, kind, scale, mL, mR, ins.X, ins.Y);
                        }
                        else
                            DrawLevel(ms, tr, g, kind, scale, mL, mR, mark, ins.X, ins.Y, hw, true);
                        nIns++;
                        double osh = TrenchGeom.Osh(g.Elev);
                        double xTop = Math.Max(TrenchGeom.XTopLeft(g, mL), TrenchGeom.XTopRight(g, mR));
                        if (xTop < hw) xTop = hw;
                        double tblH = CsAnnot.TableHeight(g, kind, hw, mL, mR, tbl, run.Cfg);
                        double x0 = ins.X - xTop * scale - labW - 2.0 * th;
                        double x1 = ins.X + xTop * scale + 5.0 * th;
                        double y0 = ins.Y - tblH - 2.0 * th;
                        double y1 = ins.Y + (g.Elev.Surface - osh + 0.5) * scale + 6.0 * th;
                        NvkSession.AddCsCell(kind, mark, x0, y0, x1, y1);
                    }
                }
            }
            if (last0 != null) NvkSession.Last = last0;

            return "[НВК] CS-DRAW: станций " + nGeom
                 + (asBlock ? "  блоков " : "  групп ") + nIns
                 + "  шагX=" + colGap.ToString("0.0")
                 + "  шагY=" + rowGap.ToString("0.0")
                 + "  в ряду " + maxPerRow.ToString()
                 + (NvkUiState.CsCols > 0 ? " (NOD)" : "");
        }

        static string BlockName(string seg, ProfileKind kind, int i)
        {
            return "NVK-CS-" + (seg ?? "X") + "-L" + ((int)kind) + "-S" + i.ToString("00");
        }

        static ObjectId RecreateBlock(Transaction tr, BlockTable bt, string name, Action<BlockTableRecord> fill)
        {
            if (bt.Has(name))
            {
                var old = (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForWrite, true);
                if (!old.IsErased) old.Erase();
            }
            var btr = new BlockTableRecord();
            btr.Name = name;
            btr.Origin = Point3d.Origin;
            bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            fill(btr);
            return btr.ObjectId;
        }

        static void DrawLevel(BlockTableRecord btr, Transaction tr, SectionGeom g, ProfileKind kind, double sc, double mL, double mR, int idx, double ox, double oy, double hw, bool withDims)
        {
            var e = g.Elev;
            double osh = TrenchGeom.Osh(e);
            int level = (int)kind;
            double bL = TrenchGeom.BottomLeft(g);
            double bR = TrenchGeom.BottomRight(g);
            double xInvL = TrenchGeom.XAtLeft(g, mL, e.SandTop);
            double xInvR = TrenchGeom.XAtRight(g, mR, e.SandTop);
            double zCtr = TrenchGeom.ZCenter(g);
            short cur = 5;
            short gray = 8;

            double xTopL = TrenchGeom.XTopLeft(g, mL);
            double xTopR = TrenchGeom.XTopRight(g, mR);
            double zWallL = TrenchGeom.SurfZAt(g, -xTopL);
            double zWallR = TrenchGeom.SurfZAt(g, xTopR);
            double zL = Math.Max(e.TrenchBottom, zWallL);
            double zR = Math.Max(e.TrenchBottom, zWallR);
            bool pitUp = zL > e.TrenchBottom + 1e-6 || zR > e.TrenchBottom + 1e-6;

            if (g.InManhole)
            {
                DrawManholeShaft(btr, tr, g, sc, osh, ox, oy);
                DrawL0L6(btr, tr, g, sc, osh, ox, oy, mL, mR, level);
                DrawAnnot(btr, tr, g, kind, sc, mL, mR, idx, ox, oy, hw);
                return;
            }

            // Стенки от фактической L6, даже если L6 < L1 — иначе разрыв на сечении 1-1.
            if (Math.Abs(zWallL - e.TrenchBottom) > 1e-6 || Math.Abs(xTopL - bL) > 1e-4)
                AddSeg(btr, tr, ox + (-xTopL) * sc, oy + (zWallL - osh) * sc, ox + (-bL) * sc, oy + (e.TrenchBottom - osh) * sc, Aci(7), 0);
            if (Math.Abs(zWallR - e.TrenchBottom) > 1e-6 || Math.Abs(xTopR - bR) > 1e-4)
                AddSeg(btr, tr, ox + bR * sc, oy + (e.TrenchBottom - osh) * sc, ox + xTopR * sc, oy + (zWallR - osh) * sc, Aci(7), 0);
            if (level == 1 || level == 5)
                AddPline(btr, tr, ToDraw(new[] { P(-bL, e.TrenchBottom), P(bR, e.TrenchBottom) }, sc, osh, ox, oy), Aci(1), false, 0);

            // L1: штриховка котлована без верхней «крышки» — земли после разработки в яме нет.
            if (pitUp && level == 1)
            {
                var earth = ToDraw(TrenchGeom.EarthOutline(g, mL, mR), sc, osh, ox, oy);
                ApplyHatch(btr, tr, earth, HatchOf(ProfileKind.Razrabotka, g));
            }

            if (level >= 2)
            {
                var sand = ToDraw(Trap(bL, bR, e.TrenchBottom, xInvL, xInvR, e.SandTop), sc, osh, ox, oy);
                AddPline(btr, tr, sand, Aci(level == 2 ? cur : gray), closed: true, lw: level == 2 ? 30 : 0);
                ApplyHatch(btr, tr, sand, HatchOf(ProfileKind.Pesok, g));
            }

            Point3d pipeC = new Point3d(ox, oy + (zCtr - osh) * sc, 0.0);
            double pipeR = (e.Diam * 0.5) * sc;
            bool pipeInZash = e.ZashTop + 1e-6 >= e.Invert + e.Diam
                && e.SandTop - 1e-6 <= e.Invert;

            if (level >= 4)
            {
                var zashPoly = TrenchGeom.ZashOutline(g, mL, mR);
                if (zashPoly.Count >= 3)
                {
                    var zash = ToDraw(zashPoly, sc, osh, ox, oy);
                    AddPline(btr, tr, zash, Aci(level == 4 ? cur : gray), closed: true, lw: level == 4 ? 30 : 0);
                    var zashFill = HatchOf(ProfileKind.Zashita, g);
                    if (pipeInZash)
                        ApplyHatchHole(btr, tr, zash, pipeC, pipeR, zashFill);
                    else
                        ApplyHatch(btr, tr, zash, zashFill);
                }
                else if (level == 4)
                {
                    AddPline(btr, tr, ToDraw(new[] { P(-xInvL, e.SandTop), P(xInvR, e.SandTop) }, sc, osh, ox, oy),
                        Aci(cur), false, 30);
                }
            }

            if (level >= 3)
            {
                var cir = new Circle(pipeC, Vector3d.ZAxis, pipeR);
                cir.SetDatabaseDefaults();
                cir.Layer = LayerIns;
                cir.Color = Aci(level == 3 ? cur : gray);
                if (level == 3) cir.LineWeight = LineWeight.LineWeight030;
                btr.AppendEntity(cir);
                tr.AddNewlyCreatedDBObject(cir, true);
            }

            if (level >= 5 && e.HasBackfill)
            {
                var zas = ToDraw(TrenchGeom.BackfillDrawPoly(g, mL, mR), sc, osh, ox, oy);
                AddPline(btr, tr, zas, Aci(cur), closed: true, lw: 30);
                ApplyHatch(btr, tr, zas, HatchOf(ProfileKind.Zasypka, g));
            }

            DrawL0L6(btr, tr, g, sc, osh, ox, oy, mL, mR, level);
            if (withDims)
                DrawDims(btr, tr, g, kind, sc, mL, mR, ox, oy);
            DrawLineTags(btr, tr, g, sc, osh, ox, oy, mL, mR, level, hw);
            DrawAnnot(btr, tr, g, kind, sc, mL, mR, idx, ox, oy, hw);
        }

        static void DrawLineTags(BlockTableRecord btr, Transaction tr, SectionGeom g,
            double sc, double osh, double ox, double oy, double mL, double mR, int level, double hw)
        {
            if (!NvkUiState.ShowLineTags) return;
            var e = g.Elev;
            double th = NvkSession.Tbl().ForCs().TextH * 0.9;
            double xTop = TrenchGeom.XTopRight(g, mR);
            double z6 = TrenchGeom.SurfZAt(g, xTop);
            double z0 = TrenchGeom.SurfZAtRaw(g, xTop);
            double l0Off = Math.Abs(z0 - z6) * sc < 0.8 * th ? 0.9 * th : 0.0;
            if (g.Pavement > 1e-6)
                LabelZ(btr, tr, ox, oy, sc, osh, xTop, z0, "L0", th, l0Off);
            LabelZ(btr, tr, ox, oy, sc, osh, xTop, z6, "L6", th, 0.0);
            double xBot = TrenchGeom.BottomRight(g);
            LabelZ(btr, tr, ox, oy, sc, osh, xBot, e.TrenchBottom, "L1", th, 0.0);
            if (level >= 2)
            {
                double x2 = TrenchGeom.XAtRight(g, mR, e.SandTop);
                LabelZ(btr, tr, ox, oy, sc, osh, x2, e.SandTop, "L2", th, 0.0);
            }
            if (level >= 3)
                LabelZ(btr, tr, ox, oy, sc, osh, e.Diam * 0.5, TrenchGeom.ZCenter(g), "L3", th, 0.0);
            if (level >= 4)
            {
                double x4 = TrenchGeom.XAtRight(g, mR, e.ZashTop);
                double z4 = TrenchGeom.ZashZAt(g, x4);
                LabelZ(btr, tr, ox, oy, sc, osh, x4, z4, "L4", th, 0.0);
            }
        }

        static void LabelZ(BlockTableRecord btr, Transaction tr, double ox, double oy,
            double sc, double osh, double x, double z, string tag, double th, double yOffDraw)
        {
            if (string.IsNullOrEmpty(tag)) return;
            double px = ox + x * sc + 0.35 * th;
            double py = oy + (z - osh) * sc + yOffDraw;
            NvkText.Make(btr, tr, LayerTbl, px, py, tag, th,
                AttachmentPoint.MiddleLeft, 0.0, 8 * th, true);
        }

        static Point2d P(double x, double z) { return new Point2d(x, z); }

        static Point2d[] Trap(double xBot, double zBot, double xTop, double zTop)
        {
            return Trap(xBot, xBot, zBot, xTop, xTop, zTop);
        }

        static Point2d[] Trap(double xBotL, double xBotR, double zBot, double xTopL, double xTopR, double zTop)
        {
            return new[]
            {
                P(-xTopL, zTop), P(-xBotL, zBot), P(xBotR, zBot), P(xTopR, zTop)
            };
        }

        static Point2d[] ToDraw(Point2d[] cs, double sc, double osh, double ox, double oy)
        {
            var d = new Point2d[cs.Length];
            for (int i = 0; i < cs.Length; i++)
                d[i] = new Point2d(ox + cs[i].X * sc, oy + (cs[i].Y - osh) * sc);
            return d;
        }

        static Point2d[] ToDraw(List<CsPt> cs, double sc, double osh, double ox, double oy)
        {
            if (cs == null || cs.Count == 0) return new Point2d[0];
            var d = new Point2d[cs.Count];
            for (int i = 0; i < cs.Count; i++)
                d[i] = new Point2d(ox + cs[i].X * sc, oy + (cs[i].Z - osh) * sc);
            return d;
        }

        /// <summary>
        /// L6 синяя: разрыв в котловане только на L2–L4 (как pc-draw-surface).
        /// L1 и L5 — сплошная. L0 красный пунктир без разрыва.
        /// </summary>
        static void DrawL0L6(BlockTableRecord btr, Transaction tr, SectionGeom g, double sc, double osh, double ox, double oy, double mL, double mR, int level)
        {
            bool gapL6 = TrenchGeom.GapSurface(level);
            DrawSurfWings(btr, tr, g, false, sc, osh, ox, oy, mL, mR, gapL6, Aci(5), 18, null);
            if (g.Pavement > 1e-6)
                DrawSurfWings(btr, tr, g, true, sc, osh, ox, oy, mL, mR, false, Aci(1), 18, "DASHED");
        }

        static void DrawSurfWings(BlockTableRecord btr, Transaction tr, SectionGeom g, bool rawTin,
            double sc, double osh, double ox, double oy, double mL, double mR, bool gap, AcadColor col, int lw, string ltype)
        {
            if (!gap)
            {
                var all = TrenchGeom.SurfaceLine(g, rawTin);
                if (all.Count >= 2)
                    AddPline(btr, tr, ToDraw(all, sc, osh, ox, oy), col, false, lw, ltype, sc);
                return;
            }
            List<CsPt> left, right;
            TrenchGeom.SurfaceWings(g, rawTin, mL, mR, out left, out right);
            if (left.Count >= 2)
                AddPline(btr, tr, ToDraw(left, sc, osh, ox, oy), col, false, lw, ltype, sc);
            if (right.Count >= 2)
                AddPline(btr, tr, ToDraw(right, sc, osh, ox, oy), col, false, lw, ltype, sc);
        }

        static void DrawDims(BlockTableRecord btr, Transaction tr, SectionGeom g, ProfileKind kind, double sc, double mL, double mR, double ox, double oy)
        {
            var e = g.Elev;
            double osh = TrenchGeom.Osh(e);
            double th = NvkSession.Tbl().ForCs().TextH;
            double off = th;
            double bL = TrenchGeom.BottomLeft(g);
            double bR = TrenchGeom.BottomRight(g);
            double xTopL = TrenchGeom.XTopLeft(g, mL);
            double xTopR = TrenchGeom.XTopRight(g, mR);
            double xInvL = TrenchGeom.XAtLeft(g, mL, e.SandTop);
            double xZashL = TrenchGeom.XAtLeft(g, mL, e.ZashTop);
            double xZashR = TrenchGeom.XAtRight(g, mR, e.ZashTop);
            double zL = Math.Max(e.TrenchBottom, TrenchGeom.SurfZAt(g, -xTopL));
            double zR = Math.Max(e.TrenchBottom, TrenchGeom.SurfZAt(g, xTopR));
            int level = (int)kind;
            double dimXl = ox - xTopL * sc - off;
            double dimXr = ox + xTopR * sc + 2.0 * off;
            if (level == 1)
            {
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, -xTopL, zL), To3(ox, oy, sc, osh, -bL, e.TrenchBottom), dimXl);
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, xTopR, zR), To3(ox, oy, sc, osh, bR, e.TrenchBottom), dimXr);
            }
            else if (level == 2)
            {
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, -bL, e.TrenchBottom), To3(ox, oy, sc, osh, -xInvL, e.SandTop), ox - xInvL * sc - off);
            }
            else if (level == 4)
            {
                double z4L = TrenchGeom.ZashZAt(g, -xZashL);
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, -xInvL, e.SandTop), To3(ox, oy, sc, osh, -xZashL, z4L), ox - Math.Max(xInvL, xZashL) * sc - off);
            }
            else if (level == 5 && e.HasBackfill)
            {
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, -xTopL, zL), To3(ox, oy, sc, osh, -xZashL, e.ZashTop), dimXl);
                AddVertDim(btr, tr, To3(ox, oy, sc, osh, xTopR, zR), To3(ox, oy, sc, osh, xZashR, e.ZashTop), dimXr);
            }
        }

        static Point3d To3(double ox, double oy, double sc, double osh, double x, double z)
        {
            return new Point3d(ox + x * sc, oy + (z - osh) * sc, 0.0);
        }

        static void AddVertDim(BlockTableRecord btr, Transaction tr, Point3d p1, Point3d p2, double dimX)
        {
            if (Math.Abs(p1.Y - p2.Y) < 1e-4) return;
            try
            {
                var dim = new RotatedDimension();
                dim.SetDatabaseDefaults();
                try { dim.DimensionStyle = btr.Database.Dimstyle; } catch (Exc) { }
                dim.Layer = LayerIns;
                dim.XLine1Point = p1;
                dim.XLine2Point = p2;
                dim.DimLinePoint = new Point3d(dimX, 0.5 * (p1.Y + p2.Y), 0.0);
                dim.Rotation = Math.PI * 0.5;
                btr.AppendEntity(dim);
                tr.AddNewlyCreatedDBObject(dim, true);
                NvkAnno.ApplyDim(dim, tr);
                NvkTag.Stamp(tr, dim);
            }
            catch (Exc) { }
        }

        static void DrawAnnot(BlockTableRecord btr, Transaction tr, SectionGeom g, ProfileKind kind,
            double sc, double mL, double mR, int idx, double ox, double oy, double hw)
        {
            var e = g.Elev;
            double osh = TrenchGeom.Osh(e);
            DrawFrame(btr, tr, g, sc, osh, ox, oy, hw);
            var tbl = NvkSession.Tbl().ForCs();
            double th = tbl.TextH;
            double topY = oy + (e.Surface + g.Pavement - osh) * sc + 2.5 * th;
            NvkText.Make(btr, tr, LayerTbl, ox, topY + th, CsAnnot.Header(idx, kind, e.Station),
                th, AttachmentPoint.MiddleCenter, 0.0, 20.0 * th, false);
            int lv = (int)kind;
            if (!g.InManhole && (lv == 3 || lv == 4))
                DrawPipeLabel(btr, tr, g, sc, osh, ox, oy);
            DrawTable(btr, tr, g, kind, sc, mL, mR, ox, oy, hw);
        }

        static void DrawManholeShaft(BlockTableRecord btr, Transaction tr, SectionGeom g,
            double sc, double osh, double ox, double oy)
        {
            var e = g.Elev;
            double hw = g.ManholeHalfW > 0.05 ? g.ManholeHalfW : 0.5;
            double zBot = e.TrenchBottom;
            double zTop = e.Surface;
            if (zTop < zBot + 0.05) zTop = zBot + 0.05;
            var box = ToDraw(new[]
            {
                P(-hw, zBot), P(hw, zBot), P(hw, zTop), P(-hw, zTop)
            }, sc, osh, ox, oy);
            AddPline(btr, tr, box, Aci(7), closed: true, lw: 30);
        }

        static void DrawFrame(BlockTableRecord btr, Transaction tr, SectionGeom g, double sc, double osh, double ox, double oy, double hw)
        {
            var e = g.Elev;
            double zMax = e.Surface + g.Pavement + 0.3;
            double xL = ox - hw * sc;
            double xR = ox + hw * sc;
            double y0 = oy;
            double yTop = oy + (zMax - osh) * sc;
            double yBot = oy + (e.TrenchBottom - osh) * sc;
            AddSeg(btr, tr, xL, y0, xL, yTop, Aci(7), 0);
            AddSeg(btr, tr, xR, y0, xR, yTop, Aci(7), 0);
            TryCenter(btr, tr, ox, yBot, ox, yTop);
            double th = NvkSession.Tbl().ForCs().TextH;
            double tick = 0.25 * th;
            double z = Math.Ceiling(osh);
            while (z <= zMax + 1e-6)
            {
                double y = oy + (z - osh) * sc;
                AddSeg(btr, tr, xL - tick, y, xL + tick, y, Aci(3), 0);
                if (y > y0 + 0.6 * th)
                    NvkText.Make(btr, tr, LayerTbl, xL - 0.3 * th, y, TableLayout.Rtos(z, 2),
                        th, AttachmentPoint.MiddleRight, 0.0, 6.0 * th, false);
                z += 1.0;
            }
        }

        static void DrawPipeLabel(BlockTableRecord btr, Transaction tr, SectionGeom g, double sc, double osh, double ox, double oy)
        {
            double cx = ox;
            double cy = oy + (TrenchGeom.ZCenter(g) - osh) * sc;
            double reach = (Math.Max(TrenchGeom.BottomLeft(g), TrenchGeom.BottomRight(g)) + 0.5) * sc * 0.7071;
            double ax = cx + reach;
            double ay = cy + reach;
            AddSeg(btr, tr, cx, cy, ax, ay, Aci(5), 0);
            double th = NvkSession.Tbl().ForCs().TextH;
            NvkText.Make(btr, tr, LayerTbl, ax + 0.3 * th, ay, CsAnnot.DiamText(g.Elev.Diam),
                th, AttachmentPoint.BottomLeft, 0.0, 6.0 * th, true);
        }

        static void DrawTable(BlockTableRecord btr, Transaction tr, SectionGeom g, ProfileKind kind,
            double sc, double mL, double mR, double ox, double oy, double hw)
        {
            var tbl = NvkSession.Tbl().ForCs();
            var cfg = NvkSession.Last != null ? NvkSession.Last.Cfg : null;
            var rows = CsAnnot.Rows(g, kind, hw, mL, mR, tbl, cfg);
            var gaps = CsAnnot.Gaps(g, kind, hw, mL, mR);
            double xInL = CsAnnot.XInnerLeft(g, kind, mL);
            double xInR = CsAnnot.XInnerRight(g, kind, mR);
            if (xInL > hw) xInL = hw;
            if (xInR > hw) xInR = hw;
            double th = tbl.TextH;
            int zPrec = kind == ProfileKind.Truba ? 3 : tbl.RoundTo;
            var zs = new List<double>();
            var labels = new List<string>();
            foreach (var r in rows)
            {
                labels.Add(r.Label);
                if (r.C1.HasValue) zs.Add(r.C1.Value);
                if (r.C2.HasValue) zs.Add(r.C2.Value);
                if (r.C3.HasValue) zs.Add(r.C3.Value);
                if (r.C4.HasValue) zs.Add(r.C4.Value);
            }
            labels.Add("Расстояние, м");
            int maxH = TableLayout.MaxChars(zs, zPrec, 7);
            int maxL = TableLayout.MaxChars(gaps, 2, 4);
            double hH = TableLayout.RowHeight(TableRowKind.Height, th, maxH, maxL);
            double hL = TableLayout.RowHeight(TableRowKind.Length, th, maxH, maxL);
            double labW = TableLayout.LabelColW(tbl, labels);

            double dc1 = ox - hw * sc;
            double dc2 = ox - xInL * sc;
            double dc3 = ox + xInR * sc;
            double dc4 = ox + hw * sc;
            double xTopL = Math.Min(hw, TrenchGeom.XTopLeft(g, mL));
            double xTopR = Math.Min(hw, TrenchGeom.XTopRight(g, mR));
            double dc2Surf = ox - xTopL * sc;
            double dc3Surf = ox + xTopR * sc;
            double dataL = dc1 - th;
            double dataR = dc4 + th;
            double xLbl = dataL - labW;
            double y0 = oy;
            double tblH = CsAnnot.TableHeight(g, kind, hw, mL, mR, tbl, cfg);
            double yBot = y0 - tblH;

            var by = ByLayer();
            AddSeg(btr, tr, xLbl, y0, dataR, y0, by, 0, LayerTbl);
            AddSeg(btr, tr, xLbl, yBot, dataR, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, xLbl, y0, xLbl, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, dataL, y0, dataL, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, dataR, y0, dataR, yBot, by, 0, LayerTbl);
            double paper = NvkUiState.VpScaleCs >= 1.0 ? NvkUiState.VpScaleCs
                : (NvkSession.Cannoscale > 0.0 ? NvkSession.Cannoscale : 1000.0);
            double den = TableLayout.LegendDenom(paper, sc);
            var cfgA = NvkSession.Last != null ? NvkSession.Last.Cfg : null;
            string areaLabel = CsAnnot.AreaLabel(kind, g.Elev.Diam, g, cfgA);
            NvkText.Make(btr, tr, LayerTbl, xLbl, y0,
                TableLayout.ScaleLegend(den, den, TrenchGeom.Osh(g.Elev), SectionArea.For(g, kind, mL, mR), areaLabel),
                0.7 * th, AttachmentPoint.BottomLeft, 0.0, 20.0 * th, false);

            double y = y0;
            foreach (var row in rows)
            {
                double rh = hH;
                y -= rh;
                AddSeg(btr, tr, xLbl, y, dataR, y, by, 0, LayerTbl);
                NvkText.Make(btr, tr, LayerTbl, 0.5 * (xLbl + dataL), y + 0.5 * rh, row.Label, th, AttachmentPoint.MiddleCenter, 0.0);
                if (!string.IsNullOrEmpty(row.Center))
                    NvkText.Make(btr, tr, LayerTbl, 0.5 * (dataL + dataR), y + 0.5 * rh, row.Center, th, AttachmentPoint.MiddleCenter, 0.0);
                else
                {
                    PutZ(btr, tr, row.C1, dc1, y, rh, zPrec, th);
                    PutZ(btr, tr, row.C2, row.IsSurf ? dc2Surf : dc2, y, rh, zPrec, th);
                    PutZ(btr, tr, row.C3, row.IsSurf ? dc3Surf : dc3, y, rh, zPrec, th);
                    PutZ(btr, tr, row.C4, dc4, y, rh, zPrec, th);
                }
            }

            // Вертикали только в строке «Расстояние» — через отметки не проводим (как pc-draw-cross-table).
            AddSeg(btr, tr, dc1, y, dc1, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, dc2, y, dc2, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, dc3, y, dc3, yBot, by, 0, LayerTbl);
            AddSeg(btr, tr, dc4, y, dc4, yBot, by, 0, LayerTbl);
            double midL = 0.5 * (y + yBot);
            NvkText.Make(btr, tr, LayerTbl, 0.5 * (xLbl + dataL), midL, "Расстояние, м", th, AttachmentPoint.MiddleCenter, 0.0);
            int lenPrec = tbl.RoundTo > 0 ? tbl.RoundTo : 2;
            PutLen(btr, tr, gaps[0], 0.5 * (dc1 + dc2), midL, gaps[0] * sc, th, lenPrec);
            PutLen(btr, tr, gaps[1], 0.5 * (dc2 + dc3), midL, gaps[1] * sc, th, lenPrec);
            PutLen(btr, tr, gaps[2], 0.5 * (dc3 + dc4), midL, gaps[2] * sc, th, lenPrec);
        }

        static void PutZ(BlockTableRecord btr, Transaction tr, double? z, double x, double y, double rowH, int prec, double th)
        {
            if (!z.HasValue) return;
            NvkText.Make(btr, tr, LayerTbl, x, y + 0.5 * rowH, TableLayout.Rtos(z.Value, prec),
                th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
        }

        static void PutLen(BlockTableRecord btr, Transaction tr, double meters, double x, double y, double widthDraw, double th, int prec)
        {
            double ang = TableLayout.RotateLength(widthDraw, th) ? Math.PI * 0.5 : 0.0;
            NvkText.Make(btr, tr, LayerTbl, x, y, TableLayout.Rtos(meters, prec), th, AttachmentPoint.MiddleCenter, ang);
        }

        static AcadColor ByLayer()
        {
            return AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
        }

        static void AddSeg(BlockTableRecord btr, Transaction tr, double x1, double y1, double x2, double y2, AcadColor color, int lw)
        {
            AddSeg(btr, tr, x1, y1, x2, y2, color, lw, LayerIns);
        }

        static void AddSeg(BlockTableRecord btr, Transaction tr, double x1, double y1, double x2, double y2, AcadColor color, int lw, string layer)
        {
            AddPline(btr, tr, new[] { new Point2d(x1, y1), new Point2d(x2, y2) }, color, false, lw, null, 1.0, layer);
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, Point2d[] pts, AcadColor color, bool closed, int lw)
        {
            AddPline(btr, tr, pts, color, closed, lw, null, 1.0);
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, Point2d[] pts, AcadColor color, bool closed, int lw, string ltype, double sc)
        {
            AddPline(btr, tr, pts, color, closed, lw, ltype, sc, LayerIns);
        }

        static void AddPline(BlockTableRecord btr, Transaction tr, Point2d[] pts, AcadColor color, bool closed, int lw, string ltype, double sc, string layer)
        {
            if (pts == null || pts.Length < 2) return;
            var pl = new Polyline(pts.Length);
            pl.SetDatabaseDefaults();
            pl.Layer = string.IsNullOrEmpty(layer) ? LayerIns : layer;
            pl.Closed = closed;
            pl.Color = color;
            if (lw > 0) pl.LineWeight = (LineWeight)lw;
            pl.Plinegen = true;
            pl.LinetypeScale = 1.0;
            if (!string.IsNullOrEmpty(ltype) && !string.Equals(ltype, "Continuous", StringComparison.OrdinalIgnoreCase))
            {
                EnsureLinetype(btr.Database, tr, ltype);
                try { pl.Linetype = ltype; } catch (Exc) { }
            }
            for (int i = 0; i < pts.Length; i++)
                pl.AddVertexAt(i, pts[i], 0, 0, 0);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            NvkTag.Stamp(tr, pl);
        }

        static void EnsureLinetype(Database db, Transaction tr, string name)
        {
            if (db == null || string.IsNullOrEmpty(name)) return;
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            try { db.LoadLineTypeFile(name, "acad.lin"); } catch (Exc) { }
            lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            try { db.LoadLineTypeFile(name, "acadiso.lin"); } catch (Exc) { }
            lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (!lt.Has(name) && string.Equals(name, "DASHED", StringComparison.OrdinalIgnoreCase))
            {
                try { db.LoadLineTypeFile("ACAD_ISO02W100", "acadiso.lin"); } catch (Exc) { }
            }
        }

        static void TryCenter(BlockTableRecord btr, Transaction tr, double x1, double y1, double x2, double y2)
        {
            try
            {
                var db = btr.Database;
                var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (!lt.Has("CENTER"))
                {
                    try { db.LoadLineTypeFile("CENTER", "acad.lin"); } catch (Exc) { }
                    try { if (!lt.Has("CENTER")) db.LoadLineTypeFile("CENTER", "acadiso.lin"); } catch (Exc) { }
                }
                var ln = new Line(new Point3d(x1, y1, 0.0), new Point3d(x2, y2, 0.0));
                ln.SetDatabaseDefaults();
                ln.Layer = LayerIns;
                ln.Color = Aci(1);
                ln.LinetypeScale = 1.0;
                if (lt.Has("CENTER")) ln.Linetype = "CENTER";
                btr.AppendEntity(ln);
                tr.AddNewlyCreatedDBObject(ln, true);
            }
            catch (Exc)
            {
                AddSeg(btr, tr, x1, y1, x2, y2, Aci(1), 0);
            }
        }

        static FillStyle HatchOf(ProfileKind kind, SectionGeom g)
        {
            if (kind == ProfileKind.Zasypka)
            {
                var sh = NvkSession.Sheet(kind);
                return HatchPat.ForBackfill(
                    sh,
                    g != null ? g.ZoneType : null,
                    NvkSession.Last != null ? NvkSession.Last.Cfg : null);
            }
            if (kind == ProfileKind.Razrabotka)
                return HatchPat.ForKind(kind, NvkSession.L1, null);
            return HatchPat.ForKind(kind, NvkSession.L1, NvkSession.Sheet(kind));
        }

        static void ApplyHatch(BlockTableRecord btr, Transaction tr, Point2d[] pts, FillStyle fill)
        {
            if (fill == null || string.IsNullOrEmpty(fill.Pattern)) return;
            TryHatch(btr, tr, pts, fill.Pattern, HatchPat.Scale(fill), CadColor.Of(fill));
        }

        static void ApplyHatchHole(BlockTableRecord btr, Transaction tr, Point2d[] pts,
            Point3d center, double r, FillStyle fill)
        {
            if (fill == null || string.IsNullOrEmpty(fill.Pattern)) return;
            TryHatchHole(btr, tr, pts, center, r, fill.Pattern, HatchPat.Scale(fill), CadColor.Of(fill));
        }

        static void TryHatchHole(BlockTableRecord btr, Transaction tr, Point2d[] pts, Point3d center, double r, string pat, double patScale, AcadColor color)
        {
            if (pts == null || pts.Length < 3 || r < 1e-6)
            {
                TryHatch(btr, tr, pts, pat, patScale, color);
                return;
            }
            try
            {
                var pl = new Polyline(pts.Length);
                pl.SetDatabaseDefaults();
                pl.Layer = LayerIns;
                pl.Closed = true;
                pl.Visible = false;
                for (int i = 0; i < pts.Length; i++)
                    pl.AddVertexAt(i, pts[i], 0, 0, 0);
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                var cir = new Circle(center, Vector3d.ZAxis, r);
                cir.SetDatabaseDefaults();
                cir.Layer = LayerIns;
                cir.Visible = false;
                btr.AppendEntity(cir);
                tr.AddNewlyCreatedDBObject(cir, true);
                var h = new Hatch();
                h.SetDatabaseDefaults();
                h.Layer = LayerIns;
                h.Color = color;
                btr.AppendEntity(h);
                tr.AddNewlyCreatedDBObject(h, true);
                NvkAnno.HatchStart(h);
                try { h.SetHatchPattern(HatchPatternType.PreDefined, pat); }
                catch (Exc) { h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31"); }
                h.PatternScale = patScale < 0.001 ? 0.001 : patScale;
                h.Associative = false;
                var outer = new ObjectIdCollection();
                outer.Add(pl.ObjectId);
                h.AppendLoop(HatchLoopTypes.Outermost, outer);
                var inner = new ObjectIdCollection();
                inner.Add(cir.ObjectId);
                h.AppendLoop(HatchLoopTypes.Default, inner);
                h.EvaluateHatch(true);
                NvkAnno.HatchEnd(h, patScale);
            }
            catch (Exc)
            {
                TryHatch(btr, tr, pts, pat, patScale, color);
            }
        }

        static void TryHatch(BlockTableRecord btr, Transaction tr, Point2d[] pts, string pat, double patScale, AcadColor color)
        {
            if (pts == null || pts.Length < 3) return;
            try
            {
                var pl = new Polyline(pts.Length);
                pl.SetDatabaseDefaults();
                pl.Layer = LayerIns;
                pl.Closed = true;
                pl.Visible = false;
                for (int i = 0; i < pts.Length; i++)
                    pl.AddVertexAt(i, pts[i], 0, 0, 0);
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                var h = new Hatch();
                h.SetDatabaseDefaults();
                h.Layer = LayerIns;
                h.Color = color;
                btr.AppendEntity(h);
                tr.AddNewlyCreatedDBObject(h, true);
                NvkAnno.HatchStart(h);
                h.SetHatchPattern(HatchPatternType.PreDefined, pat);
                h.PatternScale = patScale < 0.001 ? 0.001 : patScale;
                h.Associative = false;
                var ids = new ObjectIdCollection();
                ids.Add(pl.ObjectId);
                h.AppendLoop(HatchLoopTypes.Outermost, ids);
                h.EvaluateHatch(true);
                NvkAnno.HatchEnd(h, patScale);
            }
            catch (Exc)
            {
                // нет паттерна DOTS/ANSI31 в чертеже — оставляем контур
            }
        }

        static void AddLabel(BlockTableRecord btr, Transaction tr, string text, double x, double y, double h)
        {
            NvkText.Make(btr, tr, LayerTbl, x, y, text, h, AttachmentPoint.BottomCenter, 0.0);
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

        static void EraseAllCs(Database db, Transaction tr)
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
                string seg = NvkSites.CurrentId;
                if (n != null && n.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase)
                    && NvkTag.MatchesBlock(n, seg))
                    erase.Add(id);
                else if (string.Equals(ent.Layer, LayerIns, StringComparison.OrdinalIgnoreCase)
                         && NvkTag.Matches(ent, seg))
                    erase.Add(id);
                else if (string.Equals(ent.Layer, LayerTbl, StringComparison.OrdinalIgnoreCase)
                         && (NvkTag.Matches(ent, seg) || string.IsNullOrEmpty(NvkTag.Get(ent))))
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
                if (btr.Name != null && btr.Name.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase)
                    && NvkTag.MatchesBlock(btr.Name, NvkSites.CurrentId))
                    blkErase.Add(id);
            }
            foreach (var id in blkErase)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (btr.GetBlockReferenceIds(true, true).Count == 0)
                    btr.Erase();
            }
        }
    }
}
