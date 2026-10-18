using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    sealed class NvkSite
    {
        public string Id = "";
        public string Name = "";
        public string Pipes = "";
        public Dictionary<string, string> State;
        public List<DomainResult> Runs;
    }

    /// <summary>Список участков чертежа. Панель показывает данные выбранного.</summary>
    static class NvkSites
    {
        public static readonly List<NvkSite> All = new List<NvkSite>();
        public static int Index;
        public static SiteLayout InsertTemplate;

        public static NvkSite Current
        {
            get
            {
                EnsureOne();
                if (Index < 0) Index = 0;
                if (Index >= All.Count) Index = All.Count - 1;
                return All[Index];
            }
        }

        public static string CurrentId
        {
            get
            {
                var s = Current;
                return s != null ? s.Id : "";
            }
        }

        public static void EnsureOne()
        {
            if (All.Count > 0) return;
            All.Add(Make("U1", "Участок 1"));
            Index = 0;
        }

        public static void Reset()
        {
            All.Clear();
            Index = 0;
            All.Add(Make("U1", "Участок 1"));
            InsertTemplate = null;
        }

        static NvkSite Make(string id, string name)
        {
            return new NvkSite { Id = id ?? "U1", Name = name ?? "Участок 1", Pipes = "" };
        }

        public static List<SiteRec> Recs()
        {
            var r = new List<SiteRec>(All.Count);
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                r.Add(new SiteRec(s.Id, s.Name, s.Pipes));
            }
            return r;
        }

        public static void PutInto(IDictionary<string, string> map)
        {
            if (map == null) return;
            EnsureOne();
            map["SITES"] = SitePack.Join(Recs());
            map["SITE"] = CurrentId ?? "";
        }

        public static void TakeFrom(Dictionary<string, string> map, Database db)
        {
            string packed = "";
            string active = "";
            if (map != null)
            {
                map.TryGetValue("SITES", out packed);
                map.TryGetValue("SITE", out active);
            }
            var recs = SitePack.Split(packed);
            All.Clear();
            NvkSession.Last = null;
            NvkSession.Runs = new List<DomainResult>();
            if (recs.Count == 0)
            {
                string seg = Get(map, "SEG");
                string pipes = Get(map, "PIPES");
                string id = string.IsNullOrEmpty(seg) ? "U1" : seg;
                var one = Make(id, "Участок 1");
                one.Pipes = pipes ?? "";
                one.State = Clone(map);
                All.Add(one);
                Index = 0;
                return;
            }
            int want = 0;
            for (int i = 0; i < recs.Count; i++)
            {
                var rec = recs[i];
                var s = Make(rec.Id, rec.Name);
                s.Pipes = rec.Pipes ?? "";
                var stored = NvkNod.ReadKey(db, "S_" + NvkNod.SegKeyPublic(rec.Id));
                if (stored != null && stored.Count > 0) s.State = stored;
                else if (string.Equals(rec.Id, active, StringComparison.OrdinalIgnoreCase))
                    s.State = Clone(map);
                All.Add(s);
                if (!string.IsNullOrEmpty(active)
                    && string.Equals(rec.Id, active, StringComparison.OrdinalIgnoreCase))
                    want = i;
            }
            Index = want;
            var cur = Current;
            if (db != null && cur != null && !string.IsNullOrEmpty(cur.Pipes))
                NvkSession.LastPipes = NvkNod.PipesFromHandles(db, cur.Pipes);
            else if (cur != null && string.IsNullOrEmpty(cur.Pipes))
                NvkSession.LastPipes = new List<ObjectId>();
        }

        public static void Capture()
        {
            EnsureOne();
            var s = Current;
            var map = NvkUiState.Serialize();
            NvkNod.CaptureLayout(map);
            PutInto(map);
            s.State = map;
            s.Pipes = NvkNod.HandlesOf(NvkSession.LastPipes);
            map["PIPES"] = s.Pipes ?? "";
            map["SEG"] = s.Id ?? "";
            s.Runs = CopyRuns(NvkSession.AllRuns());
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null) NvkNod.WriteSite(doc.Database, s.Id, map);
        }

        public static NvkSite ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s != null && string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }

        /// <summary>Домены всех участков по порядку списка. Не подменяет Current.Runs.</summary>
        public static List<DomainResult> JoinedRuns()
        {
            EnsureOne();
            var r = new List<DomainResult>();
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || s.Runs == null) continue;
                for (int j = 0; j < s.Runs.Count; j++)
                    if (s.Runs[j] != null) r.Add(s.Runs[j]);
            }
            return r;
        }

        /// <summary>Сколько сечений у участков перед текущим (сквозная нумерация).</summary>
        public static int GeomOffset()
        {
            if (!NvkUiState.VolJoinSites) return 0;
            int n = 0;
            int cur = Index;
            for (int i = 0; i < cur && i < All.Count; i++)
                n += GeomCount(All[i]);
            return n;
        }

        /// <summary>Сколько точек отбора у участков перед текущим (сквозные Тn).</summary>
        public static int SampleOffset()
        {
            if (!NvkUiState.VolJoinSites) return 0;
            int n = 0;
            int cur = Index;
            for (int i = 0; i < cur && i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || s.Runs == null) continue;
                foreach (var run in s.Runs)
                {
                    if (run == null || run.Chain == null) continue;
                    var cfg = run.Cfg ?? new SectionConfig();
                    if (!cfg.PlanEn) continue;
                    n += PlanPoints.Samples(run.Chain, cfg, run.PlanTargets, run.PlanLatOffs, 1).Count;
                }
            }
            return n;
        }

        static int GeomCount(NvkSite s)
        {
            int n = 0;
            if (s == null || s.Runs == null) return 0;
            foreach (var r in s.Runs)
                if (r != null && r.Geoms != null) n += r.Geoms.Count;
            return n;
        }

        public static void Apply(NvkSite s, Database db)
        {
            if (s == null) return;
            bool join = NvkUiState.VolJoinSites;
            if (s.State != null && s.State.Count > 0)
            {
                NvkUiState.FromMap(s.State);
                NvkUiState.VolJoinSites = join;
                if (db != null) NvkNod.ApplyLayout(db, s.State);
            }
            if (db != null && !string.IsNullOrEmpty(s.Pipes))
                NvkSession.LastPipes = NvkNod.PipesFromHandles(db, s.Pipes);
            else if (string.IsNullOrEmpty(s.Pipes))
                NvkSession.LastPipes = new List<ObjectId>();
            if (s.Runs != null && s.Runs.Count > 0)
                NvkSession.SetRuns(s.Runs);
            else
            {
                NvkSession.Last = null;
                NvkSession.Runs = new List<DomainResult>();
            }
        }

        public static void RememberPipes()
        {
            EnsureOne();
            Current.Pipes = NvkNod.HandlesOf(NvkSession.LastPipes);
        }

        public static void AttachRuns(List<DomainResult> runs)
        {
            EnsureOne();
            Current.Runs = CopyRuns(runs);
            Current.Pipes = NvkNod.HandlesOf(NvkSession.LastPipes);
            ApplyNameFromRuns(Current);
        }

        public static void RefreshNameFromPipes(Database db)
        {
            EnsureOne();
            if (db == null) return;
            var pipes = NvkSession.LastPipes;
            if (pipes == null || pipes.Count == 0) return;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var segs = new List<PipeSeg>();
                    foreach (var id in pipes)
                    {
                        if (id.IsNull || !id.IsValid || id.IsErased) continue;
                        var pipe = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Pipe;
                        if (pipe != null) segs.Add(PipeReader.Read(tr, pipe));
                    }
                    tr.Commit();
                    string name = SiteTitle.FromChains(Chain.Components(segs));
                    if (!string.IsNullOrEmpty(name))
                        Current.Name = name;
                }
            }
            catch (Exc) { }
        }

        static void ApplyNameFromRuns(NvkSite s)
        {
            if (s == null || s.Runs == null) return;
            var chains = new List<Chain>();
            for (int i = 0; i < s.Runs.Count; i++)
            {
                var r = s.Runs[i];
                if (r != null && r.Chain != null) chains.Add(r.Chain);
            }
            string name = SiteTitle.FromChains(chains);
            if (!string.IsNullOrEmpty(name))
                s.Name = name;
        }

        public static void Select(int i)
        {
            EnsureOne();
            if (i < 0 || i >= All.Count || i == Index) return;
            NvkPalette.Flush();
            Capture();
            Index = i;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            Apply(Current, doc != null ? doc.Database : null);
            NvkPipeline.SaveNod();
            NvkPalette.Reload();
        }

        public static void RememberInsertTemplate()
        {
            var t = FromSession();
            if (t != null && t.Any)
                InsertTemplate = t;
        }

        public static void ClearInsertTemplate()
        {
            InsertTemplate = null;
        }

        public static SiteLayout FromSession()
        {
            var t = new SiteLayout();
            if (NvkSession.CsInsHas) t.SetCs(NvkSession.CsIns.X, NvkSession.CsIns.Y);
            if (NvkSession.EpInsHas) t.SetEp(NvkSession.EpIns.X, NvkSession.EpIns.Y);
            if (NvkSession.VolInsHas) t.SetVol(NvkSession.VolIns.X, NvkSession.VolIns.Y);
            for (int i = 0; i < 5; i++)
            {
                if (NvkSession.CsHas != null && NvkSession.CsHas[i])
                    t.SetCsLvl(i, NvkSession.CsLvl[i].X, NvkSession.CsLvl[i].Y);
                if (NvkSession.EpHas != null && NvkSession.EpHas[i])
                    t.SetEpLvl(i, NvkSession.EpLvl[i].X, NvkSession.EpLvl[i].Y);
            }
            t.Cols = NvkUiState.CsCols;
            t.ResolveOrigin();
            return t;
        }

        public static void ToSession(SiteLayout t)
        {
            if (t == null) return;
            if (t.HasCs)
            {
                NvkSession.CsIns = new Autodesk.AutoCAD.Geometry.Point3d(t.CsX, t.CsY, 0.0);
                NvkSession.CsInsHas = true;
            }
            if (t.HasEp)
            {
                NvkSession.EpIns = new Autodesk.AutoCAD.Geometry.Point3d(t.EpX, t.EpY, 0.0);
                NvkSession.EpInsHas = true;
            }
            if (t.HasVol)
            {
                NvkSession.VolIns = new Autodesk.AutoCAD.Geometry.Point3d(t.VolX, t.VolY, 0.0);
                NvkSession.VolInsHas = true;
            }
            for (int i = 0; i < 5; i++)
            {
                if (t.CsL[i])
                {
                    NvkSession.CsLvl[i] = new Autodesk.AutoCAD.Geometry.Point3d(t.CsLx[i], t.CsLy[i], 0.0);
                    NvkSession.CsHas[i] = true;
                }
                if (t.EpL[i])
                {
                    NvkSession.EpLvl[i] = new Autodesk.AutoCAD.Geometry.Point3d(t.EpLx[i], t.EpLy[i], 0.0);
                    NvkSession.EpHas[i] = true;
                }
            }
            if (t.Cols > 0)
            {
                NvkUiState.CsCols = t.Cols;
                NvkSession.CsCols = t.Cols;
            }
        }

        /// <summary>Вставка по точке: раскладка как у шаблона (участок 43-44-46 и т.п.).</summary>
        public static bool PlaceInsertsAt(double x, double y)
        {
            var src = InsertTemplate;
            if (src == null || !src.Any) src = new SiteLayout();
            var at = src.At(x, y);
            ToSession(at);
            return at.Any;
        }

        public static void AddFromCommand()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            NvkPalette.Flush();
            Capture();
            RememberInsertTemplate();
            var recs = Recs();
            var n = Make(SitePack.NextId(recs), SitePack.NextName(recs));
            n.State = FreshClone(Current.State);
            n.State["SEG"] = n.Id;
            All.Add(n);
            Index = All.Count - 1;
            Apply(n, doc.Database);
            NvkPalette.Reload();
            ed.WriteMessage("\n[НВК] участок «" + n.Name + "». Выбери трубы.");
            NvkPipeline.StageInput(true);
            RememberPipes();
            NvkPipeline.SaveNod();
            NvkPalette.Reload();
        }

        public static void RemoveFromCommand()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            NvkPalette.Flush();
            EnsureOne();
            if (All.Count <= 1)
            {
                doc.Editor.WriteMessage("\n[НВК] нужен хотя бы один участок.");
                NvkPalette.SetStatus("Нужен хотя бы один участок");
                return;
            }
            var gone = Current;
            string id = gone.Id;
            string name = gone.Name;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    NvkClean.EraseSeg(doc.Database, tr, id);
                    tr.Commit();
                }
            }
            catch (Exc ex)
            {
                doc.Editor.WriteMessage("\n[НВК] очистка участка: " + ex.Message);
            }
            NvkNod.DeleteKey(doc.Database, "S_" + NvkNod.SegKeyPublic(id));
            All.RemoveAt(Index);
            if (Index >= All.Count) Index = All.Count - 1;
            Apply(Current, doc.Database);
            NvkPipeline.SaveNod();
            NvkPalette.Reload();
            doc.Editor.WriteMessage("\n[НВК] удалён «" + name + "»");
            NvkPalette.SetStatus("Удалён " + name);
        }

        static Dictionary<string, string> FreshClone(Dictionary<string, string> src)
        {
            var d = Clone(src);
            if (d == null) d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["PIPES"] = "";
            d["SEG"] = "";
            d["ZONES"] = "";
            d["SCATTER"] = "";
            d["SCATTER2"] = "";
            d["SCATTER3"] = "";
            d["SCATTER4"] = "";
            d["SCATTER5"] = "";
            d["LATOFF"] = "";
            d["PLAN_TGT"] = "";
            d["PLAN_LAT"] = "";
            d["PLAN_LDR"] = "";
            d["CS_X"] = "";
            d["CS_Y"] = "";
            d["EP_X"] = "";
            d["EP_Y"] = "";
            d["VOL_X"] = "";
            d["VOL_Y"] = "";
            d["EP_LVLS"] = "";
            d["CS_LVLS"] = "";
            return d;
        }

        static Dictionary<string, string> Clone(Dictionary<string, string> src)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (src == null) return d;
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }

        static List<DomainResult> CopyRuns(List<DomainResult> src)
        {
            var r = new List<DomainResult>();
            if (src == null) return r;
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null) r.Add(src[i]);
            return r;
        }

        static string Get(Dictionary<string, string> map, string key)
        {
            if (map == null) return "";
            string v;
            return map.TryGetValue(key, out v) ? v : "";
        }
    }
}
