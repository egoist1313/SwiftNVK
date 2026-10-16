using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;

namespace Nvk.Cad
{
    /// <summary>Слои NVK-* и AppID NVK. Бывший nvk-sloi.lsp.</summary>
    static class NvkLayers
    {
        static readonly (string Name, short Color, bool NoPlot, bool Off)[] Specs =
        {
            ("NVK-INS",      7, false, false),
            ("NVK-EP-RAZR",  4, false, false),
            ("NVK-EP-PESOK", 2, false, false),
            ("NVK-EP-TRUBA", 5, false, false),
            ("NVK-EP-ZASH",  6, false, false),
            ("NVK-EP-ZAS",   3, false, false),
            ("NVK-CS",       1, false, false),
            ("NVK-POPER",    7, false, false),
            ("NVK-TBL",      7, false, false),
            ("NVK-PLAN",     1, false, false),
            ("NVK-PLAN-MH",  6, false, false),
            ("NVK-PLAN-SAMP", 3, false, false),
            ("NVK-VOL",      7, false, false),
            ("NVK-VOL-MH",   7, false, false),
            ("NVK-VOL-SAMP", 7, false, false),
            ("NVK-MARK",     1, false, false),
            ("NVK-HATCH",    8, false, false),
            ("NVK-HATCHBND", 8, true,  true),
            ("NVK-DATA",     8, true,  true),
            ("NVK-SHEET",    7, false, false),
        };

        public static void EnsureAll(Database db)
        {
            if (db == null) return;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureApp(db, tr);
                foreach (var s in Specs)
                    EnsureLayer(db, tr, s.Name, s.Color, s.NoPlot, s.Off);
                tr.Commit();
            }
        }

        public static void EnsureApp(Database db, Transaction tr)
        {
            if (db == null || tr == null) return;
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(NvkTag.App)) return;
            rat.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = NvkTag.App };
            rat.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }

        public static void EnsureOne(Database db, Transaction tr, string name, short color, bool noplot, bool off)
        {
            EnsureLayer(db, tr, name, color, noplot, off);
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color, bool noplot, bool off)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var rec = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, color),
                IsPlottable = !noplot,
                IsOff = off
            };
            lt.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }
    }
}
