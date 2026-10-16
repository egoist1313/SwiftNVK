using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Nvk.Cad
{
    static class NvkClean
    {
        static readonly string[] Layers =
        {
            "NVK-CS", "NVK-POPER", "NVK-VOL", "NVK-PLAN",
            "NVK-PLAN-MH", "NVK-PLAN-SAMP",
            "NVK-EP-RAZR", "NVK-EP-PESOK", "NVK-EP-TRUBA", "NVK-EP-ZASH", "NVK-EP-ZAS",
            "NVK-INS", "NVK-HATCH", "NVK-HATCHBND", "NVK-DATA", "NVK-MARK"
        };

        public static string Run(Database db, Transaction tr)
        {
            int nEnt = 0, nBlk = 0;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var erase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                var br = ent as BlockReference;
                string bname = null;
                if (br != null)
                {
                    try { bname = br.Name; } catch { }
                }
                bool ours = false;
                if (bname != null && (bname.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase)
                                      || bname.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase)))
                    ours = true;
                else if (IsNvkLayer(ent.Layer))
                    ours = true;
                if (ours) erase.Add(id);
            }
            foreach (var id in erase)
            {
                tr.GetObject(id, OpenMode.ForWrite).Erase();
                nEnt++;
            }

            bt.UpgradeOpen();
            var blkErase = new List<ObjectId>();
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsAnonymous || btr.IsLayout) continue;
                if (btr.Name != null && (btr.Name.StartsWith("NVK-CS-", StringComparison.OrdinalIgnoreCase)
                                         || btr.Name.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase)))
                    blkErase.Add(id);
            }
            foreach (var id in blkErase)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (btr.GetBlockReferenceIds(true, true).Count == 0)
                {
                    btr.Erase();
                    nBlk++;
                }
            }

            NvkSession.LastCsInserts.Clear();
            NvkSession.LastEp = null;
            return "[НВК] ОЧИСТИТЬ: объектов " + nEnt + "  блоков " + nBlk
                 + "  (трубы и TIN не трогали)";
        }

        static bool IsNvkLayer(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return false;
            if (!layer.StartsWith("NVK-", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var l in Layers)
                if (string.Equals(l, layer, StringComparison.OrdinalIgnoreCase))
                    return true;
            return layer.StartsWith("NVK-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
