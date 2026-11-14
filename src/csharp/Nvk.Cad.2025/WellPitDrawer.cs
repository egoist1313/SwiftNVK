using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Nvk.Core;
using CivilDb = Autodesk.Civil.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using Exc = System.Exception;

[assembly: CommandClass(typeof(Nvk.Cad.WellPitDrawer))]

namespace Nvk.Cad
{
    sealed class WellCsDraw
    {
        public SectionGeom Geom;
        public string Name;
        public string Handle;
        public double ShaftR;
        public double Slope;
    }

    public sealed class WellPitDrawer
    {
        const string LayerPit = "NVK-MH-PIT";
        const string LayerBerm = "NVK-MH-BERM";
        const string LayerSlope = "NVK-MH-SLOPE";
        const string LayerPipeC = "NVK-MH-PIPE-C";
        const string LayerPipeR = "NVK-MH-PIPE-R";
        const string LayerTxt = "NVK-MH-TXT";

        [CommandMethod("НВК-КОЛОДЦЫ", CommandFlags.UsePickSet)]
        public void RunRu() { Run(false); }

        [CommandMethod("NVKWELLS", CommandFlags.UsePickSet)]
        public void RunEn() { Run(false); }

        [CommandMethod("НВК-КОЛОДЦЫ-ОБНОВИТЬ")]
        public void UpdateRu() { Run(true); }

        [CommandMethod("NVKWELLSUPD")]
        public void UpdateEn() { Run(true); }

        [CommandMethod("НВК-КОЛОДЦЫ-ОЧИСТИТЬ")]
        public void CleanRu() { Clean(); }

        [CommandMethod("NVKWELLSCLEAN")]
        public void CleanEn() { Clean(); }

        public static void Run()
        {
            Run(false);
        }

