using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CivilDb = Autodesk.Civil.DatabaseServices;
using Exc = System.Exception;

namespace Nvk.Cad
{
    public sealed class LispBridge
    {
        const string PingOk = "NVK ok 0.227";

        [LispFunction("NVK-PING")]
        public static ResultBuffer Ping(ResultBuffer args)
        {
            var core = typeof(LayerStack).Assembly.GetName().Version;
            var cad = typeof(LispBridge).Assembly.GetName().Version;
            var msg = PingOk + " core=" + (core != null ? core.ToString() : "?")
                + " cad=" + (cad != null ? cad.ToString() : "?");
            return Text(msg);
        }

        [LispFunction("NVK-ROOT")]
        public static ResultBuffer Root(ResultBuffer args)
        {
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && arr[0].Value is string s && s.Length > 0)
                    NvkSession.RootDir = s;
            }
            return Text(NvkSession.RootDir ?? "");
        }

        [LispFunction("NVK-L1-SAVE")]
        public static ResultBuffer L1Save(ResultBuffer args)
        {
            NvkUiState.SyncL1();
            var st = NvkSession.L1 ?? L1Style.Default();
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && arr[0].Value is string n && n.Length > 0)
                    st.Name = n;
            }
            L1PresetStore.Save(st);
            NvkUiState.L1Preset = st.Name;
            return Text("saved " + L1PresetStore.PathOf(st.Name));
        }

        [LispFunction("NVK-L1-LOAD")]
        public static ResultBuffer L1Load(ResultBuffer args)
        {
            string name = "default";
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && arr[0].Value is string n && n.Length > 0)
                    name = n;
            }
            var st = L1PresetStore.Load(name);
            NvkUiState.FromL1(st);
            return Text("loaded " + st.Name);
        }

        [LispFunction("NVK-L1-LIST")]
        public static ResultBuffer L1List(ResultBuffer args)
        {
            var rb = new ResultBuffer();
            rb.Add(new TypedValue((int)LispDataType.ListBegin));
            foreach (var n in L1PresetStore.List())
                rb.Add(new TypedValue((int)LispDataType.Text, n));
            rb.Add(new TypedValue((int)LispDataType.ListEnd));
            return rb;
        }

        [LispFunction("NVK-FRESH-SCATTER")]
        public static ResultBuffer FreshScatter(ResultBuffer args)
        {
            NvkSession.FreshRandom = true;
            NvkSession.FreshRandomKind = 0;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0]))
                {
                    int k = (int)Math.Round(ToDouble(arr[0]));
                    if (k < 0) k = 0;
                    if (k > 5) k = 5;
                    NvkSession.FreshRandomKind = k;
                }
            }
            return Text(NvkSession.FreshRandomKind == 0
                ? "fresh all"
                : "fresh " + NvkSession.FreshRandomKind);
        }

        [LispFunction("NVK-UI")]
        public static ResultBuffer UiShow(ResultBuffer args)
        {
            NvkPalette.Show();
            return Text("panel");
        }

        [LispFunction("NVK-ROWS-PANEL")]
        public static ResultBuffer RowsPanelShow(ResultBuffer args)
        {
            NvkPalette.ShowRows();
            return Text("rows-panel");
        }

        [LispFunction("NVK-UI-GET")]
        public static ResultBuffer UiGet(ResultBuffer args)
        {
            return NvkPalette.GetLisp(null);
        }

        [LispFunction("NVK-UI-PUT")]
        public static ResultBuffer UiPut(ResultBuffer args)
        {
            return NvkPalette.PutLisp(args);
        }

        /// <summary>
        /// (nvk-table-cfg textH offsetPct roundTo slopeRound unit showPerm showLen showSlope labelColW)
        /// Настройки таблицы PoperSwift. Живут в сессии, рисунок их читает.
        /// </summary>
        [LispFunction("NVK-TABLE-CFG")]
        public static ResultBuffer TableCfgLisp(ResultBuffer args)
        {
            var t = new TableCfg();
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0])) t.TextH = ToDouble(arr[0]);
                if (arr.Length >= 2 && IsNumber(arr[1])) t.OffsetPct = ToDouble(arr[1]);
                if (arr.Length >= 3 && IsNumber(arr[2])) t.RoundTo = (int)ToDouble(arr[2]);
                if (arr.Length >= 4 && IsNumber(arr[3])) t.SlopeRoundTo = (int)ToDouble(arr[3]);
                if (arr.Length >= 5 && arr[4].Value is string u && u.Length > 0) t.SlopeUnit = u;
                if (arr.Length >= 6 && IsNumber(arr[5])) t.ShowPermille = ToDouble(arr[5]) > 0.5;
                if (arr.Length >= 7 && IsNumber(arr[6])) t.ShowLengths = ToDouble(arr[6]) > 0.5;
                if (arr.Length >= 8 && IsNumber(arr[7])) t.ShowSlopes = ToDouble(arr[7]) > 0.5;
                if (arr.Length >= 9 && IsNumber(arr[8])) t.LabelColW = ToDouble(arr[8]);
                if (arr.Length >= 10 && IsNumber(arr[9])) t.CsTextH = ToDouble(arr[9]);
            }
            if (t.TextH <= 0.0) t.TextH = 2.5;
            if (t.CsTextH <= 0.0) t.CsTextH = 0.5;
            t.Annotative = NvkUiState.Annotative;
            t.AnnGap = NvkUiState.AnnGap;
            t.DrawProjections = NvkUiState.DrawProjections;
            t.ProjectionColor = (short)NvkUiState.ProjectionColor;
            t.UseTextHeight = true;
            t.DeltaH = true;
            t.LabelHeight = NvkUiState.LabelHeight;
            t.LabelLength = NvkUiState.LabelLength;
            t.LabelSlope = NvkUiState.LabelSlope;
            if (t.OffsetPct < 0.0) t.OffsetPct = 0.0;
            if (t.OffsetPct > 100.0) t.OffsetPct = 100.0;
            t.RoundTo = 2;
            t.SlopeRoundTo = 1;
            t.ShowLengths = true;
            t.ShowSlopes = true;
            if (string.IsNullOrEmpty(t.SlopeUnit)) t.SlopeUnit = "permille";
            NvkSession.Table = t;
            return Text("textH=" + t.TextH.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                 + "  round=" + t.RoundTo
                 + "  slope=" + t.SlopeUnit);
        }

        /// <summary>
        /// (nvk-osh-override val) — задать условный горизонт вручную (число).
        /// (nvk-osh-override)     — сбросить на авто.
        /// Возвращает текущее значение или "auto".
        /// </summary>
        [LispFunction("NVK-OSH-OVERRIDE")]
        public static ResultBuffer OshOverrideLisp(ResultBuffer args)
        {
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0]))
                    NvkUiState.OshOverride = ToDouble(arr[0]);
                else
                    NvkUiState.OshOverride = null;
            }
            if (NvkUiState.OshOverride.HasValue)
                return Real(NvkUiState.OshOverride.Value);
            return Text("auto");
        }

        [LispFunction("NVK-TRENCH-WIDTH")]
        public static ResultBuffer LispTrenchWidth(ResultBuffer args)
        {
            double diam = 0.3;
            var joint = JointType.Socket;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0]))
                    diam = ToDouble(arr[0]);
                if (arr.Length >= 2 && arr[1].Value is string s)
                    joint = ParseJoint(s);
            }
            return new ResultBuffer(new TypedValue((int)LispDataType.Double, TrenchWidth.For(diam, joint)));
        }

        [LispFunction("NVK-READ-PIPE")]
        public static ResultBuffer ReadPipe(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return Text("нет документа");

            var ed = doc.Editor;
            var pso = new PromptEntityOptions("\n[НВК] Укажи трубу Civil 3D: ");
            pso.SetRejectMessage("\nЭто не труба.");
            pso.AddAllowedClass(typeof(CivilDb.Pipe), exactMatch: false);
            var psr = ed.GetEntity(pso);
            if (psr.Status != PromptStatus.OK)
                return null;

            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var pipe = tr.GetObject(psr.ObjectId, OpenMode.ForRead) as CivilDb.Pipe;
                    if (pipe == null)
                    {
                        tr.Commit();
                        return Text("не Pipe");
                    }

                    double diam = pipe.InnerDiameterOrWidth;
                    if (diam <= 0.0) diam = pipe.OuterDiameterOrWidth;
                    if (diam > 10.0) diam = diam / 1000.0;
                    if (diam <= 0.0) diam = 0.3;

                    var sp = pipe.StartPoint;
                    var ep = pipe.EndPoint;
                    double si = sp.Z - diam * 0.5;
                    double ei = ep.Z - diam * 0.5;
                    double len = pipe.Length3D;
                    string handle = psr.ObjectId.Handle.ToString();

                    tr.Commit();

                    return new ResultBuffer(
                        new TypedValue((int)LispDataType.ListBegin),
                        new TypedValue((int)LispDataType.Text, handle),
                        new TypedValue((int)LispDataType.Double, si),
                        new TypedValue((int)LispDataType.Double, ei),
                        new TypedValue((int)LispDataType.Double, diam),
                        new TypedValue((int)LispDataType.Double, len),
                        new TypedValue((int)LispDataType.ListEnd));
                }
            }
            catch (Exc ex)
            {
                ed.WriteMessage("\n[НВК] NVK-READ-PIPE: " + ex.Message);
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-SURFACES")]
        public static ResultBuffer ListSurfaces(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            var rb = new ResultBuffer();
            rb.Add(new TypedValue((int)LispDataType.ListBegin));
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var s in SurfaceSampler.ListAll(doc.Database, tr))
                {
                    rb.Add(new TypedValue((int)LispDataType.ListBegin));
                    rb.Add(new TypedValue((int)LispDataType.Text, s.Handle));
                    rb.Add(new TypedValue((int)LispDataType.Text, s.Name));
                    rb.Add(new TypedValue((int)LispDataType.ListEnd));
                }
                tr.Commit();
            }
            rb.Add(new TypedValue((int)LispDataType.ListEnd));
            return rb;
        }

        /// <summary>
        /// (nvk-domain [stations] [bedding] [thk-zash] [joint] [surf-handle] [half-width])
        /// Трубы — текущий набор выбора или запрос AECC_PIPE. Ничего не рисует.
        /// </summary>
        [LispFunction("NVK-DOMAIN")]
        public static ResultBuffer Domain(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            var ed = doc.Editor;

            string stations = "AUTO";
            var cfg = new SectionConfig();
            string surfHandle = null;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && arr[0].Value is string s0 && s0.Length > 0) stations = s0;
                if (arr.Length >= 2 && IsNumber(arr[1])) cfg.Bedding = ToDouble(arr[1]);
                if (arr.Length >= 3 && IsNumber(arr[2])) cfg.ThkZash = ToDouble(arr[2]);
                if (arr.Length >= 4 && arr[3].Value is string j) cfg.Joint = ParseJoint(j);
                if (arr.Length >= 5 && arr[4].Value is string sh && sh.Length > 0) surfHandle = sh;
                if (arr.Length >= 6 && IsNumber(arr[5])) cfg.HalfWidth = ToDouble(arr[5]);
                if (arr.Length >= 7 && IsNumber(arr[6])) cfg.SlopeM = ToDouble(arr[6]);
                if (arr.Length >= 8 && IsNumber(arr[7])) cfg.ThkPavement = ToDouble(arr[7]);
                if (arr.Length >= 9 && IsNumber(arr[8])) cfg.ThkGazn = ToDouble(arr[8]);
                if (arr.Length >= 10 && IsNumber(arr[9])) cfg.ThkTrot = ToDouble(arr[9]);
            }
            if (cfg.HalfWidth < 0.5) cfg.HalfWidth = 3.0;
            if (cfg.SlopeM < 0.0) cfg.SlopeM = 0.0;
            if (cfg.ThkPavement < 0.0) cfg.ThkPavement = 0.0;
            if (cfg.ThkGazn < 0.0) cfg.ThkGazn = 0.0;
            if (cfg.ThkTrot < 0.0) cfg.ThkTrot = 0.0;
            if (doc != null)
                NvkUiState.LoadFromDatabase(doc.Database, doc.Name, false);
            NvkUiState.ApplyTo(cfg, NvkSession.Tbl());
            if (!string.IsNullOrEmpty(NvkUiState.DesignHandle))
                surfHandle = NvkUiState.DesignHandle;
            if (!string.IsNullOrEmpty(NvkUiState.Zones))
                cfg.Zones = RoadZones.Parse(NvkUiState.Zones, 0.0);
            cfg.ApplyPavement();

            var pipeIds = PickPipes(ed);
            if (pipeIds == null || pipeIds.Count == 0)
                return Text("[НВК] трубы не выбраны.");

            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var surfs = SurfaceSampler.ListAll(doc.Database, tr);
                    ObjectId surfId = ObjectId.Null;
                    string surfName = null;
                    if (!string.IsNullOrEmpty(surfHandle))
                    {
                        surfId = SurfaceSampler.ByHandle(doc.Database, surfHandle);
                        foreach (var s in surfs)
                            if (s.Id == surfId) { surfName = s.Name; break; }
                    }
                    else if (surfs.Count == 1)
                    {
                        surfId = surfs[0].Id;
                        surfName = surfs[0].Name;
                    }
                    else if (surfs.Count > 1)
                    {
                        tr.Commit();
                        var pso = new PromptEntityOptions("\n[НВК] Укажи TIN-поверхность <Enter = без TIN>: ");
                        pso.AllowNone = true;
                        pso.SetRejectMessage("\nЭто не поверхность.");
                        pso.AddAllowedClass(typeof(TinSurface), exactMatch: false);
                        var psr = ed.GetEntity(pso);
                        using (var tr2 = doc.TransactionManager.StartTransaction())
                        {
                            if (psr.Status == PromptStatus.OK)
                            {
                                surfId = psr.ObjectId;
                                var obj = tr2.GetObject(surfId, OpenMode.ForRead);
                                surfName = Safe.PropString(obj, "Name");
                            }
                            var dump2 = DomainService.BuildDump(tr2, pipeIds, surfId, surfName, cfg, stations);
                            tr2.Commit();
                            NvkLog.Write(dump2);
                            return Text("[НВК] домен OK, лог " + NvkLog.FilePath());
                        }
                    }

                    var dump = DomainService.BuildDump(tr, pipeIds, surfId, surfName, cfg, stations);
                    tr.Commit();
                    NvkLog.Write(dump);
                    return Text("[НВК] домен OK, лог " + NvkLog.FilePath());
                }
            }
            catch (Exc ex)
            {
                ed.WriteMessage("\n[НВК] NVK-DOMAIN: " + ex.Message);
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-DOMAIN-INFO")]
        public static ResultBuffer DomainInfo(ResultBuffer args)
        {
            var last = NvkSession.Last;
            if (last == null || last.Chain == null)
                return Text("");
            string what = "CHAIN";
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && arr[0].Value is string s && s.Length > 0)
                    what = s.ToUpperInvariant();
            }
            if (what == "SEG") return Text(last.SegId ?? "");
            if (what == "DUMP") return Text(last.Dump ?? "");
            if (what == "STATIONS")
            {
                var parts = new List<string>();
                foreach (var g in last.Geoms)
                    parts.Add(g.Elev.Station.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                return Text(last.Geoms.Count + " шт: " + string.Join(", ", parts.ToArray()));
            }
            if (what == "SURF")
            {
                double zmin = double.MaxValue, zmax = double.MinValue;
                int nSamp = 0;
                foreach (var g in last.Geoms)
                {
                    if (g.Elev.Surface < zmin) zmin = g.Elev.Surface;
                    if (g.Elev.Surface > zmax) zmax = g.Elev.Surface;
                    if (g.SurfPts != null && g.SurfPts.Count > nSamp) nSamp = g.SurfPts.Count;
                }
                double hw = last.Cfg != null ? last.Cfg.HalfWidth : 0.0;
                return Text("TIN=" + (last.SurfName ?? "-")
                    + "  Z=" + zmin.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    + "…" + zmax.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    + "  ±" + hw.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " м"
                    + "  проб/ст=" + nSamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return Text("труб " + NvkSession.PipeSegCount()
                + "  длина " + NvkSession.PipeTotalLen().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " м");
        }

        /// <summary>(nvk-layout-put ep1x ep1y ... ep5x ep5y cs1x cs1y ... cs5y). 1e20 = нет точки.</summary>
        [LispFunction("NVK-LAYOUT-PUT")]
        public static ResultBuffer LayoutPut(ResultBuffer args)
        {
            for (int i = 0; i < 5; i++)
            {
                NvkSession.EpHas[i] = false;
                NvkSession.CsHas[i] = false;
            }
            if (args == null) return Text("layout 0");
            var arr = args.AsArray();
            int n = 0;
            for (int i = 0; i < 5 && (i * 2 + 1) < arr.Length; i++)
            {
                if (!IsNumber(arr[i * 2]) || !IsNumber(arr[i * 2 + 1])) continue;
                double x = ToDouble(arr[i * 2]), y = ToDouble(arr[i * 2 + 1]);
                if (x > 1e19 || y > 1e19) continue;
                NvkSession.EpLvl[i] = new Point3d(x, y, 0.0);
                NvkSession.EpHas[i] = true;
                n++;
            }
            for (int i = 0; i < 5 && (10 + i * 2 + 1) < arr.Length; i++)
            {
                if (!IsNumber(arr[10 + i * 2]) || !IsNumber(arr[10 + i * 2 + 1])) continue;
                double x = ToDouble(arr[10 + i * 2]), y = ToDouble(arr[10 + i * 2 + 1]);
                if (x > 1e19 || y > 1e19) continue;
                NvkSession.CsLvl[i] = new Point3d(x, y, 0.0);
                NvkSession.CsHas[i] = true;
                n++;
            }
            return Text("layout " + n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        [LispFunction("NVK-MOVE")]
        public static ResultBuffer MoveLisp(ResultBuffer args)
        {
            return NvkMover.MoveLisp(args);
        }

        [LispFunction("NVK-LAYOUT-GET")]
        public static ResultBuffer LayoutGet(ResultBuffer args)
        {
            var rb = new ResultBuffer();
            for (int i = 0; i < 5; i++)
            {
                if (NvkSession.EpHas[i])
                {
                    rb.Add(new TypedValue((int)LispDataType.Text, "EP" + (i + 1)));
                    rb.Add(new TypedValue((int)LispDataType.Double, NvkSession.EpLvl[i].X));
                    rb.Add(new TypedValue((int)LispDataType.Double, NvkSession.EpLvl[i].Y));
                }
                if (NvkSession.CsHas[i])
                {
                    rb.Add(new TypedValue((int)LispDataType.Text, "CS" + (i + 1)));
                    rb.Add(new TypedValue((int)LispDataType.Double, NvkSession.CsLvl[i].X));
                    rb.Add(new TypedValue((int)LispDataType.Double, NvkSession.CsLvl[i].Y));
                }
            }
            return rb;
        }

        /// <summary>(nvk-cs-draw x y [scale] [slope] [as-block 0/1]) — поперечники. Нужен кэш домена.</summary>
        [LispFunction("NVK-CS-DRAW")]
        public static ResultBuffer CsDrawLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            if (NvkSession.Last == null)
                return Text("[НВК] CS-DRAW: нет домена. Сначала НВК-ДАМП.");

            double x = 0, y = 0, scale = 1.0, slope = 0.0;
            bool asBlock = true;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0])) x = ToDouble(arr[0]);
                if (arr.Length >= 2 && IsNumber(arr[1])) y = ToDouble(arr[1]);
                if (arr.Length >= 3 && IsNumber(arr[2])) scale = ToDouble(arr[2]);
                if (arr.Length >= 4 && IsNumber(arr[3])) slope = ToDouble(arr[3]);
                if (arr.Length >= 5 && IsNumber(arr[4])) asBlock = ToDouble(arr[4]) > 0.5;
                if (arr.Length >= 6 && IsNumber(arr[5])) NvkSession.CsYgap = ToDouble(arr[5]);
            }
            if (scale <= 0.0) scale = 1.0;
            if (NvkSession.CsYgap < 0.0) NvkSession.CsYgap = 0.0;
            CaptureScale();

            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = CsDrawer.Draw(doc.Database, tr, new Point3d(x, y, 0.0), scale, slope, asBlock);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] CS-DRAW: " + ex.Message);
                return Text(ex.Message);
            }
        }

        /// <summary>(nvk-ep-draw x y [l-scale] [h-scale] [as-block 0/1]) — продольники.</summary>
        [LispFunction("NVK-EP-DRAW")]
        public static ResultBuffer EpDrawLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            if (NvkSession.Last == null || NvkSession.Last.Axis == null)
                return Text("[НВК] EP: нет домена. Сначала НВК-ДАМП.");

            double x = 0, y = 0, lSc = 1.0, hSc = 10.0;
            bool asBlock = true;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0])) x = ToDouble(arr[0]);
                if (arr.Length >= 2 && IsNumber(arr[1])) y = ToDouble(arr[1]);
                if (arr.Length >= 3 && IsNumber(arr[2])) lSc = ToDouble(arr[2]);
                if (arr.Length >= 4 && IsNumber(arr[3])) hSc = ToDouble(arr[3]);
                if (arr.Length >= 5 && IsNumber(arr[4])) asBlock = ToDouble(arr[4]) > 0.5;
            }
            if (lSc <= 0.0) lSc = 1.0;
            if (hSc <= 0.0) hSc = 10.0;
            CaptureScale();

            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = EpDrawer.Draw(doc.Database, tr, new Point3d(x, y, 0.0), lSc, hSc, asBlock);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] EP: " + ex.Message);
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-POPER-DRAW")]
        public static ResultBuffer PoperDrawLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            CaptureScale();
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = PoperDrawer.Draw(doc.Database, tr);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] POPER: " + ex.Message);
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-VOL-DRAW")]
        public static ResultBuffer VolDrawLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            double x = 0, y = 0;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0])) x = ToDouble(arr[0]);
                if (arr.Length >= 2 && IsNumber(arr[1])) y = ToDouble(arr[1]);
            }
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = VolDrawer.Draw(doc.Database, tr, new Point3d(x, y, 0.0));
                    tr.Commit();
                    NvkLog.Write(msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-PLAN-DRAW")]
        public static ResultBuffer PlanDrawLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = PlanDrawer.Draw(doc.Database, tr);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                return Text(ex.Message);
            }
        }

        [LispFunction("NVK-XY-TO-CH")]
        public static ResultBuffer XyToCh(ResultBuffer args)
        {
            double x = 0, y = 0;
            if (args != null)
            {
                var arr = args.AsArray();
                if (arr.Length >= 1 && IsNumber(arr[0])) x = ToDouble(arr[0]);
                if (arr.Length >= 2 && IsNumber(arr[1])) y = ToDouble(arr[1]);
            }
            var last = NvkSession.Last;
            if (last == null || last.Chain == null)
                return new ResultBuffer(new TypedValue((int)LispDataType.Double, -1.0));
            double ch = last.Chain.Project(x, y);
            return new ResultBuffer(new TypedValue((int)LispDataType.Double, ch));
        }

        [LispFunction("NVK-CACHE-GET")]
        public static ResultBuffer CacheGet(ResultBuffer args)
        {
            var rb = new ResultBuffer();
            rb.Add(new TypedValue((int)LispDataType.Text, "SCATTER"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheScatter)));
            rb.Add(new TypedValue((int)LispDataType.Text, "SCATTER2"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheScatter2)));
            rb.Add(new TypedValue((int)LispDataType.Text, "SCATTER3"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheScatter3)));
            rb.Add(new TypedValue((int)LispDataType.Text, "SCATTER4"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheScatter4)));
            rb.Add(new TypedValue((int)LispDataType.Text, "SCATTER5"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheScatter5)));
            rb.Add(new TypedValue((int)LispDataType.Text, "LATOFF"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CacheLatOff)));
            rb.Add(new TypedValue((int)LispDataType.Text, "PLAN_TGT"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CachePlanTgt)));
            rb.Add(new TypedValue((int)LispDataType.Text, "PLAN_LAT"));
            rb.Add(new TypedValue((int)LispDataType.Text, JoinNums(NvkSession.CachePlanLat)));
            rb.Add(new TypedValue((int)LispDataType.Text, "PLAN_LDR"));
            rb.Add(new TypedValue((int)LispDataType.Text, NvkSession.CachePlanLdr ?? ""));
            rb.Add(new TypedValue((int)LispDataType.Text, "ZONES"));
            rb.Add(new TypedValue((int)LispDataType.Text, NvkUiState.Zones ?? ""));
            return rb;
        }

        [LispFunction("NVK-CACHE-PUT")]
        public static ResultBuffer CachePut(ResultBuffer args)
        {
            if (args == null) return Text("ok");
            var arr = args.AsArray();
            for (int i = 0; i + 1 < arr.Length; i += 2)
            {
                string k = arr[i].Value as string;
                string v = arr[i + 1].Value as string;
                if (k == null) continue;
                k = k.ToUpperInvariant();
                if (k == "SCATTER") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheScatter = t; }
                else if (k == "SCATTER2") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheScatter2 = t; }
                else if (k == "SCATTER3") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheScatter3 = t; }
                else if (k == "SCATTER4") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheScatter4 = t; }
                else if (k == "SCATTER5") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheScatter5 = t; }
                else if (k == "LATOFF") { var t = SplitNums(v); if (t.Count >= 2) NvkSession.CacheLatOff = t; }
                else if (k == "PLAN_TGT") { var t = SplitNums(v); if (t.Count > 0) NvkSession.CachePlanTgt = t; }
                else if (k == "PLAN_LAT") { var t = SplitNums(v); if (t.Count > 0) NvkSession.CachePlanLat = t; }
                else if (k == "PLAN_LDR") NvkSession.CachePlanLdr = v ?? "";
                else if (k == "ZONES") NvkUiState.Zones = v ?? "";
                else if (k == "FRESH" && v == "1")
                {
                    NvkSession.FreshRandom = true;
                    NvkSession.FreshRandomKind = 0;
                }
            }
            return Text("ok");
        }

        static string JoinNums(List<double> a)
        {
            if (a == null || a.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < a.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(a[i].ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        static List<double> SplitNums(string s)
        {
            var r = new List<double>();
            if (string.IsNullOrWhiteSpace(s)) return r;
            foreach (var tok in s.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double x;
                if (double.TryParse(tok.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out x))
                    r.Add(x);
            }
            return r;
        }

        [LispFunction("NVK-CLEAN")]
        public static ResultBuffer CleanLisp(ResultBuffer args)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return Text("нет документа");
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = NvkClean.Run(doc.Database, tr);
                    tr.Commit();
                    doc.Editor.WriteMessage("\n" + msg);
                    return Text(msg);
                }
            }
            catch (Exc ex)
            {
                return Text(ex.Message);
            }
        }

        [CommandMethod("NVKDUMP")]
        public void DumpCmd()
        {
            NvkPipeline.Dump();
        }

        [CommandMethod("NVKDLLINFO")]
        public void DllInfo()
        {
            NvkPipeline.Info();
        }

        static List<ObjectId> PickPipes(Editor ed)
        {
            var sel = ed.SelectImplied();
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
            {
                var pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n[НВК] Выбери трубы Civil 3D: ";
                var filter = new SelectionFilter(new[] { new TypedValue(0, "AECC_PIPE") });
                sel = ed.GetSelection(pso, filter);
            }
            if (sel.Status != PromptStatus.OK || sel.Value == null)
                return null;
            var ids = new List<ObjectId>();
            foreach (SelectedObject so in sel.Value)
            {
                if (so != null) ids.Add(so.ObjectId);
            }
            NvkSession.LastPipes = ids;
            return ids;
        }

        static ResultBuffer Text(string s)
        {
            return new ResultBuffer(new TypedValue((int)LispDataType.Text, s));
        }

        static ResultBuffer Real(double v)
        {
            return new ResultBuffer(new TypedValue((int)LispDataType.Double, v));
        }

        static bool IsNumber(TypedValue tv)
        {
            return tv.TypeCode == (int)LispDataType.Double
                || tv.TypeCode == (int)LispDataType.Int32
                || tv.TypeCode == (int)LispDataType.Int16;
        }

        static double ToDouble(TypedValue tv)
        {
            return Convert.ToDouble(tv.Value);
        }

        static void CaptureScale()
        {
            NvkSession.Cannoscale = ReadCannoscale();
        }

        static double ReadCannoscale()
        {
            try
            {
                var o = AcadApp.GetSystemVariable("CANNOSCALE");
                string s = o as string;
                if (!string.IsNullOrEmpty(s))
                {
                    int k = s.IndexOf(':');
                    if (k >= 0 && k < s.Length - 1)
                    {
                        double d;
                        if (double.TryParse(s.Substring(k + 1),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out d)
                            && d > 0.0)
                            return d;
                    }
                }
            }
            catch (Exc) { }
            try
            {
                var ds = AcadApp.GetSystemVariable("DIMSCALE");
                double v = Convert.ToDouble(ds);
                if (v > 0.0) return v;
            }
            catch (Exc) { }
            return 1000.0;
        }

        static JointType ParseJoint(string s)
        {
            if (s == null) return JointType.Socket;
            s = s.ToUpperInvariant();
            if (s == "WELD" || s == "СВАРНОЕ") return JointType.Weld;
            if (s == "COUPLING" || s == "МУФТОВОЕ") return JointType.Coupling;
            return JointType.Socket;
        }
    }
}
