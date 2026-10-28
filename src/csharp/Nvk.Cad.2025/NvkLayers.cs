using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;

namespace Nvk.Cad
{
    /// <summary>Слои NVK-* и AppID NVK. Бывший nvk-sloi.lsp.</summary>
    static class NvkLayers
    {
        public const string VpLayer = "NVK-VP";
        /// <summary>Рамка VP: 90 = стекло. На слое 0 — иначе модель в экране бледнеет.</summary>
        public const int VpTransPct = 90;

        static readonly (string Name, short Color, bool NoPlot, bool Off)[] Specs =
        {
            ("NVK-INS",      7, false, false),
            ("NVK-EP-RAZR",  4, false, false),
            ("NVK-EP-PESOK", 2, false, false),
            ("NVK-EP-TRUBA", 5, false, false),
            ("NVK-EP-ZASH",  6, false, false),
            ("NVK-EP-ZAS",   3, false, false),
            ("NVK-CS",       1, false, false),
            ("NVK-CS-TBL",   7, false, false),
            ("NVK-POPER",    7, false, false),
            ("NVK-TBL",      7, false, false),
            ("NVK-PLAN",     1, false, false),
            ("NVK-PLAN-MH",  6, false, false),
            ("NVK-PLAN-DIM", 6, false, false),
            ("NVK-PLAN-SAMP", 3, false, false),
            ("NVK-PLAN-SIT",  5, false, false),
            ("NVK-VOL",      7, false, false),
            ("NVK-VOL-MH",   7, false, false),
            ("NVK-VOL-SAMP", 7, false, false),
            ("NVK-MARK",     7, false, false),
            ("NVK-HATCH",    8, false, false),
            ("NVK-HATCHBND", 8, true,  true),
            ("NVK-DATA",     8, true,  true),
            ("NVK-SHEET",    7, false, false),
            (VpLayer,        8, true,  false),
        };

        public static void EnsureAll(Database db)
        {
            if (db == null) return;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureApp(db, tr);
                foreach (var s in Specs)
                    EnsureLayer(db, tr, s.Name, s.Color, s.NoPlot, s.Off);
                EnsureVpLayer(db, tr);
                tr.Commit();
            }
        }

        /// <summary>
        /// Рамка видового: слой не печатается, непрозрачный.
        /// 90% стекла — на самом Viewport, после смены слоя On=true.
        /// </summary>
        public static void EnsureVpLayer(Database db, Transaction tr)
        {
            EnsureLayer(db, tr, VpLayer, 8, true, false, true, 0);
        }

        /// <summary>AutoCAD: 0% непрозрачно, 90% максимум «стекла». Alpha 255 = непрозрачно.</summary>
        public static Transparency Alpha(int pct)
        {
            if (pct < 0) pct = 0;
            if (pct > 90) pct = 90;
            int a = (int)Math.Round(255.0 * (100 - pct) / 100.0);
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            return new Transparency((byte)a);
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
            EnsureLayer(db, tr, name, color, noplot, off, false, -1);
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color,
            bool noplot, bool off, bool force, int transPct)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name))
            {
                bool paintWhite = color == 7;
                if (!force && !paintWhite) return;
                var rec = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForWrite);
                if (paintWhite && (rec.Color == null || rec.Color.ColorIndex != 7))
                    rec.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                if (!force) return;
                rec.IsPlottable = !noplot;
                rec.IsOff = off;
                if (transPct >= 0)
                {
                    try { rec.Transparency = Alpha(transPct); } catch { }
                }
                return;
            }
            lt.UpgradeOpen();
            var neu = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, color),
                IsPlottable = !noplot,
                IsOff = off
            };
            if (transPct >= 0)
            {
                try { neu.Transparency = Alpha(transPct); } catch { }
            }
            lt.Add(neu);
            tr.AddNewlyCreatedDBObject(neu, true);
        }
    }
}