        public static void Run(bool keepRot)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            NvkPalette.Flush();
            var ed = doc.Editor;
            var cfg = CfgFromUi();
            var csItems = new List<WellCsDraw>();
            int nOk = 0, nSkip = 0, nKept = 0;
            List<ObjectId> ids = null;
            var kept = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            if (!keepRot)
            {
                ids = PickWells(ed);
                if (ids == null || ids.Count == 0)
                {
                    ed.WriteMessage("\n[НВК] колодцы не выбраны.");
                    return;
                }
            }
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    NvkLayers.EnsureAll(doc.Database);
                    EnsureLayers(doc.Database, tr);
                    if (keepRot)
                    {
                        kept = ReadPitAxes(doc.Database, tr);
                        ids = StructuresFromKept(doc.Database, tr, kept);
                        if (ids == null || ids.Count == 0)
                        {
                            ed.WriteMessage("\n[НВК] нет квадратов дна. Сначала Построить, потом крути и Обновить.");
                            tr.Commit();
                            return;
                        }
                    }
                    ErasePlan(doc.Database, tr);
                    ObjectId surfId = SurfaceId(doc.Database, tr);
                    var ms = Ms(doc.Database, tr);
                    var items = new List<WellItem>();
                    foreach (var id in ids)
                    {
                        var st = tr.GetObject(id, OpenMode.ForRead) as CivilDb.Structure;
                        if (st == null)
                        {
                            nSkip++;
                            continue;
                        }
                        var well = WellReader.Read(tr, st);
                        if (well == null)
                        {
                            nSkip++;
                            continue;
                        }
                        string h = well.Handle ?? id.Handle.ToString();
                        double[] rot;
                        if (kept.TryGetValue(h, out rot) && rot != null && rot.Length >= 2)
                        {
                            well.AxisX = rot[0];
                            well.AxisY = rot[1];
                            nKept++;
                        }
                        Func<double, double, double> tin = (x, y) =>
                        {
                            if (surfId.IsNull) return well.Rim;
                            var z = SurfaceSampler.ElevationAt(tr, surfId, x, y);
                            return z.HasValue ? z.Value : well.Rim;
                        };
                        double zHint = well.Rim;
                        if (!surfId.IsNull)
                        {
                            var zc = SurfaceSampler.ElevationAt(tr, surfId, well.X, well.Y);
                            if (zc.HasValue) zHint = zc.Value;
                        }
                        double half = WellPitGeom.BottomHalf(well, cfg);
                        double pad = cfg.SlopeM < 1e-9
                            ? 1.0
                            : Math.Max(8.0, cfg.SlopeM * (Math.Max(0.0, zHint - well.Sump) + 8.0));
                        if (pad > 80.0) pad = 80.0;
                        var tris = SurfaceSampler.TrianglesInBox(tr, surfId,
                            well.X - half - pad, well.Y - half - pad,
                            well.X + half + pad, well.Y + half + pad);
                        var shape = WellPitGeom.Build(well, cfg, tin, tris);
                        items.Add(new WellItem
                        {
                            W = well,
                            S = shape,
                            Cfg = cfg,
                            Tin = tin,
                            ZHint = zHint,
                            Handle = h
                        });
                        var geom = MakeGeom(well, shape, cfg, tin);
                        csItems.Add(new WellCsDraw
                        {
                            Geom = geom,
                            Name = well.Name,
                            Handle = h,
                            ShaftR = WellPitGeom.Radius(well),
                            Slope = cfg.SlopeM
                        });
                        double az = Math.Atan2(shape.Ay, shape.Ax) * 180.0 / Math.PI;
                        if (az < 0.0) az += 360.0;
                        string axisMsg = "[НВК] колодец " + (well.Name ?? "?")
                            + "  ось " + az.ToString("0.0") + "°"
                            + "  труб " + well.PipeCount
                            + (keepRot ? "  (с квадрата)" : "");
                        NvkLog.Write(axisMsg
                            + "  Øтр=" + well.PipeDiam.ToString("0.000")
                            + "  Øк=" + well.Diam.ToString("0.000")
                            + "  отстойник=" + well.Sump.ToString("0.00")
                            + "  TIN тр=" + tris.Count
                            + "  бровка=" + shape.Berm.Count);
                        ed.WriteMessage("\n" + axisMsg);
                        nOk++;
                    }
                    var groups = ClusterItems(items);
                    var batch = new PipeBatch();
                    for (int g = 0; g < groups.Count; g++)
                    {
                        var grp = groups[g];
                        var shape = ShapeOfGroup(grp, cfg, surfId, tr);
                        DrawPit(ms, tr, grp, shape);
                        for (int i = 0; i < grp.Count; i++)
                            DrawName(ms, tr, grp[i].W, grp[i].S != null ? grp[i].S : shape);
                        var berm = shape != null && shape.Berm != null && shape.Berm.Count >= 3
                            ? shape.Berm
                            : (shape != null ? shape.Outer : null);
                        for (int i = 0; i < grp.Count; i++)
                            CollectPipes(batch, grp[i].W, berm);
                    }
                    DrawPipeBatch(ms, tr, batch);
                    tr.Commit();
                }
            }
            catch (Exc ex)
            {
                ed.WriteMessage("\n[НВК] колодцы: " + ex.Message);
                NvkLog.Write("[НВК] колодцы: " + ex.Message);
                return;
            }
            if (csItems.Count > 0 && !keepRot && !AskCsPoint(ed))
            {
                ed.WriteMessage("\n[НВК] план колодцев " + nOk + " шт. Сечения отменены.");
                return;
            }
            if (csItems.Count > 0 && keepRot && !NvkSession.MhCsInsHas && !AskCsPoint(ed))
            {
                ed.WriteMessage("\n[НВК] план обновлён. Сечения отменены.");
                return;
            }
            if (csItems.Count > 0)
            {
                try
                {
                    using (doc.LockDocument())
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        double sc = NvkUiState.CsScale > 0.0 ? NvkUiState.CsScale : 1.0;
                        string msg = CsDrawer.DrawWellPits(doc.Database, tr, csItems, NvkSession.MhCsIns, sc);
                        tr.Commit();
                        NvkLog.Write(msg);
                        ed.WriteMessage("\n" + msg);
                    }
                }
                catch (Exc ex)
                {
                    ed.WriteMessage("\n[НВК] сечения колодцев: " + ex.Message);
                    NvkLog.Write("[НВК] сечения колодцев: " + ex.Message);
                }
            }
            string surf = string.IsNullOrEmpty(NvkUiState.WellSurfName)
                ? (string.IsNullOrEmpty(NvkUiState.DesignName) ? "Rim" : NvkUiState.DesignName)
                : NvkUiState.WellSurfName;
            ed.WriteMessage("\n[НВК] колодцы: план " + nOk + " шт.  пропуск " + nSkip
                + (nKept > 0 ? "  поворот с квадрата " + nKept : "")
                + "  TIN=" + surf
                + "  откос m=" + cfg.SlopeM.ToString("0.###"));
            NvkPalette.SetStatus("Колодцы: " + nOk + " шт.");
        }

        public static void Clean()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            int n = 0;
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                n = ErasePlan(doc.Database, tr);
                n += EraseCs(doc.Database, tr);
                tr.Commit();
            }
            doc.Editor.WriteMessage("\n[НВК] колодцы: снято " + n + " объектов.");
        }

        static bool AskCsPoint(Editor ed)
        {
            if (NvkSession.MhCsInsHas) return true;
            var p = ed.GetPoint("\n[НВК] точка сечений колодцев: ");
            if (p.Status != PromptStatus.OK) return false;
            NvkSession.MhCsIns = p.Value;
            NvkSession.MhCsInsHas = true;
            return true;
        }

        static WellPitCfg CfgFromUi()
        {
            return new WellPitCfg
            {
                TrenchGap = Math.Max(0.0, NvkUiState.WellTrenchGap),
                SlopeM = Math.Max(0.0, NvkUiState.WellSlopeM),
                Bedding = Math.Max(0.0, NvkUiState.WellBedding)
            };
        }

        static SectionGeom MakeGeom(WellPitInput well, WellPitShape shape, WellPitCfg cfg,
            Func<double, double, double> tin)
        {
            double diam = well.PipeDiam > 0.05 ? well.PipeDiam : 0.20;
            var hit = new StationHit
            {
                X = well.X,
                Y = well.Y,
                Invert = well.Sump,
                Diam = diam,
                Nx = shape.Px,
                Ny = shape.Py,
                Station = 0.0
            };
            var scfg = new SectionConfig
            {
                Bedding = 0.0,
                ThkZash = 0.0,
                SlopeM = cfg.SlopeM,
                SlopeL = cfg.SlopeM,
                SlopeR = cfg.SlopeM,
                TrenchLeft = shape.AcrossHalf,
                TrenchRight = shape.AcrossHalf,
                HalfWidth = Math.Max(shape.BermAcross + 1.0, 3.0),
                UsePipeWall = false
            };
            double zSurf = tin != null ? tin(well.X, well.Y) : well.Rim;
            if (double.IsNaN(zSurf) || zSurf < 1.0) zSurf = well.Rim;
            var g = SectionGeom.From(hit, scfg, zSurf, 0.0);
            g.ManholeHalfW = WellPitGeom.Radius(well);
            double hw = scfg.HalfWidth;
            var samples = new List<SurfPt>();
            for (double t = -hw; t <= hw + 1e-9; t += 0.2)
            {
                double sx = well.X + shape.Px * t;
                double sy = well.Y + shape.Py * t;
                double z = tin != null ? tin(sx, sy) : zSurf;
                if (double.IsNaN(z) || z < 1.0) z = zSurf;
                samples.Add(new SurfPt(t, z));
            }
            g.SurfPts = samples;
            return g;
        }

        static List<ObjectId> PickWells(Editor ed)
        {
            var ids = Collect(ed.SelectImplied());
            if (ids.Count == 0)
            {
                var pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n[НВК] Выбери колодцы (AECC_STRUCTURE): ";
                var filter = new SelectionFilter(new[] { new TypedValue(0, "AECC_STRUCTURE") });
                ids = Collect(ed.GetSelection(pso, filter));
            }
            return ids;
        }

        static List<ObjectId> Collect(PromptSelectionResult sel)
        {
            var ids = new List<ObjectId>();
            if (sel == null || sel.Status != PromptStatus.OK || sel.Value == null)
                return ids;
            foreach (SelectedObject so in sel.Value)
            {
                if (so == null) continue;
                try
                {
                    string dxf = so.ObjectId.ObjectClass.DxfName;
                    if (dxf != null && dxf.IndexOf("STRUCTURE", StringComparison.OrdinalIgnoreCase) >= 0)
                        ids.Add(so.ObjectId);
                }
                catch (Exc) { }
            }
            return ids;
        }

        static ObjectId SurfaceId(Database db, Transaction tr)
        {
            string h = NvkUiState.WellSurfHandle;
            if (string.IsNullOrWhiteSpace(h)) h = NvkUiState.DesignHandle;
            return SurfaceSampler.ByHandle(db, h);
        }

        static Dictionary<string, double[]> ReadPitAxes(Database db, Transaction tr)
        {
            var map = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (!string.Equals(ent.Layer, LayerPit, StringComparison.OrdinalIgnoreCase)) continue;
                string raw = NvkTag.Get1000(ent, "MH=");
                if (string.IsNullOrEmpty(raw)) continue;
                var xs = new List<double>();
                var ys = new List<double>();
                var pl2 = ent as Polyline;
                int nVert = 0;
                if (pl2 != null && pl2.NumberOfVertices >= 4)
                {
                    nVert = pl2.NumberOfVertices;
                    int n = nVert == 4 ? 4 : 0;
                    for (int i = 0; i < n; i++)
                    {
                        var p = pl2.GetPoint2dAt(i);
                        xs.Add(p.X);
                        ys.Add(p.Y);
                    }
                }
                else
                {
                    var pl3 = ent as Polyline3d;
                    if (pl3 == null) continue;
                    foreach (ObjectId vid in pl3)
                    {
                        var v = tr.GetObject(vid, OpenMode.ForRead) as PolylineVertex3d;
                        if (v == null) continue;
                        xs.Add(v.Position.X);
                        ys.Add(v.Position.Y);
                    }
                    nVert = xs.Count;
                    if (nVert != 4) xs.Clear();
                }
                double[] rot = null;
                if (xs.Count == 4)
                {
                    double ax, ay;
                    WellPitGeom.AxisFromRect(xs, ys, out ax, out ay);
                    rot = new[] { ax, ay };
                }
                var parts = raw.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string h = parts[i].Trim();
                    if (h.Length == 0) continue;
                    if (!map.ContainsKey(h) || rot != null) map[h] = rot;
                }
            }
            return map;
        }

        static List<ObjectId> StructuresFromKept(Database db, Transaction tr, Dictionary<string, double[]> kept)
        {
            var ids = new List<ObjectId>();
            if (kept == null) return ids;
            foreach (var h in kept.Keys)
            {
                try
                {
                    var handle = new Handle(Convert.ToInt64(h, 16));
                    ObjectId id;
                    if (!db.TryGetObjectId(handle, out id) || id.IsNull) continue;
                    var st = tr.GetObject(id, OpenMode.ForRead) as CivilDb.Structure;
                    if (st != null) ids.Add(id);
                }
                catch (Exc) { }
            }
            return ids;
        }

        sealed class WellItem
        {
            public WellPitInput W;
            public WellPitShape S;
            public WellPitCfg Cfg;
            public Func<double, double, double> Tin;
            public double ZHint;
            public string Handle;
        }

        static string HandlesOf(List<WellItem> grp)
        {
            if (grp == null || grp.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < grp.Count; i++)
            {
                string h = grp[i] != null ? grp[i].Handle : null;
                if (string.IsNullOrEmpty(h) && grp[i] != null && grp[i].W != null)
                    h = grp[i].W.Handle;
                if (string.IsNullOrEmpty(h)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(h);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        static void DrawPit(BlockTableRecord ms, Transaction tr, List<WellItem> grp, WellPitShape s)
        {
            if (s == null) return;
            var floors = s.Bottoms != null && s.Bottoms.Count > 0
                ? s.Bottoms
                : (s.Outer != null ? new List<List<WellPt>> { s.Outer } : null);
            if (floors != null)
            {
                for (int i = 0; i < floors.Count; i++)
                {
                    string mh = HandlesInPoly(grp, floors[i]);
                    if (string.IsNullOrEmpty(mh)) mh = HandlesOf(grp);
                    AddPline3(ms, tr, floors[i], true, LayerPit, 1, mh);
                }
            }
            if (s.Steps != null)
            {
                for (int i = 0; i < s.Steps.Count; i++)
                    AddPline3(ms, tr, s.Steps[i], false, LayerPit, 1, null);
            }
            AddPline3(ms, tr, s.Berm, true, LayerBerm, 3, null);
            if (s.SlopeRays != null)
            {
                for (int i = 0; i < s.SlopeRays.Count; i++)
                    AddPline3(ms, tr, new List<WellPt>(s.SlopeRays[i]), false, LayerSlope, 4, null);
            }
            if (s.Faces != null)
            {
                for (int i = 0; i < s.Faces.Count; i++)
                    AddFace(ms, tr, s.Faces[i], LayerSlope, 4);
            }
        }

        static string HandlesInPoly(List<WellItem> grp, IList<WellPt> poly)
        {
            if (grp == null || poly == null || poly.Count < 3) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < grp.Count; i++)
            {
                var w = grp[i] != null ? grp[i].W : null;
                if (w == null) continue;
                if (!WellPitGeom.PointInPoly(poly, w.X, w.Y)) continue;
                string h = grp[i].Handle;
                if (string.IsNullOrEmpty(h)) h = w.Handle;
                if (string.IsNullOrEmpty(h)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(h);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        static void DrawName(BlockTableRecord ms, Transaction tr, WellPitInput w, WellPitShape s)
        {
            if (w == null || s == null) return;
            string name = string.IsNullOrWhiteSpace(w.Name) ? "колодец" : w.Name;
            double lx = w.X + s.Px * (s.BermAcross + 0.4);
            double ly = w.Y + s.Py * (s.BermAcross + 0.4);
            NvkText.Make(ms, tr, LayerTxt, lx, ly, name, 0.35,
                AttachmentPoint.BottomLeft, 0.0, 8.0, true);
        }

        static List<List<WellItem>> ClusterItems(List<WellItem> items)
        {
            var groups = new List<List<WellItem>>();
            if (items == null || items.Count == 0) return groups;
            int n = items.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (WellsConnect(items[i], items[j]))
                        Join(parent, i, j);
                }
            }
            var map = new Dictionary<int, List<WellItem>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(parent, i);
                List<WellItem> g;
                if (!map.TryGetValue(r, out g))
                {
                    g = new List<WellItem>();
                    map[r] = g;
                    groups.Add(g);
                }
                g.Add(items[i]);
            }
            return groups;
        }

        static int Find(int[] p, int i)
        {
            while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; }
            return i;
        }

        static void Join(int[] p, int a, int b)
        {
            int ra = Find(p, a), rb = Find(p, b);
            if (ra != rb) p[rb] = ra;
        }

        static bool WellsConnect(WellItem a, WellItem b)
        {
            if (a == null || b == null || a.S == null || b.S == null) return false;
            if (WellPitGeom.PolysOverlap(a.S.Outer, b.S.Outer)) return true;
            if (WellPitGeom.PolysOverlap(a.S.Berm, b.S.Berm)) return true;
            var scfg = PipeCfg();
            if (a.W != null && a.W.Pipes != null && b.W != null && b.W.Pipes != null)
            {
                var ra = new List<List<WellPt>>();
                var rb = new List<List<WellPt>>();
                CollectRects(a, scfg, ra);
                CollectRects(b, scfg, rb);
                for (int i = 0; i < ra.Count; i++)
                    for (int j = 0; j < rb.Count; j++)
                        if (WellPitGeom.PolysOverlap(ra[i], rb[j])) return true;
            }
            return false;
        }

        static List<List<WellPt>> BridgesOf(List<WellItem> grp)
        {
            var extra = new List<List<WellPt>>();
            if (grp == null) return extra;
            var scfg = PipeCfg();
            for (int i = 0; i < grp.Count; i++)
            {
                var w = grp[i] != null ? grp[i].W : null;
                if (w == null || w.Pipes == null) continue;
                for (int k = 0; k < w.Pipes.Count; k++)
                {
                    var p = w.Pipes[k];
                    if (p == null || !FarInGroup(grp, p)) continue;
                    List<WellPt> rect;
                    if (WellPitGeom.TrenchCorridor(w, p, scfg.BottomLeft(p.Diam), scfg.BottomRight(p.Diam), out rect))
                        extra.Add(rect);
                }
            }
            return extra;
        }

        static bool FarInGroup(List<WellItem> grp, WellPipeSeg p)
        {
            if (grp == null || p == null) return false;
            for (int i = 0; i < grp.Count; i++)
            {
                var w = grp[i] != null ? grp[i].W : null;
                if (w == null) continue;
                double dx = p.X1 - w.X, dy = p.Y1 - w.Y;
                double r = Math.Max(0.5, WellPitGeom.Radius(w));
                if (dx * dx + dy * dy <= r * r + 0.05) return true;
            }
            return false;
        }

        static void CollectRects(WellItem it, SectionConfig scfg, List<List<WellPt>> dst)
        {
            if (it == null || it.W == null || it.W.Pipes == null || it.S == null) return;
            var berm = it.S.Berm != null && it.S.Berm.Count >= 3 ? it.S.Berm : it.S.Outer;
            for (int i = 0; i < it.W.Pipes.Count; i++)
            {
                var p = it.W.Pipes[i];
                if (p == null) continue;
                List<WellPt> rect;
                if (WellPitGeom.TrenchRect(it.W, p, berm, scfg.BottomLeft(p.Diam), scfg.BottomRight(p.Diam), out rect))
                    dst.Add(rect);
            }
        }

        static WellPitShape ShapeOfGroup(List<WellItem> grp, WellPitCfg cfg,
            ObjectId surfId, Transaction tr)
        {
            if (grp == null || grp.Count == 0) return null;
            if (grp.Count == 1) return grp[0].S;
            var shapes = new List<WellPitShape>();
            double zHint = grp[0].ZHint;
            double minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
            for (int i = 0; i < grp.Count; i++)
            {
                if (grp[i].S == null) continue;
                shapes.Add(grp[i].S);
                if (grp[i].ZHint > zHint) zHint = grp[i].ZHint;
                var o = grp[i].S.Outer;
                if (o == null) continue;
                for (int k = 0; k < o.Count; k++)
                {
                    if (o[k].X < minX) minX = o[k].X;
                    if (o[k].Y < minY) minY = o[k].Y;
                    if (o[k].X > maxX) maxX = o[k].X;
                    if (o[k].Y > maxY) maxY = o[k].Y;
                }
            }
            if (shapes.Count == 0) return grp[0].S;
            double pad = cfg != null && cfg.SlopeM > 1e-9 ? Math.Max(8.0, cfg.SlopeM * 20.0) : 2.0;
            if (pad > 80.0) pad = 80.0;
            var tris = SurfaceSampler.TrianglesInBox(tr, surfId,
                minX - pad, minY - pad, maxX + pad, maxY + pad);
            var tin = grp[0].Tin;
            var extra = BridgesOf(grp);
            var u = WellPitGeom.UnionPits(shapes, extra, cfg, tin, tris, zHint);
            return u != null ? u : grp[0].S;
        }

        sealed class PipeBatch
        {
            public List<List<WellPt>> Rects = new List<List<WellPt>>();
            public List<double> Wx = new List<double>();
            public List<double> Wy = new List<double>();
            public List<double> WR = new List<double>();
            public double Z;
        }

        static SectionConfig PipeCfg()
        {
            return new SectionConfig
            {
                TrenchLeft = Math.Max(0.0, NvkUiState.TrenchLeft),
                TrenchRight = Math.Max(0.0, NvkUiState.TrenchRight),
                Joint = ParseJoint(NvkUiState.Joint)
            };
        }

        static void CollectPipes(PipeBatch batch, WellPitInput w, IList<WellPt> berm)
        {
            if (batch == null || w == null || w.Pipes == null) return;
            if (berm == null || berm.Count < 3) return;
            var scfg = PipeCfg();
            AddDisk(batch, w.X, w.Y, w.Sump, WellPitGeom.Radius(w));
            for (int i = 0; i < w.Pipes.Count; i++)
            {
                var p = w.Pipes[i];
                if (p == null) continue;
                double l = scfg.BottomLeft(p.Diam);
                double r = scfg.BottomRight(p.Diam);
                List<WellPt> rect;
                if (WellPitGeom.TrenchRect(w, p, berm, l, r, out rect))
                    batch.Rects.Add(rect);
                batch.Z = p.Invert0;
                if (p.FarR > 0.05)
                    AddDisk(batch, p.X1, p.Y1, p.Invert1, p.FarR);
            }
        }

        static void AddDisk(PipeBatch batch, double x, double y, double z, double r)
        {
            if (batch == null || r < 0.05) return;
            for (int i = 0; i < batch.Wx.Count; i++)
                if (Math.Abs(batch.Wx[i] - x) < 0.02 && Math.Abs(batch.Wy[i] - y) < 0.02)
                    return;
            batch.Wx.Add(x);
            batch.Wy.Add(y);
            batch.WR.Add(r);
            if (batch.Z == 0.0) batch.Z = z;
        }

        static void DrawPipeBatch(BlockTableRecord ms, Transaction tr, PipeBatch batch)
        {
            if (batch == null) return;
            var red = WellPitGeom.UnionRects(batch.Rects, batch.Z);
            for (int i = 0; i < red.Count; i++)
            {
                var loop = red[i];
                if (loop == null || loop.Pts == null) continue;
                AddPline3(ms, tr, loop.Pts, loop.Closed, LayerPipeC, 1, null);
            }
            var blue = WellPitGeom.UnionTrenchesAll(
                batch.Rects, batch.Wx, batch.Wy, batch.WR, batch.Z);
            for (int i = 0; i < blue.Count; i++)
            {
                var loop = blue[i];
                if (loop == null || loop.Pts == null) continue;
                AddPline3(ms, tr, loop.Pts, loop.Closed, LayerPipeR, 5, null);
            }
        }

        static JointType ParseJoint(string s)
        {
            if (s == null) return JointType.Socket;
            s = s.Trim().ToUpperInvariant();
            if (s == "WELD" || s == "СВАРНОЕ") return JointType.Weld;
            if (s == "COUPLING" || s == "МУФТОВОЕ") return JointType.Coupling;
            return JointType.Socket;
        }

        static void AddPline3(BlockTableRecord ms, Transaction tr, List<WellPt> pts,
            bool closed, string layer, short color, string mh)
        {
            if (pts == null || pts.Count < 2) return;
            var pl = new Polyline3d();
            pl.SetDatabaseDefaults();
            pl.Layer = layer;
            pl.Color = Aci(color);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            foreach (var p in pts)
            {
                var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
                pl.AppendVertex(v);
                tr.AddNewlyCreatedDBObject(v, true);
            }
            pl.Closed = closed;
            NvkTag.Stamp(tr, pl);
            if (!string.IsNullOrEmpty(mh))
                NvkTag.Put1000(tr, pl, "MH=", mh);
        }

        static void AddFace(BlockTableRecord ms, Transaction tr, WellPt[] q, string layer, short color)
        {
            if (q == null || q.Length < 3) return;
            var a = new Point3d(q[0].X, q[0].Y, q[0].Z);
            var b = new Point3d(q[1].X, q[1].Y, q[1].Z);
            var c = new Point3d(q[2].X, q[2].Y, q[2].Z);
            var d = q.Length > 3
                ? new Point3d(q[3].X, q[3].Y, q[3].Z)
                : c;
            var f = new Face(a, b, c, d, true, true, true, true);
            f.SetDatabaseDefaults();
            f.Layer = layer;
            f.Color = Aci(color);
            ms.AppendEntity(f);
            tr.AddNewlyCreatedDBObject(f, true);
            NvkTag.Stamp(tr, f);
        }

        static BlockTableRecord Ms(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        }

        static void EnsureLayers(Database db, Transaction tr)
        {
            Ensure(db, tr, LayerPit, 1);
            Ensure(db, tr, LayerBerm, 3);
            Ensure(db, tr, LayerSlope, 4);
            Ensure(db, tr, LayerPipeC, 1);
            Ensure(db, tr, LayerPipeR, 5);
            Ensure(db, tr, LayerTxt, 7);
        }

        static void Ensure(Database db, Transaction tr, string name, short color)
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

        static int ErasePlan(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var erase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                string layer = ent.Layer ?? "";
                if (layer.StartsWith("NVK-MH-", StringComparison.OrdinalIgnoreCase)
                    && !layer.StartsWith("NVK-MH-CS", StringComparison.OrdinalIgnoreCase))
                    erase.Add(id);
            }
            foreach (var id in erase)
                tr.GetObject(id, OpenMode.ForWrite).Erase();
            return erase.Count;
        }

        static int EraseCs(Database db, Transaction tr)
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
                if (n != null && n.StartsWith("NVK-MH-CS-", StringComparison.OrdinalIgnoreCase))
                    erase.Add(id);
            }
            foreach (var id in erase)
                tr.GetObject(id, OpenMode.ForWrite).Erase();
            return erase.Count;
        }

        static AcadColor Aci(short i)
        {
            return AcadColor.FromColorIndex(ColorMethod.ByAci, i);
        }
    }
}
