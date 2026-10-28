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
            var runs = NvkSession.VolRuns();
            bool any = false;
            foreach (var r in runs)
                if (r != null && r.Chain != null) { any = true; break; }
            if (!any)
                return "[НВК] VOL: нет домена.";
            var keep = NvkSession.Last;

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
            double wrapW = Math.Max(totalL * 0.5, th * 48.0);

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
                    Levels = VolumeCalc.All(run.Chain, run.Axis, cfg, run.Geoms, NvkSession.BandOptsOf(run)),
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
                NvkSession.Last = pack.Run;
                foreach (var p in PlanPoints.Manholes(pack.Run.Chain, false))
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
            var sets = new List<List<VolLevel>>();
            foreach (var pack in packed)
                if (pack.Levels != null) sets.Add(pack.Levels);
            var merged = VolumeCalc.MergeAll(sets);
            var cfg0 = packed[0].Cfg;
            if (keep == null && packed[0].Run != null)
                NvkSession.Last = packed[0].Run;

            bool coordDone = false;
            foreach (var kind in Kinds)
            {
                double yTop = y;
                var win = new SheetWin();
                VolLevel lv = null;
                foreach (var item in merged)
                    if (item.Kind == kind) { lv = item; break; }
                if (lv != null)
                {
                    string txt = VolumeCalc.FormatLevel(lv, cfg0);
                    if (!string.IsNullOrEmpty(txt))
                    {
                        NvkLog.Write("[НВК] VOL " + lv.Title + " V=" + lv.TotalVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                            + " м³");
                        NvkLog.Write(txt.Replace("\\P", " | "));
                        AccText(ms, tr, win, origin.X, y, th, wrapW, txt, kind);
                        y -= EstimateHeight(txt, th) + gap;
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
            if (keep != null) NvkSession.Last = keep;
            return "[НВК] VOL: текстов " + count + "  колодцев " + nMh
                 + "  проб " + nSamp + "  участков " + nRun
                 + "  окон " + (NvkSession.VolWins != null ? NvkSession.VolWins.Count.ToString() : "0")
                 + "  табл.хар " + (NvkSession.CoordWin != null && NvkSession.CoordWin.Valid ? "1" : "0")
                 + "  табл.проб " + (NvkSession.SampWin != null && NvkSession.SampWin.Valid ? "1" : "0");
        }

        /// <summary>
        /// Только Contents MText на NVK-VOL. Колодцы, пробы, окна, профиль, план не трогаем.
        /// </summary>
        public static string RefreshTexts(Database db, Transaction tr, Point3d? origin)
        {
            var runs = NvkSession.VolRuns();
            bool any = false;
            foreach (var r in runs)
                if (r != null && r.Chain != null) { any = true; break; }
            if (!any)
                return "[НВК] VOL: нет домена. Сначала Построить.";

            var tblCfg = NvkSession.Tbl();
            double th = tblCfg.TextH > 0.0 ? tblCfg.TextH : 2.5;
            double totalL = 0.0;
            foreach (var r in runs)
                if (r != null && r.Chain != null) totalL += r.Chain.Total;
            double wrapW = Math.Max(totalL * 0.5, th * 48.0);

            var packed = new List<RunVol>();
            int nRun = 0;
            foreach (var run in runs)
            {
                if (run == null || run.Chain == null) continue;
                nRun++;
                var cfg = run.Cfg ?? new SectionConfig();
                packed.Add(new RunVol
                {
                    Run = run,
                    Cfg = cfg,
                    Levels = VolumeCalc.All(run.Chain, run.Axis, cfg, run.Geoms, NvkSession.BandOptsOf(run)),
                    Index = nRun
                });
            }
            var sets = new List<List<VolLevel>>();
            foreach (var pack in packed)
                if (pack.Levels != null) sets.Add(pack.Levels);
            var merged = VolumeCalc.MergeAll(sets);
            var cfg0 = packed[0].Cfg;

            var wanted = new List<KindTxt>();
            foreach (var kind in Kinds)
            {
                VolLevel lv = null;
                foreach (var item in merged)
                    if (item.Kind == kind) { lv = item; break; }
                if (lv == null) continue;
                string txt = VolumeCalc.FormatLevel(lv, cfg0);
                if (string.IsNullOrEmpty(txt)) continue;
                NvkLog.Write("[НВК] VOL " + lv.Title + " V=" + lv.TotalVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " м³");
                NvkLog.Write(txt.Replace("\\P", " | "));
                wanted.Add(new KindTxt { Kind = kind, Txt = txt });
            }

            EnsureLayer(db, tr, Layer, 7);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var existing = CollectVolMText(ms, tr);
            var used = new HashSet<ObjectId>();
            int nUp = 0, nNew = 0, nDel = 0;

            foreach (var w in wanted)
            {
                MText hit = FindKind(existing, used, w.Kind);
                if (hit == null) hit = FindUntagged(existing, used);
                if (hit != null)
                {
                    if (!hit.IsWriteEnabled) hit.UpgradeOpen();
                    hit.Contents = w.Txt;
                    NvkTag.Put1000(tr, hit, "VOL=", ((int)w.Kind).ToString());
                    used.Add(hit.ObjectId);
                    nUp++;
                    continue;
                }
                Point3d at;
                if (!TryNewSpot(existing, origin, th, out at))
                    return "[НВК] VOL: нет текста объёмов. Сначала Построить.";
                var id = MakeMText(ms, tr, Layer, at.X, at.Y, th, wrapW, w.Txt, w.Kind);
                used.Add(id);
                nNew++;
            }
            foreach (var mt in existing)
            {
                if (used.Contains(mt.ObjectId)) continue;
                if (!mt.IsWriteEnabled) mt.UpgradeOpen();
                mt.Erase();
                nDel++;
            }
            return "[НВК] VOL текст: обновлено " + nUp
                 + "  новых " + nNew
                 + "  стёрто " + nDel
                 + "  участков " + nRun;
        }

        sealed class KindTxt
        {
            public ProfileKind Kind;
            public string Txt;
        }

        static List<MText> CollectVolMText(BlockTableRecord ms, Transaction tr)
        {
            var list = new List<MText>();
            foreach (ObjectId id in ms)
            {
                var mt = tr.GetObject(id, OpenMode.ForRead) as MText;
                if (mt == null || mt.IsErased) continue;
                if (!string.Equals(mt.Layer, Layer, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(NvkUiState.VolJoinSites || NvkTag.Matches(mt, NvkSites.CurrentId))) continue;
                list.Add(mt);
            }
            list.Sort((a, b) => b.Location.Y.CompareTo(a.Location.Y));
            return list;
        }

        static MText FindKind(List<MText> list, HashSet<ObjectId> used, ProfileKind kind)
        {
            foreach (var mt in list)
            {
                if (used.Contains(mt.ObjectId)) continue;
                ProfileKind k;
                if (TryKind(mt, out k) && k == kind) return mt;
            }
            return null;
        }

        static MText FindUntagged(List<MText> list, HashSet<ObjectId> used)
        {
            foreach (var mt in list)
            {
                if (used.Contains(mt.ObjectId)) continue;
                ProfileKind k;
                if (!TryKind(mt, out k)) return mt;
            }
            return null;
        }

        static bool TryKind(MText mt, out ProfileKind kind)
        {
            kind = ProfileKind.Razrabotka;
            string s = NvkTag.Get1000(mt, "VOL=");
            int n;
            if (!string.IsNullOrEmpty(s) && int.TryParse(s, out n) && n >= 1 && n <= 5)
            {
                kind = (ProfileKind)n;
                return true;
            }
            return GuessKind(mt != null ? mt.Contents : null, out kind);
        }

        static bool GuessKind(string txt, out ProfileKind kind)
        {
            kind = ProfileKind.Razrabotka;
            if (string.IsNullOrEmpty(txt)) return false;
            if (txt.IndexOf("разработ", StringComparison.OrdinalIgnoreCase) >= 0)
            { kind = ProfileKind.Razrabotka; return true; }
            if (txt.IndexOf("основан", StringComparison.OrdinalIgnoreCase) >= 0)
            { kind = ProfileKind.Pesok; return true; }
            if (txt.IndexOf("Общая длина", StringComparison.Ordinal) >= 0
                || txt.IndexOf("трубопровод", StringComparison.OrdinalIgnoreCase) >= 0)
            { kind = ProfileKind.Truba; return true; }
            if (txt.IndexOf("защитн", StringComparison.OrdinalIgnoreCase) >= 0)
            { kind = ProfileKind.Zashita; return true; }
            if (txt.IndexOf("песка", StringComparison.OrdinalIgnoreCase) >= 0
                || txt.IndexOf("местного грунта", StringComparison.OrdinalIgnoreCase) >= 0
                || txt.IndexOf("засып", StringComparison.OrdinalIgnoreCase) >= 0)
            { kind = ProfileKind.Zasypka; return true; }
            return false;
        }

        static bool TryNewSpot(List<MText> existing, Point3d? origin, double th, out Point3d at)
        {
            at = Point3d.Origin;
            double minY = double.MaxValue;
            double x = origin.HasValue ? origin.Value.X : double.NaN;
            foreach (var mt in existing)
            {
                if (mt.IsErased) continue;
                if (double.IsNaN(x)) x = mt.Location.X;
                if (mt.Location.Y < minY) minY = mt.Location.Y;
            }
            if (minY < double.MaxValue * 0.5)
            {
                at = new Point3d(x, minY - th * 12.0, 0.0);
                return true;
            }
            if (origin.HasValue)
            {
                at = origin.Value;
                return true;
            }
            return false;
        }

        sealed class RunVol
        {
            public DomainResult Run;
            public SectionConfig Cfg;
            public List<VolLevel> Levels;
            public int Index;
        }

        static void AccText(BlockTableRecord ms, Transaction tr, SheetWin win,
            double x, double y, double th, double w, string txt, ProfileKind kind)
        {
            var id = MakeMText(ms, tr, Layer, x, y, th, w, txt, kind);
            AccEntity(tr, win, id);
            if (!win.Any)
            {
                double h = EstimateHeight(txt, th);
                win.Union(x, y - h, x + w, y + th);
            }
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

        static double EstimateHeight(string txt, double th)
        {
            int lines = 1;
            for (int i = 0; i < txt.Length - 1; i++)
                if (txt[i] == '\\' && txt[i + 1] == 'P') lines++;
            return lines * th * 1.7;
        }

        static ObjectId MakeMText(BlockTableRecord ms, Transaction tr, string layer,
            double x, double y, double th, double w, string txt, ProfileKind kind)
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
            NvkTag.Put1000(tr, mt, "VOL=", ((int)kind).ToString());
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
                    && (NvkUiState.VolJoinSites || NvkTag.Matches(ent, NvkSites.CurrentId)))
                    erase.Add(id);
            }
            foreach (var id in erase)
                tr.GetObject(id, OpenMode.ForWrite).Erase();
        }
    }
}
