using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace Nvk.Cad
{
    static class VolDrawer
    {
        const string Layer = "NVK-VOL";
        const string LayerMh = "NVK-VOL-MH";
        const string LayerSamp = "NVK-VOL-SAMP";

        static readonly ProfileKind[] Kinds =
        {
            ProfileKind.Razrabotka,
            ProfileKind.Pesok,
            ProfileKind.Truba,
            ProfileKind.Zashita,
            ProfileKind.Zasypka
        };

        public static string Draw(Database db, Transaction tr, Point3d origin)
        {
            var runs = NvkSession.AllRuns();
            bool any = false;
            foreach (var r in runs)
                if (r != null && r.Chain != null) { any = true; break; }
            if (!any)
                return "[НВК] VOL: нет домена.";

            var tblCfg = NvkSession.Tbl();
            double th = tblCfg.TextH > 0.0 ? tblCfg.TextH : 2.5;

            EnsureLayer(db, tr, Layer, 7);
            EnsureLayer(db, tr, LayerMh, 7);
            EnsureLayer(db, tr, LayerSamp, 7);
            EraseLayer(db, tr, Layer);
            EraseLayer(db, tr, LayerMh);
            EraseLayer(db, tr, LayerSamp);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            double totalL = 0.0;
            foreach (var r in runs)
                if (r != null && r.Chain != null) totalL += r.Chain.Total;
            double wrapW = Math.Max(totalL * 0.5, th * 80.0);

            var packed = new List<RunVol>();
            int nRun = 0;
            foreach (var run in runs)
            {
                if (run == null || run.Chain == null) continue;
                nRun++;
                var cfg = run.Cfg ?? new SectionConfig();
                NvkLog.Write("[НВК] VOL mL=" + cfg.SlopeLeft().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " mR=" + cfg.SlopeRight().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " дноL=" + cfg.TrenchLeft.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " дноR=" + cfg.TrenchRight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + (VolumeCalc.VerticalWalls(cfg) ? " вертик." : " откос"));
                packed.Add(new RunVol
                {
                    Run = run,
                    Cfg = cfg,
                    Levels = VolumeCalc.All(run.Chain, run.Axis, cfg, run.Geoms),
                    Index = nRun
                });
            }

            int count = 0, nMh = 0, nSamp = 0;
            var mhAll = new List<NamedXy>();
            var sampAll = new List<NamedXy>();
            var mhSeen = new HashSet<string>();
            int sampNum = 1;
            foreach (var pack in packed)
            {
                foreach (var p in PlanPoints.Manholes(pack.Run.Chain))
                {
                    string key = p.X.ToString("0.00") + "," + p.Y.ToString("0.00");
                    if (!mhSeen.Add(key)) continue;
                    mhAll.Add(p);
                }
                if (pack.Cfg != null && pack.Cfg.PlanEn)
                {
                    var s = PlanPoints.Samples(pack.Run.Chain, pack.Cfg,
                        pack.Run.PlanTargets, pack.Run.PlanLatOffs, sampNum);
                    sampAll.AddRange(s);
                    sampNum += s.Count;
                }
            }
            double y = origin.Y;
            double gap = th * 1.5;
            double kindGap = th * 12.0;
            var sumKind = new Dictionary<ProfileKind, double>();
            foreach (var pack in packed)
            {
                foreach (var lv in pack.Levels)
                {
                    double acc;
                    sumKind.TryGetValue(lv.Kind, out acc);
                    sumKind[lv.Kind] = acc + lv.TotalVolume;
                }
            }

            bool coordDone = false;
            foreach (var kind in Kinds)
            {
                double yTop = y;
                var win = new SheetWin();
                foreach (var pack in packed)
                {
                    if (nRun > 1)
                    {
                        string head = "Участок " + pack.Index
                            + (string.IsNullOrEmpty(pack.Run.NetName) ? "" : "  " + pack.Run.NetName)
                            + "  L=" + pack.Run.Chain.Total.ToString("0.00") + " м";
                        AccText(ms, tr, win, origin.X, y, th, wrapW, head);
                        y -= th * 2.0 + gap * 0.3;
                        count++;
                    }
                    VolLevel lv = null;
                    foreach (var item in pack.Levels)
                        if (item.Kind == kind) { lv = item; break; }
                    if (lv == null) continue;
                    string txt = VolumeCalc.FormatLevel(lv, pack.Cfg);
                    if (string.IsNullOrEmpty(txt)) continue;
                    NvkLog.Write("[НВК] VOL " + lv.Title + " V=" + lv.TotalVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                        + " м³");
                    NvkLog.Write(txt.Replace("\\P", " | "));
                    AccText(ms, tr, win, origin.X, y, th, wrapW, txt);
                    y -= EstimateHeight(txt, th) + gap;
                    count++;
                }
                if (nRun > 1)
                {
                    double v;
                    if (sumKind.TryGetValue(kind, out v))
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append("Итого по ").Append(nRun).Append(" участкам:\\P");
                        AppendSum(sb, sumKind, kind, SumName(kind));
                        string tot = sb.ToString();
                        AccText(ms, tr, win, origin.X, y, th, wrapW, tot);
                        y -= EstimateHeight(tot, th) + gap;
                        count++;
                    }
                }

                if (!coordDone)
                {
                    double tx = origin.X + wrapW + th * 4;
                    double ty = yTop;
                    var mhId = DrawNamedXy(ms, tr, new Point3d(tx, ty, 0.0), th, LayerMh,
                        "Таблица координат характерных точек", mhAll);
                    if (!mhId.IsNull)
                    {
                        nMh = mhAll.Count;
                        if (NvkSession.CoordWin == null) NvkSession.CoordWin = new SheetWin();
                        AccEntity(tr, NvkSession.CoordWin, mhId);
                    }
                    double sampY = ty - (mhAll.Count + 2) * th * 2.2 - th * 8.0;
                    var sampId = DrawNamedXy(ms, tr, new Point3d(tx, sampY, 0.0), th, LayerSamp,
                        "Таблица точек отбора проб", sampAll);
                    if (!sampId.IsNull)
                    {
                        nSamp = sampAll.Count;
                        if (NvkSession.SampWin == null) NvkSession.SampWin = new SheetWin();
                        AccEntity(tr, NvkSession.SampWin, sampId);
                    }
                    coordDone = true;
                }

                if (win.Any)
                {
                    NvkSession.AddVolWin(kind, win.MinX, win.MinY, win.MaxX, win.MaxY);
                    NvkLog.Write("[НВК] VOL окно " + kind
                        + "  L=" + win.MinX.ToString("0.0")
                        + " R=" + win.MaxX.ToString("0.0")
                        + " B=" + win.MinY.ToString("0.0")
                        + " T=" + win.MaxY.ToString("0.0"));
                }
                y -= kindGap;
            }
            return "[НВК] VOL: текстов " + count + "  колодцев " + nMh
                 + "  проб " + nSamp + "  участков " + nRun
                 + "  окон " + (NvkSession.VolWins != null ? NvkSession.VolWins.Count.ToString() : "0")
                 + "  табл.хар " + (NvkSession.CoordWin != null && NvkSession.CoordWin.Valid ? "1" : "0")
                 + "  табл.проб " + (NvkSession.SampWin != null && NvkSession.SampWin.Valid ? "1" : "0");
        }

        sealed class RunVol
        {
            public DomainResult Run;
            public SectionConfig Cfg;
            public List<VolLevel> Levels;
            public int Index;
        }

        static string SumName(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "разработка";
                case ProfileKind.Pesok: return "песчаное основание";
                case ProfileKind.Truba: return "труба";
                case ProfileKind.Zashita: return "защитный слой";
                default: return "обратная засыпка";
            }
        }

        static void AccText(BlockTableRecord ms, Transaction tr, SheetWin win,
            double x, double y, double th, double w, string txt)
        {
            var id = MakeMText(ms, tr, Layer, x, y, th, w, txt);
            double h = EstimateHeight(txt, th);
            win.Union(x, y - h, x + w, y + th);
            AccEntity(tr, win, id);
        }

        static void AccEntity(Transaction tr, SheetWin win, ObjectId id)
        {
            if (win == null || id.IsNull) return;
            try
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) return;
                var ext = ent.GeometricExtents;
                win.Union(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
            }
            catch { }
        }

        static void AppendSum(System.Text.StringBuilder sb, Dictionary<ProfileKind, double> map,
            ProfileKind kind, string name)
        {
            double v;
            if (!map.TryGetValue(kind, out v)) return;
            sb.Append(name).Append(": ").Append(v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" м³\\P");
        }

        static double EstimateHeight(string txt, double th)
        {
            int lines = 1;
            for (int i = 0; i < txt.Length - 1; i++)
                if (txt[i] == '\\' && txt[i + 1] == 'P') lines++;
            return lines * th * 1.7;
        }

        static ObjectId MakeMText(BlockTableRecord ms, Transaction tr, string layer,
            double x, double y, double th, double w, string txt)
        {
            var mt = new MText();
            mt.SetDatabaseDefaults();
            mt.Layer = layer;
            mt.Location = new Point3d(x, y, 0.0);
            mt.TextHeight = th;
            mt.Width = w;
            mt.Attachment = AttachmentPoint.TopLeft;
            mt.Contents = txt;
            ms.AppendEntity(mt);
            tr.AddNewlyCreatedDBObject(mt, true);
            NvkTag.Stamp(tr, mt);
            return mt.ObjectId;
        }

        static ObjectId DrawNamedXy(BlockTableRecord ms, Transaction tr, Point3d origin, double th,
            string layer, string title, List<NamedXy> pts)
        {
            if (pts == null || pts.Count == 0) return ObjectId.Null;
            var body = new List<string[]>();
            foreach (var p in pts)
            {
                body.Add(new[]
                {
                    p.Name ?? "—",
                    TableLayout.XyText(p.X),
                    TableLayout.XyText(p.Y)
                });
            }
            var table = CadTable.Coord(ms, tr, origin, layer, title, body, th);
            return table != null ? table.ObjectId : ObjectId.Null;
        }

        // ── Слой ─────────────────────────────────────────────────────────────

        static void EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord();
            ltr.Name = name;
            ltr.Color = AcadColor.FromColorIndex(ColorMethod.ByAci, color);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static void EraseLayer(Database db, Transaction tr, string layer)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var msr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var erase = new List<ObjectId>();
            foreach (ObjectId id in msr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase)
                    && NvkTag.Matches(ent, NvkSites.CurrentId))
                    erase.Add(id);
            }
            foreach (var id in erase)
                tr.GetObject(id, OpenMode.ForWrite).Erase();
        }
    }
}
