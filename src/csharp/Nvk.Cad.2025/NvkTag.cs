using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    sealed class EpLive
    {
        public string Seg;
        public ProfileKind Kind;
        public HeightSrc Src;
        public double Ox, Oy, LSc, HSc, Osh, Y0;
        public double[] Ch;
    }

    /// <summary>Метка участка на объекте: XData NVK / 1000 SEG=id. Несколько участков в одном dwg.</summary>
    static class NvkTag
    {
        public const string App = "NVK";

        public static void StampVp(Transaction tr, Entity e, string key)
        {
            if (e == null || string.IsNullOrEmpty(key) || tr == null) return;
            try
            {
                EnsureApp(tr, e.Database);
                e.XData = new ResultBuffer(
                    new TypedValue(1001, App),
                    new TypedValue(1000, "VP=" + key));
            }
            catch (Exc) { }
        }

        public static string VpKey(Entity e)
        {
            if (e == null || e.XData == null) return null;
            try
            {
                string app = null;
                foreach (TypedValue v in e.XData)
                {
                    if (v.TypeCode == 1001) app = v.Value as string;
                    if (v.TypeCode == 1000 && app == App)
                    {
                        string s = v.Value as string;
                        if (s != null && s.StartsWith("VP="))
                            return s.Substring(3);
                    }
                }
            }
            catch (Exc) { }
            return null;
        }

        public static void Stamp(Transaction tr, Entity e)
        {
            string seg = null;
            if (NvkSession.Last != null)
                seg = NvkSession.Last.SegId;
            if (string.IsNullOrEmpty(seg))
                seg = NvkSites.CurrentId;
            Set(tr, e, seg);
        }

        public static void Set(Transaction tr, Entity e, string seg)
        {
            if (e == null || string.IsNullOrEmpty(seg) || tr == null) return;
            try
            {
                EnsureApp(tr, e.Database);
                e.XData = new ResultBuffer(
                    new TypedValue(1001, App),
                    new TypedValue(1000, "SEG=" + seg));
            }
            catch (Exc) { }
        }

        public static void StampLive(Transaction tr, Entity e, EpLive live)
        {
            if (e == null || live == null || tr == null) return;
            try
            {
                EnsureApp(tr, e.Database);
                var rb = new ResultBuffer();
                rb.Add(new TypedValue(1001, App));
                if (!string.IsNullOrEmpty(live.Seg))
                    rb.Add(new TypedValue(1000, "SEG=" + live.Seg));
                rb.Add(new TypedValue(1000, "EP=" + ((int)live.Kind) + ":" + ((int)live.Src)));
                rb.Add(new TypedValue(1040, live.Ox));
                rb.Add(new TypedValue(1040, live.Oy));
                rb.Add(new TypedValue(1040, live.LSc));
                rb.Add(new TypedValue(1040, live.HSc));
                rb.Add(new TypedValue(1040, live.Osh));
                rb.Add(new TypedValue(1040, live.Y0));
                var ch = live.Ch;
                int n = ch != null ? ch.Length : 0;
                rb.Add(new TypedValue(1070, n));
                for (int i = 0; i < n; i++)
                    rb.Add(new TypedValue(1040, ch[i]));
                e.XData = rb;
            }
            catch (Exc) { }
        }

        public static bool TryReadLive(Entity e, out EpLive live)
        {
            live = null;
            if (e == null || e.XData == null) return false;
            try
            {
                string app = null, ep = null, seg = null;
                var nums = new List<double>();
                int nCh = -1;
                foreach (TypedValue v in e.XData)
                {
                    if (v.TypeCode == 1001) app = v.Value as string;
                    if (app != App) continue;
                    if (v.TypeCode == 1000)
                    {
                        string s = v.Value as string;
                        if (s != null && s.StartsWith("SEG=")) seg = s.Substring(4);
                        else if (s != null && s.StartsWith("EP=")) ep = s.Substring(3);
                    }
                    else if (v.TypeCode == 1040 && v.Value is double d)
                        nums.Add(d);
                    else if (v.TypeCode == 1070 && v.Value is short sh)
                        nCh = sh;
                    else if (v.TypeCode == 1070 && v.Value is int ii)
                        nCh = ii;
                }
                if (string.IsNullOrEmpty(ep) || nums.Count < 6) return false;
                var parts = ep.Split(':');
                int k, srcN;
                if (parts.Length < 2 || !int.TryParse(parts[0], out k) || !int.TryParse(parts[1], out srcN))
                    return false;
                live = new EpLive
                {
                    Seg = seg,
                    Kind = (ProfileKind)k,
                    Src = (HeightSrc)srcN,
                    Ox = nums[0], Oy = nums[1],
                    LSc = nums[2], HSc = nums[3],
                    Osh = nums[4], Y0 = nums[5]
                };
                int start = 6;
                int take = nCh >= 0 ? nCh : nums.Count - start;
                if (take < 0) take = 0;
                if (start + take > nums.Count) take = nums.Count - start;
                live.Ch = new double[take];
                for (int i = 0; i < take; i++)
                    live.Ch[i] = nums[start + i];
                return true;
            }
            catch (Exc) { live = null; return false; }
        }

        static void EnsureApp(Transaction tr, Database db)
        {
            if (db == null || tr == null) return;
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(App)) return;
            rat.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = App };
            rat.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }

        public static string Get(Entity e)
        {
            if (e == null || e.XData == null) return null;
            try
            {
                string app = null;
                foreach (TypedValue v in e.XData)
                {
                    if (v.TypeCode == 1001) app = v.Value as string;
                    if (v.TypeCode == 1000 && app == App)
                    {
                        string s = v.Value as string;
                        if (s != null && s.StartsWith("SEG="))
                            return s.Substring(4);
                    }
                }
            }
            catch (Exc) { }
            return null;
        }

        public static bool Matches(Entity e, string seg)
        {
            if (string.IsNullOrEmpty(seg)) return true;
            string s = Get(e);
            if (string.IsNullOrEmpty(s)) return true;
            return string.Equals(s, seg, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchesBlock(string blockName, string seg)
        {
            if (string.IsNullOrEmpty(blockName)) return false;
            if (!blockName.StartsWith("NVK-", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(seg)) return true;
            return blockName.IndexOf("-" + seg + "-", System.StringComparison.OrdinalIgnoreCase) >= 0
                || blockName.EndsWith("-" + seg, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
