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
    }

    sealed class CsCell
    {
        public int Index;
        public SheetWin Win = new SheetWin();
    }

    /// <summary>
    /// Пять вкладок уровней. На каждом: VP продольника, VP сечений,
    /// VP расчёта объёмов; отдельные VP таблиц координат и отбора проб;
    /// на четырёх (кроме Трубы) — VP плана.
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
                    SheetWin ep = null;
                    List<CsCell> cells = null;
                    SheetWin vol = null;
                    if (NvkSession.EpWins != null)
                        NvkSession.EpWins.TryGetValue(kind, out ep);
                    if (NvkSession.CsCells != null)
                        NvkSession.CsCells.TryGetValue(kind, out cells);
                    if (NvkUiState.DoVol && NvkSession.VolWins != null)
                        NvkSession.VolWins.TryGetValue(kind, out vol);
                    bool hasEp = ep != null && ep.Valid;
                    bool hasCs = cells != null && cells.Count > 0;
                    bool hasVol = vol != null && vol.Valid;
                    bool hasCoord = NvkUiState.DoVol && NvkSession.CoordWin != null && NvkSession.CoordWin.Valid;
                    bool hasSamp = NvkUiState.DoVol && kind != ProfileKind.Truba
                        && NvkSession.SampWin != null && NvkSession.SampWin.Valid;
                    bool hasPlan = NvkUiState.DoPlan && kind != ProfileKind.Truba
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
                            hasEp ? ep : null, cells);
                        if (have == null) existing.Add(want);
                        if (added > 0) nNew++;
                        else nKeep++;
                    }
                    catch (Exc ex)
                    {
                        nFail++;
                        NvkLog.Write("[НВК] лист " + (have ?? want) + " FAIL " + ex.Message);
                    }
                    NvkQuiet.ToModel();
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

        static void RefineWins(Database db, Transaction tr)
        {
            foreach (var m in EpLayers)
            {
                double x0, y0, x1, y1;
                if (TryLayerExtents(db, tr, m.Layer, out x0, out y0, out x1, out y1))
                    NvkSession.AddEpWin(m.Kind, x0, y0, x1, y1);
                SheetWin w;
                if (NvkSession.EpWins != null
                    && NvkSession.EpWins.TryGetValue(m.Kind, out w)
                    && w != null && w.Valid)
                {
                    UnionNearby(db, tr, w, new[] { "NVK-TBL", "NVK-MARK", "NVK-HATCH" });
                    NvkLog.Write("[НВК] EP окно " + m.Kind
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
                        UnionNearby(db, tr, cell.Win, new[] { "NVK-CS", "NVK-TBL" });
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
            if (TryLayerExtents(db, tr, "NVK-PLAN", out px0, out py0, out px1, out py1))
            {
                NvkSession.AddPlanWin(px0, py0, px1, py1);
                var p = NvkSession.PlanWin;
                NvkLog.Write("[НВК] PLAN окно L=" + p.MinX.ToString("0.0")
                    + " R=" + p.MaxX.ToString("0.0")
                    + " B=" + p.MinY.ToString("0.0")
                    + " T=" + p.MaxY.ToString("0.0"));
            }
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
            ProfileKind kind, SheetWin ep, List<CsCell> cells)
        {
            ObjectId layoutId = ObjectId.Null;
            try { layoutId = lm.GetLayoutId(name); } catch { }
            if (layoutId.IsNull)
            {
                layoutId = lm.CreateLayout(name);
                createdNew = true;
            }

            int nEp = ScaleN(NvkUiState.VpScaleEp, 500);
            int nCs = ScaleN(NvkUiState.VpScaleCs, 200);
            SheetWin vol = null;
            if (NvkUiState.DoVol && NvkSession.VolWins != null)
                NvkSession.VolWins.TryGetValue(kind, out vol);
            SheetWin plan = null;
            if (NvkUiState.DoPlan && kind != ProfileKind.Truba)
                plan = NvkSession.PlanWin;

            try { lm.CurrentLayout = "Model"; } catch { }

            var have = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            double paperW = 841.0, paperH = 594.0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                try
                {
                    Point2d sz = layout.PlotPaperSize;
                    if (sz.X > 20 && sz.Y > 20) { paperW = sz.X; paperH = sz.Y; }
                }
                catch { }
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
            if (paperW + 1.0 < paperH)
            {
                double t = paperW; paperW = paperH; paperH = t;
            }

            double margin = 12.0, gap = 6.0, titleBand = 10.0;
            double usableW = paperW - 2.0 * margin;
            if (usableW < 40.0) usableW = 40.0;
            SheetWin coord = NvkUiState.DoVol ? NvkSession.CoordWin : null;
            SheetWin samp = (NvkUiState.DoVol && kind != ProfileKind.Truba) ? NvkSession.SampWin : null;
            var specs = PackSpecs(kind, ep, vol, plan, coord, samp, cells, nEp, nCs,
                margin, gap, titleBand, usableW, paperH);

            int nAdd = 0;
            NvkLayers.EnsureAll(db);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                var ps = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
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
                    try { vp.On = true; } catch { }
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
                if (plan != null && plan.Valid) titleTxt += "    план";
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
                if (titleEnt == null)
                {
                    NvkLayers.EnsureOne(db, tr, TitleLayer, 7, false, false);
                    var title = new MText();
                    title.SetDatabaseDefaults();
                    title.Location = new Point3d(margin, paperH - margin * 0.4, 0.0);
                    title.Attachment = AttachmentPoint.TopLeft;
                    title.TextHeight = 7.0;
                    title.Width = usableW;
                    title.Contents = titleTxt;
                    title.Layer = TitleLayer;
                    ps.AppendEntity(title);
                    tr.AddNewlyCreatedDBObject(title, true);
                }
                else
                {
                    titleEnt.UpgradeOpen();
                    titleEnt.Contents = titleTxt;
                }
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
                foreach (var s in specs)
                {
                    ObjectId id;
                    if (!have.TryGetValue(s.Key, out id) || id.IsNull) continue;
                    var vp = tr.GetObject(id, OpenMode.ForWrite) as Viewport;
                    if (vp == null || vp.IsErased) continue;
                    try { vp.Width = s.W; } catch { }
                    try { vp.Height = s.H; } catch { }
                    try { vp.CenterPoint = new Point3d(s.Cx, s.Cy, 0.0); } catch { }
                    try { vp.On = true; } catch { }
                    AimVp(vp, s.Win, s.ScaleN, s.Key, s.TwistDeg);
                    if (s.Key != null && s.Key.StartsWith("PLAN:", StringComparison.Ordinal))
                        ApplyPlanLayers(tr, vp);
                }
                tr.Commit();
            }
            NvkLog.Write("[НВК] лист " + name + " вид записан");

            NvkLog.Write("[НВК] лист " + name
                + (createdNew ? " новый" : "")
                + "  бумага " + paperW.ToString("0") + "x" + paperH.ToString("0")
                + "  EP 1:" + nEp.ToString()
                + "  CS 1:" + nCs.ToString()
                + "  экранов +" + nAdd.ToString()
                + "  вид " + specs.Count.ToString());
            return nAdd > 0 ? nAdd : specs.Count;
        }

        static List<VpSpec> PackSpecs(ProfileKind kind, SheetWin ep, SheetWin vol, SheetWin plan,
            SheetWin coord, SheetWin samp, List<CsCell> cells, int nEp, int nCs,
            double margin, double gap, double titleBand, double usableW, double paperH)
        {
            var specs = new List<VpSpec>();
            double xLeft = margin;
            double cursorX = xLeft;
            double cursorY = paperH - margin - titleBand;
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

            place(ep, nEp, "EP:" + ((int)kind).ToString(), 0.0);
            place(vol, nEp, "VOL:" + ((int)kind).ToString(), 0.0);
            place(coord, nEp, "COORD", 0.0);
            place(samp, nEp, "SAMP", 0.0);
            double planTwist = kind != ProfileKind.Truba ? NvkUiState.PlanRotDeg : 0.0;
            if (kind != ProfileKind.Truba)
                place(plan, nEp, "PLAN:" + ((int)kind).ToString(), planTwist);

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

        static void AimVp(Viewport vp, SheetWin win, int scaleN, string key, double twistDeg)
        {
            if (vp == null || win == null || !win.Valid) return;
            double mm = MmPerM(scaleN);
            double cx = (win.MinX + win.MaxX) * 0.5;
            double cy = (win.MinY + win.MaxY) * 0.5;
            double twist = twistDeg * Math.PI / 180.0;
            try { vp.PerspectiveOn = false; } catch { }
            try { vp.ViewDirection = new Vector3d(0.0, 0.0, 1.0); } catch { }
            try { vp.TwistAngle = twist; } catch { }
            if (Math.Abs(twist) > 1e-9)
            {
                try { vp.ViewTarget = new Point3d(cx, cy, 0.0); } catch { }
                try { vp.ViewCenter = new Point2d(0.0, 0.0); } catch { }
            }
            else
            {
                try { vp.ViewTarget = new Point3d(0.0, 0.0, 0.0); } catch { }
                try { vp.ViewCenter = new Point2d(cx, cy); } catch { }
            }
            try { vp.ViewHeight = vp.Height / mm; } catch { }
            NvkLog.Write("[НВК] VP вид " + key
                + "  1:" + scaleN.ToString()
                + "  центр " + cx.ToString("0.0") + "," + cy.ToString("0.0")
                + "  модель " + win.Width.ToString("0.00") + "x" + win.Height.ToString("0.00")
                + "  бумага " + vp.Width.ToString("0.0") + "x" + vp.Height.ToString("0.0")
                + "  H=" + vp.ViewHeight.ToString("0.00")
                + (Math.Abs(twistDeg) > 1e-9 ? "  пск " + twistDeg.ToString("0.##") : ""));
        }

        static void ApplyPlanLayers(Transaction tr, Viewport vp)
        {
            if (vp == null || tr == null) return;
            var on = PlanView.SplitLayers(NvkUiState.PlanVpOn);
            if (on.Count == 0) return;
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
                    if (PlanView.IsOn(rec.Name, on)) thaw.Add(id);
                    else freeze.Add(id);
                }
                if (thaw.Count > 0)
                {
                    try { vp.ThawLayersInViewport(thaw.GetEnumerator()); } catch { }
                }
                if (freeze.Count > 0)
                {
                    try { vp.FreezeLayersInViewport(freeze.GetEnumerator()); } catch { }
                }
                NvkLog.Write("[НВК] PLAN слои вкл " + on.Count.ToString()
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
