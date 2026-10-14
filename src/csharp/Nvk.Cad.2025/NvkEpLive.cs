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
    /// <summary>
    /// Ручки вершин продольника: STRETCH пишет scatter и перерисовывает лист (таблица/отметки).
    /// </summary>
    static class NvkEpLive
    {
        static bool _busy;
        static bool _idleHooked;
        static bool _dyingHooked;
        static readonly HashSet<ObjectId> _dirty = new HashSet<ObjectId>();
        static readonly HashSet<Document> _hooked = new HashSet<Document>();

        public static bool Busy => _busy;

        public static void Mark(ObjectId id)
        {
            if (_busy || id.IsNull) return;
            _dirty.Add(id);
        }

        public static void Attach(Document doc)
        {
            if (doc == null) return;
            if (!_hooked.Add(doc)) return;
            doc.CommandEnded += OnCmd;
            doc.Database.ObjectModified += OnMod;
            if (!_dyingHooked)
            {
                try
                {
                    AcadApp.DocumentManager.DocumentToBeDestroyed += OnDocDying;
                    _dyingHooked = true;
                }
                catch (Exc) { }
            }
        }

        public static void DetachAll()
        {
            if (_dyingHooked)
            {
                try { AcadApp.DocumentManager.DocumentToBeDestroyed -= OnDocDying; } catch (Exc) { }
                _dyingHooked = false;
            }
            foreach (var doc in _hooked)
            {
                try
                {
                    doc.CommandEnded -= OnCmd;
                    doc.Database.ObjectModified -= OnMod;
                }
                catch (Exc) { }
            }
            _hooked.Clear();
        }

        static void OnDocDying(object sender, DocumentCollectionEventArgs e)
        {
            var doc = e != null ? e.Document : null;
            if (doc == null) return;
            try
            {
                doc.CommandEnded -= OnCmd;
                doc.Database.ObjectModified -= OnMod;
            }
            catch (Exc) { }
            _hooked.Remove(doc);
        }

        static void OnMod(object sender, ObjectEventArgs e)
        {
            if (_busy || e == null || e.DBObject == null) return;
            var pl = e.DBObject as Polyline;
            if (pl == null || pl.Closed) return;
            EpLive live;
            if (!NvkTag.TryReadLive(pl, out live) || live == null) return;
            _dirty.Add(pl.ObjectId);
        }

        static void OnCmd(object sender, CommandEventArgs e)
        {
            if (_busy || _dirty.Count == 0) return;
            string c = (e != null ? e.GlobalCommandName : null) ?? "";
            c = c.ToUpperInvariant();
            if (c.IndexOf("STRETCH", StringComparison.Ordinal) < 0
                && c.IndexOf("GRIP", StringComparison.Ordinal) < 0)
            {
                _dirty.Clear();
                return;
            }
            if (_idleHooked) return;
            _idleHooked = true;
            AcadApp.Idle += OnIdle;
        }

        static void OnIdle(object sender, EventArgs e)
        {
            AcadApp.Idle -= OnIdle;
            _idleHooked = false;
            if (_busy || _dirty.Count == 0) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { _dirty.Clear(); return; }
            try
            {
                Apply(doc);
            }
            catch (Exc ex)
            {
                try { NvkLog.Write("[НВК] EP live: " + ex.Message); } catch (Exc) { }
            }
        }

        static void Apply(Document doc)
        {
            var ids = new List<ObjectId>(_dirty);
            _dirty.Clear();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (id.IsNull || id.IsErased) continue;
                    var pl = tr.GetObject(id, OpenMode.ForWrite) as Polyline;
                    if (pl == null) continue;
                    EpLive live;
                    if (!NvkTag.TryReadLive(pl, out live) || live == null) continue;
                    if (SkipSrc(live.Src)) continue;
                    SnapX(pl, live);
                    NvkTag.StampLive(tr, pl, live);
                    keys.Add(Key(live));
                }
                if (keys.Count == 0)
                {
                    tr.Commit();
                    return;
                }
                bool any = false;
                foreach (var key in keys)
                {
                    if (BakeKey(doc.Database, tr, key))
                        any = true;
                }
                tr.Commit();
                if (!any) return;
            }
            _busy = true;
            try
            {
                NvkPipeline.StageEp();
                NvkPipeline.SaveNod();
                NvkLog.Write("[НВК] EP live: вершины → отметки, листов " + keys.Count.ToString());
            }
            finally
            {
                _busy = false;
                _dirty.Clear();
            }
        }

        static bool SkipSrc(HeightSrc src)
        {
            return src == HeightSrc.RoadTop
                || src == HeightSrc.BlackEarth
                || src == HeightSrc.DeltaZash;
        }

        static string Key(EpLive live)
        {
            return (live.Seg ?? "") + "|" + ((int)live.Kind) + ":" + ((int)live.Src);
        }

        static void SnapX(Polyline pl, EpLive live)
        {
            if (pl == null || live == null || live.Ch == null || live.LSc < 1e-12) return;
            int n = Math.Min(pl.NumberOfVertices, live.Ch.Length);
            for (int i = 0; i < n; i++)
            {
                var p = pl.GetPoint2dAt(i);
                double x = live.Ox + live.Ch[i] * live.LSc;
                if (Math.Abs(p.X - x) > 1e-9)
                    pl.SetPointAt(i, new Point2d(x, p.Y));
            }
        }

        static bool BakeKey(Database db, Transaction tr, string key)
        {
            EpLive sample = null;
            var pls = new List<Polyline>();
            Collect(db, tr, key, pls, ref sample);
            if (sample == null || pls.Count == 0) return false;
            if (SkipSrc(sample.Src)) return false;

            DomainResult run = FindRun(sample.Seg);
            if (run == null || run.Axis == null || run.Axis.Count < 2 || run.Chain == null)
                return false;
            var cfg = run.Cfg ?? new SectionConfig();
            double total = run.Chain.Total;
            if (total < 1e-9) return false;

            var series = Scatter.SeriesOf(sample.Src);
            var vals = run.Vals(series);
            int n = vals != null && vals.Count >= 2 ? vals.Count : Scatter.DenseCount(total);
            if (vals == null || vals.Count != n)
            {
                vals = new List<double>(n);
                for (int i = 0; i < n; i++) vals.Add(0.0);
            }

            double amp = RowScatter.Amp(sample.Src);
            var dz = new List<double>(n);
            double maxAbs = 0.0;
            for (int i = 0; i < n; i++)
            {
                double ch = n == 1 ? 0.0 : total * i / (n - 1);
                double x = sample.Ox + ch * sample.LSc;
                double y;
                if (!TryY(pls, x, out y))
                {
                    dz.Add(Scatter.Meters(ch, total, vals, amp > 1e-9 ? amp : 1.0));
                    continue;
                }
                double z = sample.HSc > 1e-12
                    ? sample.Osh + (y - sample.Oy) / sample.HSc
                    : sample.Osh;
                double zProj = ProfileRows.ZAtCh(run.Axis, sample.Src, cfg, ch);
                double d = z - zProj;
                dz.Add(d);
                double a = Math.Abs(d);
                if (a > maxAbs) maxAbs = a;
            }
            if (amp < 1e-9)
                amp = Math.Max(maxAbs, Scatter.DefaultAmp(series));
            if (amp < 1e-6) amp = 1e-6;
            RowScatter.Ensure(sample.Src, amp);
            var baked = Scatter.FromOffsets(dz, amp);
            PutVals(sample.Seg, series, baked);
            return true;
        }

        static void Collect(Database db, Transaction tr, string key, List<Polyline> pls, ref EpLive sample)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (btr.IsAnonymous) continue;
                string n = btr.Name;
                bool ms = string.Equals(n, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase);
                bool ep = n != null && n.StartsWith("NVK-EP-", StringComparison.OrdinalIgnoreCase);
                if (!ms && !ep) continue;
                foreach (ObjectId id in btr)
                {
                    var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (pl == null || pl.Closed) continue;
                    EpLive live;
                    if (!NvkTag.TryReadLive(pl, out live) || live == null) continue;
                    if (!string.Equals(Key(live), key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    pls.Add(pl);
                    sample = live;
                }
            }
        }

        static bool TryY(List<Polyline> pls, double x, out double y)
        {
            y = 0.0;
            foreach (var pl in pls)
            {
                int n = pl.NumberOfVertices;
                for (int i = 0; i < n - 1; i++)
                {
                    var a = pl.GetPoint2dAt(i);
                    var b = pl.GetPoint2dAt(i + 1);
                    double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
                    if (x < lo - 1e-6 || x > hi + 1e-6) continue;
                    double dx = b.X - a.X;
                    y = Math.Abs(dx) < 1e-12 ? a.Y : a.Y + (x - a.X) / dx * (b.Y - a.Y);
                    return true;
                }
            }
            return false;
        }

        static DomainResult FindRun(string seg)
        {
            foreach (var r in NvkSession.AllRuns())
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(seg) || string.Equals(r.SegId, seg, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return NvkSession.Last;
        }

        static void PutVals(string seg, ProfileKind series, List<double> vals)
        {
            switch (series)
            {
                case ProfileKind.Pesok:    NvkSession.CacheScatter2 = vals; break;
                case ProfileKind.Truba:    NvkSession.CacheScatter3 = vals; break;
                case ProfileKind.Zashita:  NvkSession.CacheScatter4 = vals; break;
                case ProfileKind.Zasypka:  NvkSession.CacheScatter5 = vals; break;
                default:                   NvkSession.CacheScatter  = vals; break;
            }
            foreach (var r in NvkSession.AllRuns())
            {
                if (r == null) continue;
                if (!string.IsNullOrEmpty(seg)
                    && !string.Equals(r.SegId, seg, StringComparison.OrdinalIgnoreCase))
                    continue;
                switch (series)
                {
                    case ProfileKind.Pesok:   r.ScatterL2 = vals; break;
                    case ProfileKind.Truba:   r.ScatterL3 = vals; break;
                    case ProfileKind.Zashita: r.ScatterL4 = vals; break;
                    case ProfileKind.Zasypka: r.ScatterL5 = vals; break;
                    default:                  r.ScatterVals = vals; break;
                }
            }
        }
    }
}
