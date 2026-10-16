using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace Nvk.Cad
{
    static class PlanDrawer
    {
        const string Layer = "NVK-PLAN";
        static readonly string[] LeaderLayers =
        {
            Layer, PlanView.LayerMh, PlanView.LayerSamp
        };

        public static string Draw(Database db, Transaction tr)
        {
            var runs = NvkSession.AllRuns();
            bool any = false;
            foreach (var r in runs)
                if (r != null && r.Chain != null) { any = true; break; }
            if (!any)
                return "[НВК] PLAN: нет домена.";

            var cfg0 = (NvkSession.Last != null ? NvkSession.Last.Cfg : null) ?? new SectionConfig();
            if (!cfg0.PlanEn)
                return "[НВК] PLAN: выкл. (plan_en)";
            EnsureLayer(db, tr, Layer, 1);
            EnsureLayer(db, tr, PlanView.LayerMh, 6);
            EnsureLayer(db, tr, PlanView.LayerSamp, 3);
            var poses = LeaderPoses.Parse(NvkSession.CachePlanLdr);
            var live = NvkLeader.Collect(db, tr, LeaderLayers);
            LeaderPoses.Overlay(poses, live);
            int nLiveArr = 0;
            foreach (var kv in live)
                if (kv.Value != null && kv.Value.HasArrow) nLiveArr++;
            NvkLog.Write("[НВК] PLAN выноски из чертежа " + live.Count.ToString(CultureInfo.InvariantCulture)
                + "  стрелок " + nLiveArr.ToString(CultureInfo.InvariantCulture));
            EraseLayer(db, tr, LeaderLayers);
            NvkLeader.LogStyle(db, tr);
            double planDeg = NvkUiState.PlanRotDeg;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int nLead = 0, nMh = 0, nRun = 0;
            var keep = NvkSession.Last;
            foreach (var last in runs)
            {
                if (last == null || last.Chain == null) continue;
                NvkSession.Last = last;
                nRun++;
                var cfg = last.Cfg ?? cfg0;
                var axis = PlanGeom.Axis(last.Chain);
                AddPline(ms, tr, axis, false, 1, 0);
                var trench = TrenchByOffset(last.Chain, cfg);
                AddPline(ms, tr, trench, true, 5, 30);

                if (cfg.PlanEn && last.PlanTargets != null)
                {
                    var samples = PlanPoints.Samples(last.Chain, cfg, last.PlanTargets, last.PlanLatOffs, nLead + 1);
                    foreach (var p in samples)
                    {
                        nLead++;
                        double tx, ty;
                        PlanView.Offset(p.X, p.Y, planDeg, 5.0, out tx, out ty);
                        PlaceLeader(ms, tr, poses, p.X, p.Y, tx, ty, p.Name,
                            PlanView.LayerSamp, planDeg);
                    }
                }

                foreach (var s in last.Chain.Segs)
                {
                    if (s.StartR > 0.05)
                    {
                        AddCircle(ms, tr, s.X0, s.Y0, s.StartR);
                        AddMhLeader(ms, tr, s, true, poses, planDeg);
                        nMh++;
                    }
                }
                var lastSeg = last.Chain.Segs[last.Chain.Segs.Count - 1];
                if (lastSeg.EndR > 0.05)
                {
                    AddCircle(ms, tr, lastSeg.X1, lastSeg.Y1, lastSeg.EndR);
                    AddMhLeader(ms, tr, lastSeg, false, poses, planDeg);
                    nMh++;
                }
            }
            if (keep != null) NvkSession.Last = keep;
            NvkSession.CachePlanLdr = LeaderPoses.Format(poses);
            NvkLeader.LogCounts();
            int nArr = 0;
            foreach (var kv in poses)
                if (kv.Value != null && kv.Value.HasArrow) nArr++;
            NvkLog.Write("[НВК] PLAN выноски сохранены " + poses.Count.ToString(CultureInfo.InvariantCulture)
                + "  стрелок " + nArr.ToString(CultureInfo.InvariantCulture));

            RecordPlanWin(db, tr);
            return "[НВК] PLAN: участков " + nRun
                 + "  ось + траншея"
                 + "  колодцев " + nMh + "  проб " + nLead;
        }

        static void RecordPlanWin(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            bool any = false;
            double x0 = 0, y0 = 0, x1 = 0, y1 = 0;
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                if (!IsPlanLayer(ent.Layer))
                    continue;
                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch { continue; }
                if (!any)
                {
                    x0 = ext.MinPoint.X; y0 = ext.MinPoint.Y;
                    x1 = ext.MaxPoint.X; y1 = ext.MaxPoint.Y;
                    any = true;
                }
                else
                {
                    if (ext.MinPoint.X < x0) x0 = ext.MinPoint.X;
                    if (ext.MinPoint.Y < y0) y0 = ext.MinPoint.Y;
                    if (ext.MaxPoint.X > x1) x1 = ext.MaxPoint.X;
                    if (ext.MaxPoint.Y > y1) y1 = ext.MaxPoint.Y;
                }
            }
            if (!any || x1 <= x0 || y1 <= y0) return;
            NvkSession.AddPlanWin(x0, y0, x1, y1);
            NvkLog.Write("[НВК] PLAN окно L=" + x0.ToString("0.0", CultureInfo.InvariantCulture)
                + " R=" + x1.ToString("0.0", CultureInfo.InvariantCulture)
                + " B=" + y0.ToString("0.0", CultureInfo.InvariantCulture)
                + " T=" + y1.ToString("0.0", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Контур траншеи как AutoCAD OFFSET оси (GetOffsetCurves), иначе miter OffsetPolyVar.
        /// OFFSET не ставит треугольник через ось на изломе.
        /// </summary>
        static List<PlanPt> TrenchByOffset(Chain chain, SectionConfig cfg)
        {
            var fallback = PlanGeom.TrenchClosed(chain, cfg);
            var axis = PlanGeom.Axis(chain);
            if (axis.Count < 2 || chain == null || chain.Segs.Count == 0)
                return fallback;
            JointType joint = cfg != null ? cfg.Joint : JointType.Socket;
            double hwL = cfg != null ? cfg.BottomLeft(chain.Segs[0].Diam) : TrenchWidth.For(chain.Segs[0].Diam, joint) * 0.5;
            double hwR = cfg != null ? cfg.BottomRight(chain.Segs[0].Diam) : hwL;
            bool uniform = true;
            for (int i = 1; i < chain.Segs.Count; i++)
            {
                double l = cfg != null ? cfg.BottomLeft(chain.Segs[i].Diam) : TrenchWidth.For(chain.Segs[i].Diam, joint) * 0.5;
                double r = cfg != null ? cfg.BottomRight(chain.Segs[i].Diam) : l;
                if (Math.Abs(l - hwL) > 0.02 || Math.Abs(r - hwR) > 0.02) { uniform = false; break; }
                hwL = 0.5 * (hwL + l);
                hwR = 0.5 * (hwR + r);
            }
            if (!uniform || hwL < 1e-6 || hwR < 1e-6)
                return fallback;
            var acad = OffsetClosed(axis, hwL, hwR);
            return acad.Count >= 4 ? acad : fallback;
        }

        static List<PlanPt> OffsetClosed(List<PlanPt> axis, double hwL, double hwR)
        {
            var empty = new List<PlanPt>();
            Polyline pl = null;
            DBObjectCollection leftC = null, rightC = null;
            try
            {
                pl = new Polyline(axis.Count);
                for (int i = 0; i < axis.Count; i++)
                    pl.AddVertexAt(i, new Point2d(axis[i].X, axis[i].Y), 0, 0, 0);
                leftC = pl.GetOffsetCurves(hwL);
                rightC = pl.GetOffsetCurves(-hwR);
                var left = PtsFromCurves(leftC);
                var right = PtsFromCurves(rightC);
                if (left.Count < 2 || right.Count < 2) return empty;
                var closed = new List<PlanPt>(left.Count + right.Count);
                closed.AddRange(left);
                for (int i = right.Count - 1; i >= 0; i--)
                    closed.Add(right[i]);
                return closed;
            }
            catch
            {
                return empty;
            }
            finally
            {
                DisposeCol(leftC);
                DisposeCol(rightC);
                if (pl != null) pl.Dispose();
            }
        }

        static List<PlanPt> PtsFromCurves(DBObjectCollection col)
        {
            var pts = new List<PlanPt>();
            if (col == null) return pts;
            foreach (DBObject obj in col)
            {
                var poly = obj as Polyline;
                if (poly != null)
                {
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        var p = poly.GetPoint2dAt(i);
                        pts.Add(new PlanPt(p.X, p.Y));
                    }
                    continue;
                }
                var c = obj as Curve;
                if (c == null) continue;
                try
                {
                    var sp = c.StartPoint;
                    pts.Add(new PlanPt(sp.X, sp.Y));
                    var ep = c.EndPoint;
                    if (Math.Abs(ep.X - sp.X) + Math.Abs(ep.Y - sp.Y) > 1e-9)
                        pts.Add(new PlanPt(ep.X, ep.Y));
                }
                catch { }
            }
            return pts;
        }

        static void DisposeCol(DBObjectCollection col)
        {
            if (col == null) return;
            foreach (DBObject obj in col)
            {
                try { obj.Dispose(); } catch { }
            }
            try { col.Dispose(); } catch { }
        }

        static void AddCircle(BlockTableRecord ms, Transaction tr, double x, double y, double r)
        {
            var c = new Circle(new Point3d(x, y, 0.0), Vector3d.ZAxis, r);
            c.SetDatabaseDefaults();
            c.Layer = Layer;
            c.Color = Aci(6);
            ms.AppendEntity(c);
            tr.AddNewlyCreatedDBObject(c, true);
        }

        static bool IsPlanLayer(string layer)
        {
            return LayerIn(layer, LeaderLayers);
        }

        static bool LayerIn(string layer, string[] layers)
        {
            if (string.IsNullOrEmpty(layer) || layers == null) return false;
            for (int i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layer, layers[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static void AddMhLeader(BlockTableRecord ms, Transaction tr, PipeSeg s, bool atStart,
            Dictionary<string, LeaderPose> poses, double planDeg)
        {
            string name = atStart ? s.StartName : s.EndName;
            if (string.IsNullOrEmpty(name)) return;
            double x = atStart ? s.X0 : s.X1;
            double y = atStart ? s.Y0 : s.Y1;
            double r = atStart ? s.StartR : s.EndR;
            double off = (r > 0.05 ? r : 0.5) + 5.0;
            double tx, ty;
            PlanView.Offset(x, y, planDeg, off, out tx, out ty);
            PlaceLeader(ms, tr, poses, x, y, tx, ty, name, PlanView.LayerMh, planDeg);
        }

        static void PlaceLeader(BlockTableRecord ms, Transaction tr,
            Dictionary<string, LeaderPose> poses,
            double ax, double ay, double tx, double ty, string key, string layer, double planDeg)
        {
            double rot = PlanView.LeaderRotRad(planDeg);
            LeaderPose p;
            if (poses != null && poses.TryGetValue(key, out p) && p != null)
            {
                tx = p.X;
                ty = p.Y;
            }
            if (poses != null)
            {
                poses[key] = new LeaderPose
                {
                    Key = key, X = tx, Y = ty, Rot = rot,
                    Ax = ax, Ay = ay, HasArrow = true
                };
            }
            NvkLeader.Make(ms, tr, layer, ax, ay, tx, ty, key, rot);
        }

        static void AddPline(BlockTableRecord ms, Transaction tr, List<PlanPt> pts, bool closed, short color, int lw)
        {
            if (pts == null || pts.Count < 2) return;
            var pl = new Polyline(pts.Count);
            pl.SetDatabaseDefaults();
            pl.Layer = Layer;
            pl.Closed = closed;
            pl.Color = Aci(color);
            if (lw > 0) pl.LineWeight = (LineWeight)lw;
            for (int i = 0; i < pts.Count; i++)
                pl.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
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

        static void EraseLayer(Database db, Transaction tr, string[] layers)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var erase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && LayerIn(ent.Layer, layers))
                    erase.Add(id);
            }
            foreach (var id in erase)
                tr.GetObject(id, OpenMode.ForWrite).Erase();
        }
    }
}
