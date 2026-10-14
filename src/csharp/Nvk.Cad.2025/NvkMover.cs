using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    public sealed class NvkMover
    {
        public static ResultBuffer MoveLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return LispBridgeText("нет документа");
            var ed = doc.Editor;
            var pe = ed.GetEntity("\n[НВК] выбери профиль или сечение: ");
            if (pe.Status != PromptStatus.OK) return LispBridgeText("отмена");
            string group;
            Point3d from;
            if (!Identify(doc.Database, pe.ObjectId, pe.PickedPoint, out group, out from))
                return LispBridgeText("не NVK профиль/сечение");
            var pp = ed.GetPoint("\n[НВК] новая точка вставки: ");
            if (pp.Status != PromptStatus.OK) return LispBridgeText("отмена");
            Point3d to = pp.Value;
            Vector3d d = to - from;
            int n = 0;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                n = MoveGroup(doc.Database, tr, group, d);
                ApplyDelta(group, from, d);
                tr.Commit();
            }
            ed.WriteMessage("\n[НВК] сдвинуто объектов " + n + "  " + group);
            return LispBridge.LayoutGet(null);
        }

        static ResultBuffer LispBridgeText(string s)
        {
            return new ResultBuffer(new TypedValue((int)LispDataType.Text, s ?? ""));
        }

        static bool Identify(Database db, ObjectId id, Point3d pick, out string group, out Point3d from)
        {
            group = null;
            from = pick;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) { tr.Commit(); return false; }
                var br = ent as BlockReference;
                string name = null;
                if (br != null)
                {
                    try { name = br.Name; } catch (Exc) { }
                }
                if (name != null && name.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase))
                {
                    int lv = ParseL(name);
                    group = "CS" + lv;
                    int idx = lv - 1;
                    from = NvkSession.CsHas[idx] ? NvkSession.CsLvl[idx] : br.Position;
                    tr.Commit();
                    return true;
                }
                string lay = ent.Layer ?? "";
                int el = EpLevel(lay);
                if (el > 0)
                {
                    group = "EP" + el;
                    int idx = el - 1;
                    from = NvkSession.EpHas[idx] ? NvkSession.EpLvl[idx] : pick;
                    tr.Commit();
                    return true;
                }
                if (string.Equals(lay, "NVK-CS", StringComparison.OrdinalIgnoreCase))
                {
                    group = "CS1";
                    from = NvkSession.CsHas[0] ? NvkSession.CsLvl[0] : pick;
                    tr.Commit();
                    return true;
                }
                tr.Commit();
            }
            return false;
        }

        static int ParseL(string name)
        {
            int k = name.IndexOf("-L", StringComparison.OrdinalIgnoreCase);
            if (k < 0) return 1;
            int n;
            if (int.TryParse(name.Substring(k + 2, 1), out n) && n >= 1 && n <= 5)
                return n;
            return 1;
        }

        static int EpLevel(string lay)
        {
            if (string.Equals(lay, "NVK-EP-RAZR", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(lay, "NVK-EP-PESOK", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(lay, "NVK-EP-TRUBA", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(lay, "NVK-EP-ZASH", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(lay, "NVK-EP-ZAS", StringComparison.OrdinalIgnoreCase)) return 5;
            return 0;
        }

        static int MoveGroup(Database db, Transaction tr, string group, Vector3d d)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var mat = Matrix3d.Displacement(d);
            int n = 0;
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (!Match(ent, group)) continue;
                ent.UpgradeOpen();
                ent.TransformBy(mat);
                n++;
            }
            return n;
        }

        static bool Match(Entity ent, string group)
        {
            if (group.StartsWith("EP"))
            {
                int lv = group[2] - '0';
                return EpLevel(ent.Layer) == lv;
            }
            if (group.StartsWith("CS"))
            {
                int lv = group[2] - '0';
                var br = ent as BlockReference;
                string name = null;
                if (br != null) { try { name = br.Name; } catch (Exc) { } }
                if (name != null && name.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase))
                    return ParseL(name) == lv;
                return string.Equals(ent.Layer, "NVK-CS", StringComparison.OrdinalIgnoreCase) && lv == 1;
            }
            return false;
        }

        static void ApplyDelta(string group, Point3d from, Vector3d d)
        {
            int lv = group[2] - '0';
            int idx = lv - 1;
            if (idx < 0 || idx > 4) return;
            if (group.StartsWith("EP"))
            {
                if (!NvkSession.EpHas[idx]) NvkSession.EpLvl[idx] = from;
                NvkSession.EpLvl[idx] = NvkSession.EpLvl[idx] + d;
                NvkSession.EpHas[idx] = true;
            }
            else
            {
                if (!NvkSession.CsHas[idx]) NvkSession.CsLvl[idx] = from;
                NvkSession.CsLvl[idx] = NvkSession.CsLvl[idx] + d;
                NvkSession.CsHas[idx] = true;
            }
        }
    }
}
