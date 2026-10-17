using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Конвейер этапов. Бывший nvk-orchestrator.lsp + nvk-input/cs/ep/planvol.</summary>
    static class NvkPipeline
    {
        public static void Run()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            NvkSession.RunId++;
            NvkSession.ClearSheetWins();
            NvkLog.BeginRun(NvkSession.RunId, NvkSession.Version);
            ed.WriteMessage("\n[НВК] ПОСТРОИТЬ #" + NvkSession.RunId
                + "  лог: " + NvkLog.FilePath());

            object[] quiet = null;
            int ok = 0, skip = 0, fail = 0;
            try
            {
                quiet = NvkQuiet.Push();
                NvkLog.Write("[НВК] вкладка Модель, regen выкл");

                Step("CONFIG", StageConfig, ref ok, ref skip, ref fail);
                Step("INPUT", () => StageInput(false), ref ok, ref skip, ref fail);
                StageDomain();
                DomainOtherSites();
                bool domainOk = NvkSession.Last != null && NvkSession.Last.Chain != null;
                LogStage("CHAIN", domainOk ? DomainInfo("CHAIN") : null, ref ok, ref fail);
                LogStage("STATIONS", domainOk ? DomainInfo("STATIONS") : null, ref ok, ref fail);
                LogStage("SURF", domainOk ? DomainInfo("SURF") : null, ref ok, ref fail);
                LogStage("GEOM-CS", domainOk ? "стек линий — лог CHAIN" : null, ref ok, ref fail);

                if (NvkUiState.DoCs) Step("CS-DRAW", StageCs, ref ok, ref skip, ref fail);
                else Skip("CS-DRAW", ref skip);
                if (NvkUiState.DoEp) Step("EP", StageEp, ref ok, ref skip, ref fail);
                else Skip("EP", ref skip);
                if (NvkUiState.DoPoper) Step("POPER", StagePoper, ref ok, ref skip, ref fail);
                else Skip("POPER", ref skip);
                if (NvkUiState.DoVol) Step("VOL", StageVol, ref ok, ref skip, ref fail);
                else Skip("VOL", ref skip);
                if (NvkUiState.DoPlan) Step("PLAN", StagePlan, ref ok, ref skip, ref fail);
                else Skip("PLAN", ref skip);
                if (NvkUiState.DoSheets) Step("SHEETS", StageSheets, ref ok, ref skip, ref fail);
                else Skip("SHEETS", ref skip);

                string done = "[НВК] ========== готово   OK " + ok
                    + "  SKIP " + skip + "  FAIL " + fail + " ==========";
                NvkLog.Write(done);
                ed.WriteMessage("\n" + done);
                NvkLog.Write("[НВК] NOD…");
                SaveNod();
                NvkLog.Write("[НВК] NOD ок");
                NvkPalette.Reload();
            }
            finally
            {
                NvkQuiet.Pop(quiet);
                NvkLog.Write("[НВК] тихий режим снят");
                try
                {
                    doc.Editor.Regen();
                    doc.Editor.UpdateScreen();
                    NvkLog.Write("[НВК] экран обновлён");
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] REGEN " + ex.Message);
                }
                NvkPalette.SetStatus("Готово");
            }
        }

        public static string Dump()
        {
            StageConfig();
            StageInput(false);
            var dump = StageDomain();
            DomainOtherSites();
            return dump;
        }

        public static string StageConfig()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return "нет документа";
            NvkLayers.EnsureAll(doc.Database);
            ApplyTable();
            return "AppID NVK, слои NVK-*, таблица PoperSwift";
        }

        public static string StageInput(bool force)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var ed = doc.Editor;
            var pipes = AlivePipes();
            if (pipes.Count > 0 && !force)
            {
                NvkSession.LastPipes = pipes;
                NvkSites.RememberPipes();
                NvkSites.RefreshNameFromPipes(doc.Database);
                ed.WriteMessage("\n[НВК] INPUT: труб " + pipes.Count
                    + "  TIN=" + (string.IsNullOrEmpty(NvkUiState.DesignName) ? "-" : NvkUiState.DesignName)
                    + "  (НВК-ВВОД — сменить)");
                return "труб " + pipes.Count;
            }

            ed.WriteMessage("\n[НВК] INPUT: выбери трубы Civil 3D");
            var ids = PickPipes(ed, pipes.Count > 0);
            if (ids == null || ids.Count == 0)
            {
                if (pipes.Count > 0)
                {
                    NvkSession.LastPipes = pipes;
                    NvkSites.RememberPipes();
                    NvkSites.RefreshNameFromPipes(doc.Database);
                    ed.WriteMessage("\n[НВК] оставляю прежние трубы: " + pipes.Count);
                    return "труб " + pipes.Count;
                }
                ed.WriteMessage("\n[НВК] трубы не выбраны.");
                return null;
            }
            NvkSession.LastPipes = ids;
            NvkSites.RememberPipes();
            NvkSites.RefreshNameFromPipes(doc.Database);
            ed.WriteMessage("\n[НВК] труб: " + ids.Count);
            PickTin(ed, doc, false);
            SaveNod();
            return "труб " + ids.Count;
        }

        public static string StageDomain()
        {
            return StageDomain(true);
        }

        static string StageDomain(bool promptTin)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var ed = doc.Editor;
            var pipes = AlivePipes();
            if (pipes.Count == 0)
            {
                if (!promptTin) return null;
                var msg = StageInput(false);
                pipes = AlivePipes();
                if (pipes.Count == 0) return msg;
            }

            ApplyTable();
            var cfg = NewCfg();
            var tbl = NvkSession.Tbl();
            NvkUiState.ApplyTo(cfg, tbl);
            NvkSession.Table = tbl;
            string stations = string.IsNullOrWhiteSpace(NvkUiState.Stations) ? "AUTO" : NvkUiState.Stations;
            string surfHandle = NvkUiState.DesignHandle;
            CaptureCannoscale();

            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var surfs = SurfaceSampler.ListAll(doc.Database, tr);
                    ObjectId surfId = ObjectId.Null;
                    string surfName = NvkUiState.DesignName;
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
                        NvkUiState.DesignHandle = surfs[0].Handle;
                        NvkUiState.DesignName = surfName;
                    }
                    else if (surfs.Count > 1)
                    {
                        if (!promptTin)
                        {
                            surfId = surfs[0].Id;
                            surfName = surfs[0].Name;
                        }
                        else
                        {
                            tr.Commit();
                            PickTin(ed, doc, true);
                            using (var tr2 = doc.TransactionManager.StartTransaction())
                            {
                                ObjectId sid = ObjectId.Null;
                                string sn = NvkUiState.DesignName;
                                if (!string.IsNullOrEmpty(NvkUiState.DesignHandle))
                                    sid = SurfaceSampler.ByHandle(doc.Database, NvkUiState.DesignHandle);
                                var dump2 = DomainService.BuildDump(tr2, pipes, sid, sn, cfg, stations);
                                tr2.Commit();
                                NvkLog.Write(dump2);
                                return dump2;
                            }
                        }
                    }

                    var dump = DomainService.BuildDump(tr, pipes, surfId, surfName, cfg, stations);
                    tr.Commit();
                    NvkLog.Write(dump);
                    return dump;
                }
            }
            catch (Exc ex)
            {
                ed.WriteMessage("\n[НВК] домен: " + ex.Message);
                return null;
            }
        }

        static void DomainOtherSites()
        {
            if (!NvkUiState.VolJoinSites) return;
            NvkSites.EnsureOne();
            if (NvkSites.All.Count < 2) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            NvkSites.Capture();
            int cur = NvkSites.Index;
            try
            {
                for (int i = 0; i < NvkSites.All.Count; i++)
                {
                    if (i == cur) continue;
                    var s = NvkSites.All[i];
                    if (s == null || string.IsNullOrEmpty(s.Pipes)) continue;
                    NvkSites.Index = i;
                    NvkSites.Apply(s, doc.Database);
                    if (AlivePipes().Count == 0) continue;
                    StageDomain(false);
                    NvkSites.Capture();
                }
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] домен других участков: " + ex.Message);
            }
            finally
            {
                NvkSites.Index = cur;
                NvkSites.Apply(NvkSites.Current, doc.Database);
            }
        }

        public static string StageCs()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            if (NvkSession.Last == null)
            {
                if (StageDomain() == null || NvkSession.Last == null)
                    return null;
            }
            if (!NvkSession.CsInsHas)
            {
                var p = doc.Editor.GetPoint("\n[НВК] CS-DRAW точка: ");
                if (p.Status != PromptStatus.OK) return null;
                NvkSession.CsIns = p.Value;
                NvkSession.CsInsHas = true;
            }
            ApplyTable();
            CaptureCannoscale();
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = CsDrawer.Draw(doc.Database, tr, NvkSession.CsIns,
                        NvkUiState.CsScale, NvkUiState.SlopeM, NvkUiState.BlockCs);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return msg;
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] CS-DRAW: " + ex.Message);
                return null;
            }
        }

        public static string StageEp()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            if (NvkSession.Last == null || NvkSession.Last.Axis == null)
            {
                if (StageDomain() == null || NvkSession.Last == null)
                    return null;
            }
            if (!NvkSession.EpInsHas)
            {
                if (NvkSession.CsInsHas)
                {
                    NvkSession.EpIns = new Point3d(NvkSession.CsIns.X + 80.0, NvkSession.CsIns.Y, 0.0);
                    NvkSession.EpInsHas = true;
                }
                else
                {
                    var p = doc.Editor.GetPoint("\n[НВК] EP точка: ");
                    if (p.Status != PromptStatus.OK) return null;
                    NvkSession.EpIns = p.Value;
                    NvkSession.EpInsHas = true;
                }
            }
            CaptureCannoscale();
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = EpDrawer.Draw(doc.Database, tr, NvkSession.EpIns,
                        NvkUiState.LScale, NvkUiState.HScale, false);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return msg;
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] EP: " + ex.Message);
                return null;
            }
        }

        public static string StagePoper()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            if (NvkSession.LastEp == null)
            {
                var ep = StageEp();
                if (ep == null) return null;
            }
            ApplyTable();
            CaptureCannoscale();
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = PoperDrawer.Draw(doc.Database, tr);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return msg;
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] POPER: " + ex.Message);
                return null;
            }
        }

        public static string StageVol()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            if (NvkSession.Last == null)
            {
                if (StageDomain() == null) return null;
            }
            if (!NvkSession.VolInsHas)
            {
                if (NvkSession.EpInsHas)
                {
                    NvkSession.VolIns = new Point3d(NvkSession.EpIns.X, NvkSession.EpIns.Y - 40.0, 0.0);
                    NvkSession.VolInsHas = true;
                }
                else if (NvkSession.CsInsHas)
                {
                    NvkSession.VolIns = new Point3d(NvkSession.CsIns.X, NvkSession.CsIns.Y - 80.0, 0.0);
                    NvkSession.VolInsHas = true;
                }
                else
                {
                    var p = doc.Editor.GetPoint("\n[НВК] VOL точка: ");
                    if (p.Status != PromptStatus.OK) return null;
                    NvkSession.VolIns = p.Value;
                    NvkSession.VolInsHas = true;
                }
            }
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = VolDrawer.Draw(doc.Database, tr, NvkSession.VolIns);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return msg;
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] VOL: " + ex.Message);
                return null;
            }
        }

        public static string StagePlan()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            if (NvkSession.Last == null)
            {
                if (StageDomain() == null) return null;
            }
            try
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var msg = PlanDrawer.Draw(doc.Database, tr);
                    tr.Commit();
                    NvkLog.Write(msg);
                    return msg;
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] PLAN: " + ex.Message);
                return null;
            }
        }

        public static string StageSheets()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            try
            {
                var msg = NvkSheets.Build(doc);
                NvkLog.Write(msg);
                doc.Editor.WriteMessage("\n" + msg);
                return msg;
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] ЛИСТЫ FAIL " + ex.Message);
                doc.Editor.WriteMessage("\n[НВК] ЛИСТЫ: " + ex.Message);
                return null;
            }
        }

        public static string Clean()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return "нет документа";
            try
            {
                string msg;
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    msg = NvkClean.Run(doc.Database, tr);
                    tr.Commit();
                }
                int nLay = NvkSheets.DeleteLayouts();
                if (nLay > 0) msg += "  листов " + nLay;
                NvkSession.ClearSheetWins();
                doc.Editor.WriteMessage("\n" + msg);
                return msg;
            }
            catch (Exc ex)
            {
                return ex.Message;
            }
        }

        public static void ResetInserts()
        {
            NvkSession.CsInsHas = false;
            NvkSession.EpInsHas = false;
            NvkSession.VolInsHas = false;
            for (int i = 0; i < 5; i++)
            {
                NvkSession.EpHas[i] = false;
                NvkSession.CsHas[i] = false;
            }
            NvkSession.CacheScatter = null;
            NvkSession.CacheScatter2 = null;
            NvkSession.CacheScatter3 = null;
            NvkSession.CacheScatter4 = null;
            NvkSession.CacheScatter5 = null;
            NvkSession.CacheLatOff = null;
            NvkSession.CachePlanTgt = null;
            NvkSession.CachePlanLat = null;
            NvkSession.FreshRandom = true;
            NvkSession.FreshRandomKind = 0;
            SaveNod();
        }

        public static void Info()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var last = NvkSession.Last;
            int nPipe = AlivePipes().Count;
            ed.WriteMessage("\n");
            ed.WriteMessage("╔══════════════════════════════════════════════╗");
            ed.WriteMessage("\n║  Поперечник НВК  " + NvkSession.Version);
            ed.WriteMessage("\n╠══════════════════════════════════════════════╣");
            ed.WriteMessage("\n║  папка  " + (NvkSession.RootDir ?? "-"));
            ed.WriteMessage("\n║  DLL    да  NVK ok " + NvkSession.Version);
            ed.WriteMessage("\n║  труб   " + (nPipe > 0 ? nPipe.ToString() : "нет")
                + "  TIN=" + (string.IsNullOrEmpty(NvkUiState.DesignName) ? "-" : NvkUiState.DesignName));
            ed.WriteMessage("\n║  CS pt  " + FmtPt(NvkSession.CsInsHas, NvkSession.CsIns)
                + "  EP pt " + FmtPt(NvkSession.EpInsHas, NvkSession.EpIns));
            if (last != null && last.Chain != null)
                ed.WriteMessage("\n║  домен  труб " + last.Chain.Segs.Count
                    + "  L=" + last.Chain.Total.ToString("0.0") + " м"
                    + "  CS=" + (last.Geoms == null ? 0 : last.Geoms.Count));
            ed.WriteMessage("\n╠══════════════════════════════════════════════╣");
            ed.WriteMessage("\n║  НВК-ПАНЕЛЬ     панель                       ║");
            ed.WriteMessage("\n║  НВК-ПОСТРОИТЬ  все этапы                    ║");
            ed.WriteMessage("\n║  НВК-ЛИСТЫ      вкладки + видовые экраны     ║");
            ed.WriteMessage("\n║  НВК-ВВОД       трубы + TIN                  ║");
            ed.WriteMessage("\n║  лог    " + NvkLog.FilePath());
            ed.WriteMessage("\n╚══════════════════════════════════════════════╝");
        }

        public static void SaveNod()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try { NvkNod.WriteCurrent(doc.Database); }
            catch (Exc) { }
            try { NvkProfile.Save(); }
            catch (Exc) { }
        }

        public static void CaptureCannoscale()
        {
            NvkSession.Cannoscale = ReadCannoscale();
        }

        public static void ApplyTable()
        {
            var t = NvkSession.Tbl();
            t.TextH = NvkUiState.TextH;
            t.OffsetPct = NvkUiState.TextOff;
            t.CsTextH = NvkUiState.CsTextH;
            t.SlopeUnit = string.IsNullOrEmpty(NvkUiState.SlopeUnit) ? "permille" : NvkUiState.SlopeUnit;
            t.ShowPermille = NvkUiState.ShowPermille;
            t.LabelColW = NvkUiState.LabelColW;
            if (t.TextH <= 0.0) t.TextH = 2.5;
            if (t.CsTextH <= 0.0) t.CsTextH = 0.5;
            if (t.OffsetPct < 0.0) t.OffsetPct = 0.0;
            if (t.OffsetPct > 100.0) t.OffsetPct = 100.0;
            NvkUiState.ApplyTo(NewCfg(), t);
            NvkSession.Table = t;
            NvkSession.CsYgap = NvkUiState.CsYgap < 0.0 ? 0.0 : NvkUiState.CsYgap;
        }

        static SectionConfig NewCfg()
        {
            var cfg = new SectionConfig();
            cfg.Bedding = NvkUiState.Bedding;
            cfg.ThkZash = NvkUiState.ThkZash;
            cfg.Joint = ParseJoint(NvkUiState.Joint);
            cfg.HalfWidth = NvkUiState.HalfWidth < 0.5 ? 3.0 : NvkUiState.HalfWidth;
            cfg.SlopeL = NvkUiState.SlopeL < 0.0 ? 0.0 : NvkUiState.SlopeL;
            cfg.SlopeR = NvkUiState.SlopeR < 0.0 ? 0.0 : NvkUiState.SlopeR;
            cfg.SlopeM = cfg.SlopeL > cfg.SlopeR ? cfg.SlopeL : cfg.SlopeR;
            cfg.TrenchLeft = NvkUiState.TrenchLeft < 0.0 ? 0.0 : NvkUiState.TrenchLeft;
            cfg.TrenchRight = NvkUiState.TrenchRight < 0.0 ? 0.0 : NvkUiState.TrenchRight;
            cfg.ThkPavement = NvkUiState.ThkAsph < 0.0 ? 0.0 : NvkUiState.ThkAsph;
            cfg.ThkGazn = NvkUiState.ThkGazn < 0.0 ? 0.0 : NvkUiState.ThkGazn;
            cfg.ThkTrot = NvkUiState.ThkTrot < 0.0 ? 0.0 : NvkUiState.ThkTrot;
            return cfg;
        }

        static JointType ParseJoint(string s)
        {
            if (s == null) return JointType.Socket;
            s = s.ToUpperInvariant();
            if (s == "WELD" || s == "СВАРНОЕ") return JointType.Weld;
            if (s == "COUPLING" || s == "МУФТОВОЕ") return JointType.Coupling;
            return JointType.Socket;
        }

        static List<ObjectId> AlivePipes()
        {
            var r = new List<ObjectId>();
            if (NvkSession.LastPipes == null) return r;
            foreach (var id in NvkSession.LastPipes)
            {
                if (!id.IsNull && id.IsValid && !id.IsErased)
                    r.Add(id);
            }
            return r;
        }

        static List<ObjectId> PickPipes(Editor ed, bool hadPrev)
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
                return hadPrev ? AlivePipes() : null;
            var ids = new List<ObjectId>();
            foreach (SelectedObject so in sel.Value)
            {
                if (so != null) ids.Add(so.ObjectId);
            }
            return ids;
        }

        static void PickTin(Editor ed, Autodesk.AutoCAD.ApplicationServices.Document doc, bool promptIfMany)
        {
            if (!string.IsNullOrEmpty(NvkUiState.DesignHandle))
            {
                ed.WriteMessage("\n[НВК] TIN L0 с панели: "
                    + (string.IsNullOrEmpty(NvkUiState.DesignName)
                        ? NvkUiState.DesignHandle : NvkUiState.DesignName));
                return;
            }
            List<SurfInfo> surfs;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                surfs = SurfaceSampler.ListAll(doc.Database, tr);
                tr.Commit();
            }
            if (surfs == null || surfs.Count == 0)
            {
                ed.WriteMessage("\n[НВК] поверхностей нет.");
                return;
            }
            if (surfs.Count == 1)
            {
                NvkUiState.DesignHandle = surfs[0].Handle;
                NvkUiState.DesignName = surfs[0].Name;
                ed.WriteMessage("\n[НВК] TIN: " + surfs[0].Name);
                return;
            }
            if (!promptIfMany)
            {
                ed.WriteMessage("\n[НВК] TIN задай на панели (Покрытия → L0).");
                return;
            }
            var pso = new PromptEntityOptions("\n[НВК] Укажи TIN-поверхность <Enter = без TIN>: ");
            pso.AllowNone = true;
            pso.SetRejectMessage("\nЭто не поверхность.");
            pso.AddAllowedClass(typeof(TinSurface), exactMatch: false);
            var psr = ed.GetEntity(pso);
            if (psr.Status == PromptStatus.OK)
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var obj = tr.GetObject(psr.ObjectId, OpenMode.ForRead);
                    NvkUiState.DesignHandle = psr.ObjectId.Handle.ToString();
                    NvkUiState.DesignName = Safe.PropString(obj, "Name");
                    tr.Commit();
                }
            }
        }

        static string DomainInfo(string what)
        {
            var last = NvkSession.Last;
            if (last == null || last.Chain == null) return "";
            what = (what ?? "CHAIN").ToUpperInvariant();
            if (what == "SEG") return last.SegId ?? "";
            if (what == "STATIONS")
            {
                var parts = new List<string>();
                foreach (var g in last.Geoms)
                    parts.Add(g.Elev.Station.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                return last.Geoms.Count + " шт: " + string.Join(", ", parts.ToArray());
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
                return "TIN=" + (last.SurfName ?? "-")
                    + "  Z=" + zmin.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    + "…" + zmax.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    + "  ±" + hw.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " м"
                    + "  проб/ст=" + nSamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return "труб " + last.Chain.Segs.Count
                + "  длина " + last.Chain.Total.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " м";
        }

        static void Step(string name, Func<string> fn, ref int ok, ref int skip, ref int fail)
        {
            NvkLog.Write("---- " + name + " ----");
            string rez = null;
            try { rez = fn(); }
            catch (Exc ex) { rez = null; NvkLog.Write(" FAIL  " + name + "  |  " + ex.Message); }
            if (rez == null)
            {
                LogLine(name, "FAIL", "");
                fail++;
            }
            else
            {
                LogLine(name, "OK", rez);
                ok++;
            }
        }

        static void Skip(string name, ref int skip)
        {
            LogLine(name, "SKIP", "выкл. на панели");
            skip++;
        }

        static void LogStage(string name, string text, ref int ok, ref int fail)
        {
            NvkLog.Write("---- " + name + " ----");
            if (string.IsNullOrEmpty(text))
            {
                LogLine(name, "FAIL", "");
                fail++;
            }
            else
            {
                LogLine(name, "OK", text);
                ok++;
            }
        }

        static void LogLine(string name, string status, string text)
        {
            string pad = status == "OK" ? "  OK   " : status == "SKIP" ? " SKIP  " : " FAIL  ";
            string line = pad + name + (string.IsNullOrEmpty(text) ? "" : "  |  " + text);
            NvkLog.Write(line);
            if (status == "FAIL" || status == "SKIP")
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                    doc.Editor.WriteMessage("\n[НВК] " + status + "  " + name
                        + (string.IsNullOrEmpty(text) ? "" : "  |  " + text));
            }
        }

        static string FmtPt(bool has, Point3d p)
        {
            if (!has) return "-";
            return p.X.ToString("0.0") + "," + p.Y.ToString("0.0");
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
    }

    /// <summary>
    /// Глушит regen/аннотации и держит Модель, чтобы вкладки с видовыми
    /// не пересчитывали размеры на каждом объекте.
    /// </summary>
    static class NvkQuiet
    {
        public static object[] Push()
        {
            var old = new object[4];
            try { old[0] = AcadApp.GetSystemVariable("REGENMODE"); } catch { }
            try { old[1] = AcadApp.GetSystemVariable("ANNOAUTOSCALE"); } catch { }
            try { old[2] = AcadApp.GetSystemVariable("UPDATETHUMBNAIL"); } catch { }
            try { old[3] = AcadApp.GetSystemVariable("LAYOUTREGENCTL"); } catch { }
            try { AcadApp.SetSystemVariable("REGENMODE", 0); } catch { }
            try { AcadApp.SetSystemVariable("ANNOAUTOSCALE", 0); } catch { }
            try { AcadApp.SetSystemVariable("UPDATETHUMBNAIL", 0); } catch { }
            try { AcadApp.SetSystemVariable("LAYOUTREGENCTL", 1); } catch { }
            ToModel();
            return old;
        }

        public static void Pop(object[] old)
        {
            ToModel();
            if (old == null) return;
            try { if (old[2] != null) AcadApp.SetSystemVariable("UPDATETHUMBNAIL", old[2]); } catch { }
            try { if (old[3] != null) AcadApp.SetSystemVariable("LAYOUTREGENCTL", old[3]); } catch { }
            try { if (old[1] != null) AcadApp.SetSystemVariable("ANNOAUTOSCALE", old[1]); } catch { }
            try { if (old[0] != null) AcadApp.SetSystemVariable("REGENMODE", old[0]); } catch { }
        }

        public static void ToModel()
        {
            try
            {
                var lm = LayoutManager.Current;
                if (lm == null) return;
                string cur = null;
                try { cur = lm.CurrentLayout; } catch { }
                if (string.Equals(cur, "Model", StringComparison.OrdinalIgnoreCase))
                    return;
                lm.CurrentLayout = "Model";
            }
            catch (Exc) { }
        }
    }

    static class NvkSend
    {
        public static void Command(string name)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null || string.IsNullOrWhiteSpace(name)) return;
            string n = name.Trim();
            if (n.StartsWith("(C:", StringComparison.OrdinalIgnoreCase) && n.EndsWith(")"))
                n = n.Substring(3, n.Length - 4).Trim();
            else if (n.Length > 2 && n.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(2).Trim();
            doc.SendStringToExecute(n + " ", true, false, false);
        }
    }
}
