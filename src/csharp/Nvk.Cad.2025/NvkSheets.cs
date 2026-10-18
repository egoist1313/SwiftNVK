using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    sealed class SheetWin
    {
        public double MinX = double.MaxValue;
        public double MinY = double.MaxValue;
        public double MaxX = double.MinValue;
        public double MaxY = double.MinValue;
        public bool Any;

        public bool Valid
        {
            get { return Any && MaxX > MinX + 1e-6 && MaxY > MinY + 1e-6; }
        }

        public double Width { get { return Valid ? MaxX - MinX : 0; } }
        public double Height { get { return Valid ? MaxY - MinY : 0; } }

        public void Union(double x0, double y0, double x1, double y1)
        {
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }
            if (!Any)
            {
                MinX = x0; MinY = y0; MaxX = x1; MaxY = y1;
                Any = true;
                return;
            }
            if (x0 < MinX) MinX = x0;
            if (y0 < MinY) MinY = y0;
            if (x1 > MaxX) MaxX = x1;
            if (y1 > MaxY) MaxY = y1;
        }

        public void Union(WinBox b)
        {
            Union(b.MinX, b.MinY, b.MaxX, b.MaxY);
        }

        public WinBox ToBox()
        {
            return new WinBox(MinX, MinY, MaxX, MaxY);
        }

        public bool Overlaps(SheetWin other, double gap)
        {
            if (other == null || !Valid || !other.Valid) return false;
            return ToBox().Overlaps(other.ToBox(), gap);
        }

        public bool Overlaps(WinBox other, double gap)
        {
            if (!Valid) return false;
            return ToBox().Overlaps(other, gap);
        }

        public SheetWin CloneWin()
        {
            var w = new SheetWin();
            if (!Any) return w;
            w.Union(MinX, MinY, MaxX, MaxY);
            return w;
        }

        /// <summary>
        /// Таблица профиля под графикой (NVK-TBL), подписи слева.
        /// Не расширять влево на шаг CS→EP (80 м), иначе влезет сетка сечений.
        /// </summary>
        public void ExpandEpForTable()
        {
            if (!Valid) return;
            MinY -= Math.Max(80.0, 0.8 * Height);
            MinX -= 25.0;
            MaxX += 5.0;
            MaxY += 5.0;
        }
    }

    sealed class CsCell
    {
        public int Index;
        public SheetWin Win = new SheetWin();
    }

    /// <summary>
    /// Пять вкладок уровней. На каждом: VP продольника, VP сечений,
    /// VP расчёта объёмов; отдельные VP таблиц координат и отбора проб;
    /// VP плана на всех пяти (на Трубе слой отбора проб заморожен).
    /// </summary>
    static class NvkSheets
    {
        const string TitleLayer = "NVK-SHEET";

        static readonly ProfileKind[] Kinds =
        {
            ProfileKind.Razrabotka,
            ProfileKind.Pesok,
            ProfileKind.Truba,
            ProfileKind.Zashita,
            ProfileKind.Zasypka
        };

        static readonly (ProfileKind Kind, string Layer)[] EpLayers =
        {
            (ProfileKind.Razrabotka, "NVK-EP-RAZR"),
            (ProfileKind.Pesok,      "NVK-EP-PESOK"),
            (ProfileKind.Truba,      "NVK-EP-TRUBA"),
            (ProfileKind.Zashita,    "NVK-EP-ZASH"),
            (ProfileKind.Zasypka,    "NVK-EP-ZAS"),
        };

        public static string NameOf(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    return "Разработка";
                case ProfileKind.Pesok:
                    return "Песок " + FmtCm(NvkUiState.Bedding);
                case ProfileKind.Truba:
                    return "Труба";
                case ProfileKind.Zashita:
                    return "Защитный слой " + FmtMm(NvkUiState.ThkZash);
                default:
                    return "Обратная засыпка";
            }
        }

        static string FmtCm(double m)
        {
            double cm = m * 100.0;
            if (Math.Abs(cm - Math.Round(cm)) < 1e-6)
                return Math.Round(cm).ToString("0") + "см";
            return cm.ToString("0.##") + "см";
        }

        static string FmtMm(double m)
        {
            double mm = m * 1000.0;
            if (Math.Abs(mm - Math.Round(mm)) < 1e-6)
                return Math.Round(mm).ToString("0") + "мм";
            return mm.ToString("0.##") + "мм";
        }

        public static bool IsOurs(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("НВК-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("NVK-", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name == "Разработка" || name == "Труба" || name == "Обратная засыпка")
                return true;
            if (name.StartsWith("Песок", StringComparison.Ordinal))
                return true;
            if (name.StartsWith("Защитный слой", StringComparison.Ordinal))
                return true;
            return false;
        }

        public static bool TryKind(string name, out ProfileKind kind)
        {
            kind = ProfileKind.Razrabotka;
            if (string.IsNullOrEmpty(name)) return false;
            if (string.Equals(name, "Model", StringComparison.OrdinalIgnoreCase))
                return false;
            for (int i = 0; i < Kinds.Length; i++)
            {
                var k = Kinds[i];
                if (string.Equals(name, NameOf(k), StringComparison.OrdinalIgnoreCase)
                    || MatchesKind(k, name))
                {
                    kind = k;
                    return true;
                }
            }
            return false;
        }

        public static string Build(Document doc)
        {
            if (doc == null) return "[НВК] ЛИСТЫ: нет документа";
            var db = doc.Database;
            var lm = LayoutManager.Current;
            NvkLog.Write("[НВК] ЛИСТЫ старт (вкладки не переключаем)");

            using (var tr = db.TransactionManager.StartTransaction())
            {
                RefineWins(db, tr);
                tr.Commit();
            }
            NvkLog.Write("[НВК] ЛИСТЫ окна модели собраны");

            var existing = ListLayoutNames(db);
            int nNew = 0, nKeep = 0, nFail = 0, nSkip = 0;
            object[] quiet = null;
            try
            {
                quiet = NvkQuiet.Push();

                foreach (var kind in Kinds)
                {
                    List<CsCell> epCells = null;
                    List<CsCell> cells = null;
                    SheetWin vol = null;
                    if (NvkSession.EpCells != null)
                        NvkSession.EpCells.TryGetValue(kind, out epCells);
                    if (NvkSession.CsCells != null)
                        NvkSession.CsCells.TryGetValue(kind, out cells);
                    if (NvkUiState.DoVol && NvkSession.VolWins != null)
                        NvkSession.VolWins.TryGetValue(kind, out vol);
                    bool hasEp = AnyCell(epCells);
                    bool hasCs = cells != null && cells.Count > 0;
                    bool hasVol = vol != null && vol.Valid;
                    bool hasCoord = NvkUiState.DoVol && NvkSession.CoordWin != null && NvkSession.CoordWin.Valid;
                    bool hasSamp = NvkUiState.DoVol && kind != ProfileKind.Truba
                        && NvkSession.SampWin != null && NvkSession.SampWin.Valid;
                    bool hasPlan = NvkUiState.DoPlan
                        && NvkSession.PlanWin != null && NvkSession.PlanWin.Valid;
                    if (!hasEp && !hasCs && !hasVol && !hasCoord && !hasSamp && !hasPlan)
                    {
                        nSkip++;
                        continue;
                    }

                    string want = NameOf(kind);
                    string have = FindLayout(existing, kind, want);
                    try
                    {
                        int added = MakeLevel(db, lm, have ?? want, have == null, kind,
                            hasEp ? epCells : null, cells);
                        if (have == null) existing.Add(want);
                        if (added > 0) nNew++;
                        else nKeep++;
                    }
                    catch (Exc ex)
                    {
                        nFail++;
                        NvkLog.Write("[НВК] лист " + (have ?? want) + " FAIL " + ex.Message);
                    }
                }
            }
            finally
            {
                NvkQuiet.Pop(quiet);
            }

            string msg = "[НВК] ЛИСТЫ: новых " + nNew + "  без изменений " + nKeep;
            if (nFail > 0) msg += "  ошибок " + nFail;
            if (nSkip > 0) msg += "  пустых " + nSkip;
            return msg;
        }

        static List<string> ListLayoutNames(Database db)
        {
            var names = new List<string>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                    if (!string.IsNullOrEmpty(e.Key)) names.Add(e.Key);
                tr.Commit();
            }
            return names;
        }

        static string FindLayout(List<string> names, ProfileKind kind, string want)
        {
            foreach (var n in names)
                if (string.Equals(n, want, StringComparison.OrdinalIgnoreCase))
                    return n;
            foreach (var n in names)
                if (MatchesKind(kind, n)) return n;
            return null;
        }

        static bool MatchesKind(ProfileKind kind, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    return name == "Разработка"
                        || name.IndexOf("L1", StringComparison.OrdinalIgnoreCase) >= 0
                           && name.IndexOf("Разраб", StringComparison.OrdinalIgnoreCase) >= 0;
                case ProfileKind.Pesok:
                    return name.StartsWith("Песок", StringComparison.Ordinal)
                        || name.IndexOf("L2", StringComparison.OrdinalIgnoreCase) >= 0
                           && name.IndexOf("Песок", StringComparison.OrdinalIgnoreCase) >= 0;
                case ProfileKind.Truba:
                    return name == "Труба"
                        || name.IndexOf("L3", StringComparison.OrdinalIgnoreCase) >= 0
                           && name.IndexOf("Труба", StringComparison.OrdinalIgnoreCase) >= 0;
                case ProfileKind.Zashita:
                    return name.StartsWith("Защитный слой", StringComparison.Ordinal)
                        || name.IndexOf("L4", StringComparison.OrdinalIgnoreCase) >= 0
                           && (name.IndexOf("Защит", StringComparison.OrdinalIgnoreCase) >= 0
                               || name.IndexOf("Zash", StringComparison.OrdinalIgnoreCase) >= 0);
                default:
                    return name == "Обратная засыпка"
                        || name.IndexOf("L5", StringComparison.OrdinalIgnoreCase) >= 0
                           && name.IndexOf("Засып", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static int DeleteLayouts()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;
            var db = doc.Database;
            var lm = LayoutManager.Current;
            try { lm.CurrentLayout = "Model"; } catch { }

            var names = new List<string>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                {
                    if (IsOurs(e.Key)) names.Add(e.Key);
                }
                tr.Commit();
            }

            int n = 0;
            foreach (var name in names)
            {
                try
                {
                    lm.DeleteLayout(name);
                    n++;
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] лист удалить " + name + "  " + ex.Message);
                }
            }
            return n;
        }

        static bool AnyCell(List<CsCell> cells)
        {
            if (cells == null) return false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null && cells[i].Win != null && cells[i].Win.Valid)
                    return true;
            }
            return false;
        }

        static void RefineWins(Database db, Transaction tr)
        {
            foreach (var m in EpLayers)
            {
                SplitEpWins(db, tr, m.Kind, m.Layer);
                List<CsCell> cells;
                if (NvkSession.EpCells == null
                    || !NvkSession.EpCells.TryGetValue(m.Kind, out cells)
                    || cells == null)
                    continue;
                for (int i = 0; i < cells.Count; i++)
                {
                    var w = cells[i] != null ? cells[i].Win : null;
                    if (w == null || !w.Valid) continue;
                    UnionNearby(db, tr, w, new[] { "NVK-TBL", "NVK-MARK", "NVK-HATCH" });
                    NvkLog.Write("[НВК] EP окно " + m.Kind
                        + " #" + cells[i].Index.ToString()
                        + "  L=" + w.MinX.ToString("0.0")
                        + " R=" + w.MaxX.ToString("0.0")
                        + " B=" + w.MinY.ToString("0.0")
                        + " T=" + w.MaxY.ToString("0.0"));
                }
            }

            if (NvkSession.CsCells != null)
            {
                foreach (var kv in NvkSession.CsCells)
                {
                    if (kv.Value == null) continue;
                    foreach (var cell in kv.Value)
                    {
                        if (cell == null || cell.Win == null || !cell.Win.Valid) continue;
                        UnionNearby(db, tr, cell.Win, new[] { "NVK-CS", "NVK-CS-TBL", "NVK-TBL" });
                    }
                }
            }

            if (NvkSession.VolWins != null)
            {
                foreach (var kv in NvkSession.VolWins)
                {
                    if (kv.Value != null && kv.Value.Valid)
                        UnionNearby(db, tr, kv.Value, new[] { "NVK-VOL" });
                }
            }
            if (NvkSession.CoordWin != null && NvkSession.CoordWin.Valid)
                UnionNearby(db, tr, NvkSession.CoordWin, new[] { "NVK-VOL-MH" });
            if (NvkSession.SampWin != null && NvkSession.SampWin.Valid)
                UnionNearby(db, tr, NvkSession.SampWin, new[] { "NVK-VOL-SAMP" });

            double px0, py0, px1, py1;
            string[] planLyrs = { "NVK-PLAN", PlanView.LayerMh, PlanView.LayerSamp, PlanView.LayerSit };
            for (int i = 0; i < planLyrs.Length; i++)
            {
                if (TryLayerExtents(db, tr, planLyrs[i], out px0, out py0, out px1, out py1))
                    NvkSession.AddPlanWin(px0, py0, px1, py1);
            }
            if (NvkSession.PlanWin != null && NvkSession.PlanWin.Valid)
            {
                var p = NvkSession.PlanWin;
                NvkLog.Write("[НВК] PLAN окно L=" + p.MinX.ToString("0.0")
                    + " R=" + p.MaxX.ToString("0.0")
                    + " B=" + p.MinY.ToString("0.0")
                    + " T=" + p.MaxY.ToString("0.0"));
            }
        }

        /// <summary>
        /// Один слой NVK-EP-* — все профили этого уровня. Кластер по зазору
        /// 100 м: два участка (разные вставки) → два окна, не один гигант.
        /// RecordSheetWin текущего участка, если пересекается, сохраняет таблицу.
        /// </summary>
        static void SplitEpWins(Database db, Transaction tr, ProfileKind kind, string layer)
        {
            var recorded = new List<CsCell>();
            List<CsCell> have;
            if (NvkSession.EpCells != null
                && NvkSession.EpCells.TryGetValue(kind, out have)
                && have != null)
            {
                for (int i = 0; i < have.Count; i++)
                {
                    if (have[i] != null && have[i].Win != null && have[i].Win.Valid)
                        recorded.Add(have[i]);
                }
            }
            var boxes = CollectLayerBoxes(db, tr, layer);
            var clusters = WinCluster.Cluster(boxes, WinCluster.EpGapM);
            var cells = new List<CsCell>();
            var used = new bool[recorded.Count];
            for (int i = 0; i < clusters.Count; i++)
            {
                var cl = clusters[i];
                int hit = -1;
                for (int r = 0; r < recorded.Count; r++)
                {
                    if (used[r]) continue;
                    if (recorded[r].Win.Overlaps(cl, WinCluster.EpGapM))
                    {
                        hit = r;
                        break;
                    }
                }
                var cell = new CsCell { Index = cells.Count };
                if (hit >= 0)
                {
                    used[hit] = true;
                    cell.Win = recorded[hit].Win.CloneWin();
                }
                else
                {
                    cell.Win.Union(cl);
                    cell.Win.ExpandEpForTable();
                }
                cells.Add(cell);
            }
            for (int r = 0; r < recorded.Count; r++)
            {
                if (used[r]) continue;
                var cell = new CsCell { Index = cells.Count };
                cell.Win = recorded[r].Win.CloneWin();
                cells.Add(cell);
            }
            if (NvkSession.EpCells == null)
                NvkSession.EpCells = new Dictionary<ProfileKind, List<CsCell>>();
            NvkSession.EpCells[kind] = cells;
        }

        static List<WinBox> CollectLayerBoxes(Database db, Transaction tr, string layer)
        {
            var boxes = new List<WinBox>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                if (!string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    continue;
                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch { continue; }
                var box = new WinBox(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
                if (box.Valid) boxes.Add(box);
            }
            return boxes;
        }

        static void UnionNearby(Database db, Transaction tr, SheetWin win, string[] layers)
        {
            if (win == null || !win.Valid || layers == null || layers.Length == 0) return;
            double pad = Math.Max(2.0, 0.05 * Math.Max(win.Width, win.Height));
            double a0 = win.MinX - pad, b0 = win.MinY - pad, a1 = win.MaxX + pad, b1 = win.MaxY + pad;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                bool hit = false;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (string.Equals(ent.Layer, layers[i], StringComparison.OrdinalIgnoreCase))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit) continue;
                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch { continue; }
                if (ext.MaxPoint.X < a0 || ext.MinPoint.X > a1) continue;
                if (ext.MaxPoint.Y < b0 || ext.MinPoint.Y > b1) continue;
                win.Union(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
            }
        }

        static bool TryLayerExtents(Database db, Transaction tr, string layer,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0;
            bool any = false;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                if (!string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase))
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
            return any && x1 > x0 && y1 > y0;
        }

        static int MakeLevel(Database db, LayoutManager lm, string name, bool createdNew,
            ProfileKind kind, List<CsCell> epCells, List<CsCell> cells)
        {
            string stayOn = "Model";
            try
            {
                string cur = lm.CurrentLayout;
                if (!string.IsNullOrEmpty(cur)) stayOn = cur;
            }
            catch { }

            ObjectId layoutId = ObjectId.Null;
            try { layoutId = lm.GetLayoutId(name); } catch { }
            if (layoutId.IsNull)
            {
                layoutId = lm.CreateLayout(name);
                createdNew = true;
                try
                {
                    using (var trNew = db.TransactionManager.StartTransaction())
                    {
                        var layNew = (Layout)trNew.GetObject(layoutId, OpenMode.ForWrite);
                        var page = SheetSnapStore.PageOf(kind);
                        if (!SheetSnapStore.ApplyPage(layNew, page, name))
                            TrySetupA1(layNew, name);
                        trNew.Commit();
                    }
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] лист " + name + " плот A1: " + ex.Message);
                }
                try
                {
                    if (!string.Equals(lm.CurrentLayout, stayOn, StringComparison.OrdinalIgnoreCase))
                        lm.CurrentLayout = stayOn;
                }
                catch { }
            }

            int nEp = ScaleN(NvkUiState.VpScaleEp, 500);
            int nCs = ScaleN(NvkUiState.VpScaleCs, 200);
            int nPlan = ScaleN(NvkUiState.VpScalePlan, 500);
            SheetWin vol = null;
            if (NvkUiState.DoVol && NvkSession.VolWins != null)
                NvkSession.VolWins.TryGetValue(kind, out vol);
            SheetWin plan = null;
            if (NvkUiState.DoPlan)
                plan = NvkSession.PlanWin;

            var have = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            double paperW = 841.0, paperH = 594.0;
            double px0 = 0.0, py0 = 0.0, px1 = 841.0, py1 = 594.0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                ReadPrintBox(layout, name, out paperW, out paperH, out px0, out py0, out px1, out py1);
                var ps = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                int nTagged = 0, nAlien = 0;
                foreach (ObjectId id in ps)
                {
                    var vp = tr.GetObject(id, OpenMode.ForRead) as Viewport;
                    if (vp == null || vp.IsErased) continue;
                    if (vp.Number == 1) continue;
                    string k = NvkTag.VpKey(vp);
                    if (string.IsNullOrEmpty(k))
                    {
                        nAlien++;
                        continue;
                    }
                    have[k] = id;
                    nTagged++;
                }
                NvkLog.Write("[НВК] лист " + name
                    + " наших " + nTagged.ToString()
                    + "  чужих " + nAlien.ToString()
                    + "  (чужие не трогаем)");
                tr.Commit();
            }
            var keepPaper = new HashSet<string>(have.Keys, StringComparer.OrdinalIgnoreCase);
            if (paperW + 1.0 < paperH)
            {
                double t = paperW; paperW = paperH; paperH = t;
            }

            double pad = 8.0, gap = 6.0, titleBand = 10.0;
            double xLeft = px0 + pad;
            double topY = py1 - pad - titleBand;
            double usableW = (px1 - px0) - 2.0 * pad;
            if (usableW < 40.0) usableW = 40.0;
            if (topY < py0 + pad + 40.0) topY = py1 - pad;
            SheetWin coord = NvkUiState.DoVol ? NvkSession.CoordWin : null;
            SheetWin samp = (NvkUiState.DoVol && kind != ProfileKind.Truba) ? NvkSession.SampWin : null;
            var specs = PackSpecs(kind, epCells, vol, plan, coord, samp, cells, nEp, nCs, nPlan,
                xLeft, topY, gap, usableW);
            FitSpecs(specs, xLeft, py0 + pad, px1 - pad, py1 - pad);
            ApplySnapPaper(kind, specs);
            if (SpecsOverflow(specs, px0, py0, px1, py1))
                NvkLog.Write("[НВК] лист " + name + " видовые крупнее области печати, рамки 1:N");

            int nAdd = 0;
            NvkLayers.EnsureAll(db);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                NvkLayers.EnsureVpLayer(db, tr);
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                var ps = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                DropStaleEpVps(tr, have, specs, kind);
                foreach (var s in specs)
                {
                    if (have.ContainsKey(s.Key)) continue;
                    var vp = new Viewport();
                    vp.SetDatabaseDefaults();
                    vp.CenterPoint = new Point3d(s.Cx, s.Cy, 0.0);
                    vp.Width = s.W;
                    vp.Height = s.H;
                    ps.AppendEntity(vp);
                    tr.AddNewlyCreatedDBObject(vp, true);
                    StyleVp(vp);
                    NvkTag.StampVp(tr, vp, s.Key);
                    have[s.Key] = vp.ObjectId;
                    nAdd++;
                    NvkLog.Write("[НВК] VP " + s.Key
                        + "  бумага " + s.W.ToString("0.0") + "x" + s.H.ToString("0.0")
                        + "  1:" + s.ScaleN.ToString());
                }
                string titleTxt = name
                    + "    профиль 1:" + nEp.ToString()
                    + "    сечения 1:" + nCs.ToString();
                if (vol != null && vol.Valid) titleTxt += "    объёмы";
                if (coord != null && coord.Valid) titleTxt += "    характерные точки";
                if (samp != null && samp.Valid) titleTxt += "    отбор проб";
                if (plan != null && plan.Valid) titleTxt += "    план 1:" + nPlan.ToString();
                MText titleEnt = null;
                foreach (ObjectId id in ps)
                {
                    var mt = tr.GetObject(id, OpenMode.ForRead) as MText;
                    if (mt != null && string.Equals(mt.Layer, TitleLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        titleEnt = mt;
                        break;
                    }
                }
                bool titleNew = titleEnt == null;
                if (titleNew)
                {
                    NvkLayers.EnsureOne(db, tr, TitleLayer, 7, false, false);
                    var title = new MText();
                    title.SetDatabaseDefaults();
                    title.Location = new Point3d(xLeft, py1 - pad * 0.4, 0.0);
                    title.Attachment = AttachmentPoint.TopLeft;
                    title.TextHeight = 7.0;
                    title.Width = usableW;
                    title.Contents = titleTxt;
                    title.Layer = TitleLayer;
                    ps.AppendEntity(title);
                    tr.AddNewlyCreatedDBObject(title, true);
                    titleEnt = title;
                    SheetSnapStore.PlaceTitle(titleEnt, kind);
                }
                else
                {
                    titleEnt.UpgradeOpen();
                    titleEnt.Contents = titleTxt;
                }
                if (createdNew)
                    SheetSnapStore.Spawn(db, tr, ps, kind);
                tr.Commit();
            }

            if (specs.Count == 0)
            {
                NvkLog.Write("[НВК] лист " + name + " экраны на месте");
                return 0;
            }

            NvkLog.Write("[НВК] лист " + name + " вид из модели " + specs.Count.ToString());
            using (var tr = db.TransactionManager.StartTransaction())
            {
                NvkLayers.EnsureVpLayer(db, tr);
                foreach (var s in specs)
                {
                    ObjectId id;
                    if (!have.TryGetValue(s.Key, out id) || id.IsNull) continue;
                    var vp = tr.GetObject(id, OpenMode.ForWrite) as Viewport;
                    if (vp == null || vp.IsErased) continue;
                    try { vp.Locked = false; } catch { }
                    bool keep = keepPaper.Contains(s.Key);
                    if (!keep)
                    {
                        try { vp.Width = s.W; } catch { }
                        try { vp.Height = s.H; } catch { }
                        try { vp.CenterPoint = new Point3d(s.Cx, s.Cy, 0.0); } catch { }
                    }
                    else
                        NvkLog.Write("[НВК] VP бумага как стоит " + s.Key);
                    StyleVp(vp);
                    AimVp(vp, s.Win, s.ScaleN, s.Key, s.TwistDeg);
                    if (s.Key != null && s.Key.StartsWith("PLAN:", StringComparison.Ordinal))
                        ApplyPlanLayers(tr, vp, kind);
                }
                tr.Commit();
            }
            NvkLog.Write("[НВК] лист " + name + " вид записан");

            NvkLog.Write("[НВК] лист " + name
                + (createdNew ? " новый" : "")
                + "  бумага " + paperW.ToString("0") + "x" + paperH.ToString("0")
                + "  EP 1:" + nEp.ToString()
                + "  CS 1:" + nCs.ToString()
                + "  план 1:" + nPlan.ToString()
                + "  экранов +" + nAdd.ToString()
                + "  вид " + specs.Count.ToString());
            return nAdd > 0 ? nAdd : specs.Count;
        }

        static int DropStaleEpVps(Transaction tr, Dictionary<string, ObjectId> have,
            List<VpSpec> specs, ProfileKind kind)
        {
            if (have == null || have.Count == 0) return 0;
            var want = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (specs != null)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(specs[i].Key))
                        want[specs[i].Key] = true;
                }
            }
            var kill = new List<string>();
            foreach (var kv in have)
            {
                if (!IsEpKey(kind, kv.Key)) continue;
                if (want.ContainsKey(kv.Key)) continue;
                kill.Add(kv.Key);
            }
            int n = 0;
            for (int i = 0; i < kill.Count; i++)
            {
                ObjectId id;
                if (!have.TryGetValue(kill[i], out id) || id.IsNull) continue;
                try
                {
                    var vp = tr.GetObject(id, OpenMode.ForWrite) as Viewport;
                    if (vp != null && !vp.IsErased)
                    {
                        vp.Erase();
                        n++;
                        NvkLog.Write("[НВК] VP снят " + kill[i]);
                    }
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] VP снять " + kill[i] + " " + ex.Message);
                }
                have.Remove(kill[i]);
            }
            return n;
        }

        static bool IsEpKey(ProfileKind kind, string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string p = "EP:" + ((int)kind).ToString();
            if (string.Equals(key, p, StringComparison.OrdinalIgnoreCase)) return true;
            return key.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase);
        }

        static void ApplySnapPaper(ProfileKind kind, List<VpSpec> specs)
        {
            var lay = SheetSnapStore.LayoutOf(kind);
            if (lay == null || specs == null) return;
            int n = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                double cx, cy, w, h;
                if (!SheetSnap.TryVp(lay, s.Key, out cx, out cy, out w, out h))
                    continue;
                specs[i] = new VpSpec(cx, cy, w, h, s.Win, s.ScaleN, s.Key, s.TwistDeg);
                n++;
                NvkLog.Write("[НВК] VP снимок " + s.Key
                    + "  бумага " + w.ToString("0.0") + "x" + h.ToString("0.0")
                    + " @ " + cx.ToString("0.0") + "," + cy.ToString("0.0"));
            }
            if (n > 0)
                NvkLog.Write("[НВК] лист снимок " + kind.ToString()
                    + " экранов " + n.ToString());
        }

        static List<VpSpec> PackSpecs(ProfileKind kind, List<CsCell> epCells, SheetWin vol, SheetWin plan,
            SheetWin coord, SheetWin samp, List<CsCell> cells, int nEp, int nCs, int nPlan,
            double xLeft, double topY, double gap, double usableW)
        {
            var specs = new List<VpSpec>();
            double cursorX = xLeft;
            double cursorY = topY;
            double rowH = 0.0;

            Action<SheetWin, int, string, double> place = (win, scaleN, key, twist) =>
            {
                if (win == null || !win.Valid) return;
                double w, h;
                SizeVp(win, MmPerM(scaleN), twist, out w, out h);
                if (cursorX > xLeft + 0.1 && cursorX + w > xLeft + usableW)
                {
                    cursorX = xLeft;
                    cursorY -= rowH + gap;
                    rowH = 0.0;
                }
                specs.Add(new VpSpec(cursorX + w * 0.5, cursorY - h * 0.5, w, h, win, scaleN, key, twist));
                NvkLog.Write("[НВК] VP пак " + key
                    + "  модель L=" + win.MinX.ToString("0.0")
                    + " R=" + win.MaxX.ToString("0.0")
                    + " B=" + win.MinY.ToString("0.0")
                    + " T=" + win.MaxY.ToString("0.0")
                    + "  бумага " + w.ToString("0.0") + "x" + h.ToString("0.0")
                    + "  1:" + scaleN.ToString()
                    + (Math.Abs(twist) > 1e-9 ? "  пск " + twist.ToString("0.##") : ""));
                cursorX += w + gap;
                if (h > rowH) rowH = h;
            };

            PlaceEpCells(epCells, kind, nEp, place);
            place(vol, nEp, "VOL:" + ((int)kind).ToString(), 0.0);
            place(coord, nEp, "COORD", 0.0);
            place(samp, nEp, "SAMP", 0.0);
            double planTwist = NvkUiState.PlanRotDeg;
            place(plan, nPlan, "PLAN:" + ((int)kind).ToString(), planTwist);

            if (cells != null && cells.Count > 0)
            {
                if (rowH > 0.0)
                {
                    cursorX = xLeft;
                    cursorY -= rowH + gap;
                    rowH = 0.0;
                }
                var ordered = new List<CsCell>(cells);
                ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
                foreach (var cell in ordered)
                {
                    if (cell == null || cell.Win == null || !cell.Win.Valid) continue;
                    string csKey = "CS:" + ((int)kind).ToString() + ":" + cell.Index.ToString();
                    place(cell.Win, nCs, csKey, 0.0);
                }
            }
            return specs;
        }

        static void PlaceEpCells(List<CsCell> epCells, ProfileKind kind, int nEp,
            Action<SheetWin, int, string, double> place)
        {
            if (epCells == null || epCells.Count == 0 || place == null) return;
            var ordered = new List<CsCell>(epCells);
            ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
            int nWin = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                var c = ordered[i];
                if (c != null && c.Win != null && c.Win.Valid) nWin++;
            }
            for (int i = 0; i < ordered.Count; i++)
            {
                var cell = ordered[i];
                if (cell == null || cell.Win == null || !cell.Win.Valid) continue;
                string key = nWin <= 1
                    ? "EP:" + ((int)kind).ToString()
                    : "EP:" + ((int)kind).ToString() + ":" + cell.Index.ToString();
                place(cell.Win, nEp, key, 0.0);
            }
        }

        /// <summary>
        /// Слой рамки NVK-VP (не печатается), стекло 90 на объекте.
        /// Слой менять ДО On: смена слоя после On гасит экран.
        /// </summary>
        static void StyleVp(Viewport vp)
        {
            if (vp == null || vp.IsErased) return;
            try { vp.Layer = NvkLayers.VpLayer; } catch (Exc) { }
            try { vp.Transparency = NvkLayers.Alpha(NvkLayers.VpTransPct); } catch (Exc) { }
            try { vp.On = true; } catch (Exc) { }
        }

        static void AimVp(Viewport vp, SheetWin win, int scaleN, string key, double twistDeg)
        {
            if (vp == null || win == null || !win.Valid) return;
            double cx = (win.MinX + win.MaxX) * 0.5;
            double cy = (win.MinY + win.MaxY) * 0.5;
            double twist = PlanView.TwistRad(twistDeg);
            double modelW, modelH;
            if (Math.Abs(twistDeg) > 1e-9)
                PlanView.RotatedSize(win.MinX, win.MinY, win.MaxX, win.MaxY, twistDeg, out modelW, out modelH);
            else
            {
                modelW = win.Width;
                modelH = win.Height;
            }
            double viewH = modelH * 1.08;
            if (viewH < 1e-9) viewH = 1.0;
            try { vp.Locked = false; } catch { }
            try { vp.PerspectiveOn = false; } catch { }
            try { vp.ViewDirection = new Vector3d(0.0, 0.0, 1.0); } catch { }
            try { vp.ViewTarget = new Point3d(cx, cy, 0.0); } catch { }
            try { vp.ViewCenter = new Point2d(0.0, 0.0); } catch { }
            try { vp.TwistAngle = twist; } catch { }
            try { vp.ViewHeight = viewH; } catch { }
            try { vp.TwistAngle = twist; } catch (Exc ex)
            {
                NvkLog.Write("[НВК] VP twist " + key + ": " + ex.Message);
            }
            try { vp.On = true; } catch { }
            double gotDeg = 0.0;
            try { gotDeg = vp.TwistAngle * 180.0 / Math.PI; } catch { }
            bool on = false;
            try { on = vp.On; } catch { }
            NvkLog.Write("[НВК] VP вид " + key
                + "  1:" + scaleN.ToString()
                + "  центр " + cx.ToString("0.0") + "," + cy.ToString("0.0")
                + "  модель " + win.Width.ToString("0.00") + "x" + win.Height.ToString("0.00")
                + "  бумага " + vp.Width.ToString("0.0") + "x" + vp.Height.ToString("0.0")
                + "  H=" + vp.ViewHeight.ToString("0.00")
                + "  пск " + twistDeg.ToString("0.##")
                + "  twist " + gotDeg.ToString("0.##")
                + "  On=" + (on ? "1" : "0"));
        }

        static void ApplyPlanLayers(Transaction tr, Viewport vp, ProfileKind kind)
        {
            if (vp == null || tr == null) return;
            var on = PlanView.SplitLayers(NvkUiState.PlanVpOnFor(kind));
            bool freezeSamp = kind == ProfileKind.Truba;
            try
            {
                var db = vp.Database;
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var freeze = new ObjectIdCollection();
                var thaw = new ObjectIdCollection();
                foreach (ObjectId id in lt)
                {
                    var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (rec.IsDependent) continue;
                    if (string.Equals(rec.Name, NvkLayers.VpLayer, StringComparison.OrdinalIgnoreCase))
                    {
                        thaw.Add(id);
                        continue;
                    }
                    if (PlanView.IsOn(rec.Name, on, freezeSamp)) thaw.Add(id);
                    else if (!rec.IsOff && !rec.IsFrozen) freeze.Add(id);
                }
                if (thaw.Count > 0)
                {
                    try { vp.ThawLayersInViewport(thaw.GetEnumerator()); } catch { }
                }
                if (freeze.Count > 0)
                {
                    try { vp.FreezeLayersInViewport(freeze.GetEnumerator()); } catch { }
                }
                NvkLog.Write("[НВК] PLAN " + NameOf(kind)
                    + " слои " + (on.Count == 0 ? "наши" : "вкл " + on.Count.ToString())
                    + "  заморозка VP " + freeze.Count.ToString());
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] PLAN слои: " + ex.Message);
            }
        }

        struct VpSpec
        {
            public double Cx, Cy, W, H;
            public SheetWin Win;
            public int ScaleN;
            public string Key;
            public double TwistDeg;
            public VpSpec(double cx, double cy, double w, double h, SheetWin win, int scaleN, string key, double twistDeg)
            {
                Cx = cx; Cy = cy; W = w; H = h; Win = win; ScaleN = scaleN; Key = key; TwistDeg = twistDeg;
            }
        }

        struct AimSpec
        {
            public ObjectId Id;
            public SheetWin Win;
            public int ScaleN;
            public string Key;
            public AimSpec(ObjectId id, SheetWin win, int scaleN, string key)
            {
                Id = id; Win = win; ScaleN = scaleN; Key = key;
            }
        }

        static double FitSpecs(List<VpSpec> specs, double x0, double y0, double x1, double y1)
        {
            if (specs == null || specs.Count == 0) return 1.0;
            int n = specs.Count;
            var cx = new double[n];
            var cy = new double[n];
            var w = new double[n];
            var h = new double[n];
            for (int i = 0; i < n; i++)
            {
                cx[i] = specs[i].Cx;
                cy[i] = specs[i].Cy;
                w[i] = specs[i].W;
                h[i] = specs[i].H;
            }
            double k = SheetFit.Fit(cx, cy, w, h, x0, y0, x1, y1);
            for (int i = 0; i < n; i++)
            {
                var s = specs[i];
                specs[i] = new VpSpec(cx[i], cy[i], w[i], h[i], s.Win, s.ScaleN, s.Key, s.TwistDeg);
            }
            return k;
        }

        static bool SpecsOverflow(List<VpSpec> specs, double x0, double y0, double x1, double y1)
        {
            if (specs == null || specs.Count == 0) return false;
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                if (s.Cx - s.W * 0.5 < x0 - 1e-3) return true;
                if (s.Cy - s.H * 0.5 < y0 - 1e-3) return true;
                if (s.Cx + s.W * 0.5 > x1 + 1e-3) return true;
                if (s.Cy + s.H * 0.5 > y1 + 1e-3) return true;
            }
            return false;
        }

        static void ReadPrintBox(Layout layout, string name,
            out double paperW, out double paperH,
            out double x0, out double y0, out double x1, out double y1)
        {
            paperW = 841.0;
            paperH = 594.0;
            x0 = 0.0;
            y0 = 0.0;
            x1 = paperW;
            y1 = paperH;
            if (layout == null) return;
            try
            {
                Point2d sz = layout.PlotPaperSize;
                if (sz.X > 20.0 && sz.Y > 20.0) { paperW = sz.X; paperH = sz.Y; }
            }
            catch { }
            if (paperW + 1.0 < paperH)
            {
                double t = paperW; paperW = paperH; paperH = t;
            }
            double left = 0.0, bot = 0.0, right = 0.0, top = 0.0;
            try
            {
                Extents2d m = layout.PlotPaperMargins;
                left = m.MinPoint.X;
                bot = m.MinPoint.Y;
                right = m.MaxPoint.X;
                top = m.MaxPoint.Y;
            }
            catch { }
            if (left < 0.0) left = 0.0;
            if (bot < 0.0) bot = 0.0;
            if (right < 0.0) right = 0.0;
            if (top < 0.0) top = 0.0;
            double pw = paperW - left - right;
            double ph = paperH - bot - top;
            if (pw < 40.0) pw = paperW;
            if (ph < 40.0) ph = paperH;
            x0 = 0.0;
            y0 = 0.0;
            x1 = pw;
            y1 = ph;
            NvkLog.Write("[НВК] лист " + name
                + " бумага " + paperW.ToString("0") + "x" + paperH.ToString("0")
                + "  печать " + pw.ToString("0") + "x" + ph.ToString("0")
                + "  поля " + left.ToString("0.#") + "/" + bot.ToString("0.#"));
        }

        static void TrySetupA1(Layout layout, string name)
        {
            if (layout == null) return;
            var psv = PlotSettingsValidator.Current;
            psv.RefreshLists(layout);
            string[] devices =
            {
                "DWG To PDF.pc3",
                "AutoCAD PDF (General Documentation).pc3",
                "AutoCAD PDF (High Quality Print).pc3"
            };
            var order = new List<string>();
            System.Collections.Specialized.StringCollection have = null;
            try { have = psv.GetPlotDeviceList(); } catch { }
            for (int i = 0; i < devices.Length; i++)
            {
                if (HasName(have, devices[i])) order.Add(devices[i]);
            }
            if (have != null)
            {
                foreach (string d in have)
                {
                    if (string.IsNullOrEmpty(d)) continue;
                    if (d.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!order.Contains(d)) order.Add(d);
                }
            }
            try
            {
                string cur = layout.PlotConfigurationName;
                if (!string.IsNullOrEmpty(cur) && !order.Contains(cur))
                    order.Insert(0, cur);
            }
            catch { }

            string[] mediaTry =
            {
                "ISO_A1_(841.00_x_594.00_MM)",
                "ISO_full_bleed_A1_(841.00_x_594.00_MM)"
            };
            for (int i = 0; i < order.Count; i++)
            {
                string dev = order[i];
                bool set = false;
                for (int m = 0; m < mediaTry.Length && !set; m++)
                {
                    try { psv.SetPlotConfigurationName(layout, dev, mediaTry[m]); set = true; }
                    catch { }
                }
                if (!set)
                {
                    try { psv.SetPlotConfigurationName(layout, dev, ""); set = true; }
                    catch { }
                }
                if (!set) continue;
                try { psv.RefreshLists(layout); } catch { }
                string media = PickA1Media(psv, layout);
                if (string.IsNullOrEmpty(media))
                {
                    for (int m = 0; m < mediaTry.Length; m++)
                    {
                        if (HasName(psv.GetCanonicalMediaNameList(layout), mediaTry[m]))
                        { media = mediaTry[m]; break; }
                    }
                }
                if (string.IsNullOrEmpty(media)) continue;
                try
                {
                    psv.SetCanonicalMediaName(layout, media);
                    psv.SetPlotPaperUnits(layout, PlotPaperUnit.Millimeters);
                    psv.SetPlotType(layout, PlotType.Layout);
                    psv.SetPlotRotation(layout, PlotRotation.Degrees000);
                    psv.SetPlotOrigin(layout, Point2d.Origin);
                    try { psv.SetUseStandardScale(layout, true); } catch { }
                    try { psv.SetStdScaleType(layout, StdScaleType.StdScale1To1); } catch { }
                    try { layout.PlotViewportBorders = false; } catch { }
                    NvkLog.Write("[НВК] лист " + name + " плот " + dev + "  " + media);
                    return;
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] лист " + name + " плот " + dev + ": " + ex.Message);
                }
            }
            NvkLog.Write("[НВК] лист " + name + " плот A1 не выставлен, поля как есть");
        }

        static bool HasName(System.Collections.Specialized.StringCollection list, string name)
        {
            if (list == null || string.IsNullOrEmpty(name)) return false;
            foreach (string s in list)
            {
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static string PickA1Media(PlotSettingsValidator psv, Layout layout)
        {
            System.Collections.Specialized.StringCollection names = null;
            try { names = psv.GetCanonicalMediaNameList(layout); } catch { }
            return PickA1From(names);
        }

        static string PickA1From(System.Collections.Specialized.StringCollection names)
        {
            if (names == null) return null;
            string best = null;
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                string u = n.ToUpperInvariant();
                bool a1 = u.IndexOf("A1", StringComparison.Ordinal) >= 0;
                bool mm841 = u.IndexOf("841", StringComparison.Ordinal) >= 0;
                bool mm594 = u.IndexOf("594", StringComparison.Ordinal) >= 0;
                if (!a1 && !(mm841 && mm594)) continue;
                int i841 = u.IndexOf("841", StringComparison.Ordinal);
                int i594 = u.IndexOf("594", StringComparison.Ordinal);
                bool land = i841 >= 0 && (i594 < 0 || i841 < i594);
                if (land && (a1 || mm841)) return n;
                if (best == null) best = n;
            }
            return best;
        }

        static int ScaleN(double v, int fallback)
        {
            int n = (int)Math.Round(v);
            if (n < 1) n = fallback;
            return n;
        }

        static double MmPerM(int scaleN)
        {
            if (scaleN < 1) scaleN = 1;
            return 1000.0 / scaleN;
        }

        static void SizeVp(SheetWin win, double mmPerM, out double w, out double h)
        {
            SizeVp(win, mmPerM, 0.0, out w, out h);
        }

        static void SizeVp(SheetWin win, double mmPerM, double twistDeg, out double w, out double h)
        {
            double modelW, modelH;
            if (Math.Abs(twistDeg) > 1e-9)
                PlanView.RotatedSize(win.MinX, win.MinY, win.MaxX, win.MaxY, twistDeg, out modelW, out modelH);
            else
            {
                modelW = win.Width;
                modelH = win.Height;
            }
            modelW *= 1.08;
            modelH *= 1.08;
            if (modelW < 1e-6) modelW = 1.0;
            if (modelH < 1e-6) modelH = 1.0;
            w = modelW * mmPerM;
            h = modelH * mmPerM;
            if (w < 20.0) w = 20.0;
            if (h < 20.0) h = 20.0;
        }

    }
}
