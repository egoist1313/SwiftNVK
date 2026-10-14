using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using ColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using Exc = System.Exception;

namespace Nvk.Cad
{
    public sealed class ZonePicker
    {
        [CommandMethod("НВК-ЗОНЫ")]
        public void PickRu() { Pick(); }

        [CommandMethod("NVKZONES")]
        public void PickEn() { Pick(); }

        const double MaxPickDist = 50.0;

        public static void Pick()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var runs = NvkSession.AllRuns();
            int nRun = 0;
            foreach (var r in runs)
                if (r != null && r.Chain != null) nRun++;
            if (nRun == 0)
            {
                ed.WriteMessage("\n[НВК] сначала Трубы, потом зоны. Нужна ось сети.");
                return;
            }
            var parts = new string[runs.Count];
            var types = new[]
            {
                new[] { "АСФАЛЬТ", "А", "1" },
                new[] { "ГАЗОН", "Г", "3" },
                new[] { "ТРОТУАР", "Т", "4" },
                new[] { "ПРОЧЕЕ", "П", "6" }
            };
            ed.WriteMessage("\n[НВК] зоны покрытия. Пары точек на плане — на ближайший участок. Enter — следующий тип.");
            if (nRun > 1)
                ed.WriteMessage("\n[НВК] участков " + nRun.ToString() + ", кликай ближе к нужной оси.");
            var tmp = new List<ObjectId>();
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                EnsureTmp(doc.Database, tr);
                tr.Commit();
            }
            foreach (var t in types)
            {
                short col = short.Parse(t[2]);
                while (true)
                {
                    var p1 = ed.GetPoint("\n  " + t[0] + " начало <Enter=стоп>: ");
                    if (p1.Status != PromptStatus.OK) break;
                    Mark(doc, p1.Value, col, tmp);
                    var opt = new PromptPointOptions("\n  " + t[0] + " конец: ");
                    opt.UseBasePoint = true;
                    opt.BasePoint = p1.Value;
                    var p2 = ed.GetPoint(opt);
                    if (p2.Status != PromptStatus.OK) continue;
                    Mark(doc, p2.Value, col, tmp);
                    Seg(doc, p1.Value, p2.Value, col, tmp);
                    double mx = 0.5 * (p1.Value.X + p2.Value.X);
                    double my = 0.5 * (p1.Value.Y + p2.Value.Y);
                    int ri = NearestRun(runs, mx, my, MaxPickDist);
                    if (ri < 0)
                    {
                        ed.WriteMessage("\n  точка далеко от труб — кликни ближе к оси участка.");
                        continue;
                    }
                    var chain = runs[ri].Chain;
                    double d1, d2;
                    double ch1 = chain.Project(p1.Value.X, p1.Value.Y, out d1);
                    double ch2 = chain.Project(p2.Value.X, p2.Value.Y, out d2);
                    if (ch1 > ch2) { double x = ch1; ch1 = ch2; ch2 = x; }
                    if (ch2 - ch1 < 1e-4) continue;
                    string tok = t[1] + ":"
                        + ch1.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                        + "-"
                        + ch2.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(parts[ri])) parts[ri] = tok;
                    else parts[ri] = parts[ri] + " " + tok;
                    string runLbl = nRun > 1 ? " участок " + (ri + 1).ToString() : "";
                    ed.WriteMessage("\n  + " + t[0] + runLbl
                        + " ПК " + ch1.ToString("0.00") + "—" + ch2.ToString("0.00"));
                }
            }
            string zones = RoadZones.JoinRuns(parts);
            NvkUiState.Zones = zones;
            if (NvkSession.L1 != null) NvkSession.L1.Zones = zones;
            NvkPalette.SetZones(zones);
            NvkNod.PatchEntry(doc.Database, "ZONES", zones);
            NvkProfile.Save();
            ed.WriteMessage("\n[НВК] зоны: " + (zones.Length > 0 ? zones : "(нет)") + ". Теперь Построить / Обновить.");
            ClearTmp(doc, tmp);
        }

        static int NearestRun(IList<DomainResult> runs, double x, double y, double maxDist)
        {
            int best = -1;
            double bestD = maxDist;
            for (int i = 0; i < runs.Count; i++)
            {
                var c = runs[i] != null ? runs[i].Chain : null;
                if (c == null) continue;
                double d;
                c.Project(x, y, out d);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        static void EnsureTmp(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has("NVK-ZONE-TMP")) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = "NVK-ZONE-TMP" };
            ltr.Color = AcadColor.FromColorIndex(ColorMethod.ByAci, 6);
            ltr.IsPlottable = false;
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static void Mark(Autodesk.AutoCAD.ApplicationServices.Document doc, Point3d p, short col, List<ObjectId> ids)
        {
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    double s = 1.0;
                    var c = AcadColor.FromColorIndex(ColorMethod.ByAci, col);
                    ids.Add(AddLn(ms, tr, p.X - s, p.Y, p.X + s, p.Y, c));
                    ids.Add(AddLn(ms, tr, p.X, p.Y - s, p.X, p.Y + s, c));
                    tr.Commit();
                }
            }
            catch (Exc) { }
        }

        static void Seg(Autodesk.AutoCAD.ApplicationServices.Document doc, Point3d a, Point3d b, short col, List<ObjectId> ids)
        {
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    ids.Add(AddLn(ms, tr, a.X, a.Y, b.X, b.Y, AcadColor.FromColorIndex(ColorMethod.ByAci, col)));
                    tr.Commit();
                }
            }
            catch (Exc) { }
        }

        static ObjectId AddLn(BlockTableRecord ms, Transaction tr, double x1, double y1, double x2, double y2, AcadColor c)
        {
            var ln = new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));
            ln.SetDatabaseDefaults();
            ln.Layer = "NVK-ZONE-TMP";
            ln.Color = c;
            ms.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
            return ln.ObjectId;
        }

        static void ClearTmp(Autodesk.AutoCAD.ApplicationServices.Document doc, List<ObjectId> ids)
        {
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (var id in ids)
                    {
                        if (id.IsNull) continue;
                        try
                        {
                            var e = tr.GetObject(id, OpenMode.ForWrite);
                            e.Erase();
                        }
                        catch (Exc) { }
                    }
                    tr.Commit();
                }
            }
            catch (Exc) { }
        }
    }
}
