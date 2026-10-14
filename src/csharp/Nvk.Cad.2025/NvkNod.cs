using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Чтение/запись словаря NVK / STATE из NOD текущего чертежа.</summary>
    static class NvkNod
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Dictionary<string, string> Read(Database db)
        {
            var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (db == null) return empty;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (nod == null || !nod.Contains("NVK"))
                    {
                        tr.Commit();
                        return empty;
                    }
                    var nvk = tr.GetObject(nod.GetAt("NVK"), OpenMode.ForRead) as DBDictionary;
                    if (nvk == null || !nvk.Contains("STATE"))
                    {
                        tr.Commit();
                        return empty;
                    }
                    var rec = tr.GetObject(nvk.GetAt("STATE"), OpenMode.ForRead) as Xrecord;
                    if (rec == null || rec.Data == null)
                    {
                        tr.Commit();
                        return empty;
                    }
                    var lines = new List<string>();
                    foreach (TypedValue tv in rec.Data)
                    {
                        if (tv.TypeCode == 1 && tv.Value is string s && s.Length > 0)
                            lines.Add(s);
                    }
                    tr.Commit();
                    return NodPairs.Parse(lines);
                }
            }
            catch
            {
                return empty;
            }
        }

        /// <summary>Полный STATE (и S_{seg}, если есть). Создаёт словарь NVK при необходимости.</summary>
        public static void Write(Database db, IDictionary<string, string> map, string segId)
        {
            if (db == null || map == null) return;
            WriteKey(db, "STATE", map);
            if (!string.IsNullOrEmpty(segId))
                WriteKey(db, "S_" + SegKey(segId), map);
        }

        public static void WriteCurrent(Database db)
        {
            if (db == null) return;
            var map = NvkUiState.Serialize();
            AddLayout(map, db);
            string seg = NvkSession.Last != null ? NvkSession.Last.SegId : null;
            Write(db, map, seg);
        }

        /// <summary>
        /// Обновить один ключ в STATE XRECORD чертежа (без затирания остальных).
        /// Если STATE нет — создаёт полный STATE.
        /// </summary>
        public static void PatchEntry(Database db, string key, string value)
        {
            if (db == null || string.IsNullOrEmpty(key)) return;
            try
            {
                var map = Read(db);
                if (map.Count == 0)
                {
                    WriteCurrent(db);
                    map = Read(db);
                }
                map[key] = value ?? "";
                string seg = NvkSession.Last != null ? NvkSession.Last.SegId : null;
                Write(db, map, seg);
            }
            catch { }
        }

        public static void ApplyLayout(Database db, Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0) return;
            TryPt(map, "CS_X", "CS_Y", ref NvkSession.CsIns, ref NvkSession.CsInsHas);
            TryPt(map, "EP_X", "EP_Y", ref NvkSession.EpIns, ref NvkSession.EpInsHas);
            TryPt(map, "VOL_X", "VOL_Y", ref NvkSession.VolIns, ref NvkSession.VolInsHas);
            ParseLvls(Get(map, "EP_LVLS"), NvkSession.EpLvl, NvkSession.EpHas);
            ParseLvls(Get(map, "CS_LVLS"), NvkSession.CsLvl, NvkSession.CsHas);
            ApplyNums(Get(map, "SCATTER"), ref NvkSession.CacheScatter);
            ApplyNums(Get(map, "SCATTER2"), ref NvkSession.CacheScatter2);
            ApplyNums(Get(map, "SCATTER3"), ref NvkSession.CacheScatter3);
            ApplyNums(Get(map, "SCATTER4"), ref NvkSession.CacheScatter4);
            ApplyNums(Get(map, "SCATTER5"), ref NvkSession.CacheScatter5);
            ApplyNums(Get(map, "LATOFF"), ref NvkSession.CacheLatOff);
            ApplyNums(Get(map, "PLAN_TGT"), ref NvkSession.CachePlanTgt);
            ApplyNums(Get(map, "PLAN_LAT"), ref NvkSession.CachePlanLat);
            string ldr = Get(map, "PLAN_LDR");
            if (!string.IsNullOrEmpty(ldr)) NvkSession.CachePlanLdr = ldr;
            string pipes = Get(map, "PIPES");
            if (!string.IsNullOrEmpty(pipes) && db != null)
            {
                var ids = PipesFromHandles(db, pipes);
                if (ids.Count > 0)
                    NvkSession.LastPipes = ids;
            }
        }

        public static List<ObjectId> PipesFromHandles(Database db, string pipes)
        {
            var ids = new List<ObjectId>();
            if (db == null || string.IsNullOrWhiteSpace(pipes)) return ids;
            foreach (var tok in pipes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string h = tok.Trim();
                if (h.Length == 0) continue;
                try
                {
                    long n = Convert.ToInt64(h, 16);
                    ObjectId id;
                    if (db.TryGetObjectId(new Handle(n), out id) && !id.IsNull && id.IsValid)
                        ids.Add(id);
                }
                catch (Exc) { }
            }
            return ids;
        }

        public static string HandlesOf(IList<ObjectId> ids)
        {
            if (ids == null || ids.Count == 0) return "";
            var hs = new List<string>(ids.Count);
            foreach (var id in ids)
            {
                if (id.IsNull) continue;
                try { hs.Add(id.Handle.ToString().ToUpperInvariant()); }
                catch (Exc) { }
            }
            hs.Sort(StringComparer.Ordinal);
            return string.Join(";", hs.ToArray());
        }

        static void AddLayout(Dictionary<string, string> map, Database db)
        {
            if (NvkSession.CsInsHas)
            {
                map["CS_X"] = F(NvkSession.CsIns.X);
                map["CS_Y"] = F(NvkSession.CsIns.Y);
            }
            if (NvkSession.EpInsHas)
            {
                map["EP_X"] = F(NvkSession.EpIns.X);
                map["EP_Y"] = F(NvkSession.EpIns.Y);
            }
            if (NvkSession.VolInsHas)
            {
                map["VOL_X"] = F(NvkSession.VolIns.X);
                map["VOL_Y"] = F(NvkSession.VolIns.Y);
            }
            map["EP_LVLS"] = FmtLvls(NvkSession.EpLvl, NvkSession.EpHas);
            map["CS_LVLS"] = FmtLvls(NvkSession.CsLvl, NvkSession.CsHas);
            map["SCATTER"] = JoinNums(NvkSession.CacheScatter);
            map["SCATTER2"] = JoinNums(NvkSession.CacheScatter2);
            map["SCATTER3"] = JoinNums(NvkSession.CacheScatter3);
            map["SCATTER4"] = JoinNums(NvkSession.CacheScatter4);
            map["SCATTER5"] = JoinNums(NvkSession.CacheScatter5);
            map["LATOFF"] = JoinNums(NvkSession.CacheLatOff);
            map["PLAN_TGT"] = JoinNums(NvkSession.CachePlanTgt);
            map["PLAN_LAT"] = JoinNums(NvkSession.CachePlanLat);
            map["PLAN_LDR"] = NvkSession.CachePlanLdr ?? "";
            map["PIPES"] = HandlesOf(NvkSession.LastPipes);
            map["SEG"] = NvkSession.Last != null ? (NvkSession.Last.SegId ?? "") : "";
            if (db != null && string.IsNullOrEmpty(map["PIPES"]) && NvkSession.LastPipes != null)
                map["PIPES"] = HandlesOf(NvkSession.LastPipes);
        }

        static void WriteKey(Database db, string key, IDictionary<string, string> map)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                    DBDictionary nvk;
                    if (nod.Contains("NVK"))
                        nvk = (DBDictionary)tr.GetObject(nod.GetAt("NVK"), OpenMode.ForWrite);
                    else
                    {
                        nvk = new DBDictionary();
                        nod.SetAt("NVK", nvk);
                        tr.AddNewlyCreatedDBObject(nvk, true);
                    }

                    var rec = new Xrecord();
                    var rb = new ResultBuffer();
                    foreach (var kv in map)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        foreach (var chunk in BuildChunks(kv.Key, kv.Value ?? ""))
                            rb.Add(new TypedValue(1, chunk));
                    }
                    rec.Data = rb;

                    if (nvk.Contains(key))
                    {
                        try
                        {
                            var old = tr.GetObject(nvk.GetAt(key), OpenMode.ForWrite);
                            old.Erase();
                        }
                        catch (Exc) { }
                    }
                    nvk.SetAt(key, rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                    tr.Commit();
                }
            }
            catch (Exc) { }
        }

        static string SegKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "X";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.Length == 0 ? "X" : sb.ToString();
        }

        static IEnumerable<string> BuildChunks(string key, string value)
        {
            const int Lim = 240;
            if (key.Length + 1 + value.Length <= Lim)
            {
                yield return key + "=" + value;
                yield break;
            }
            int n = 1;
            while (value.Length > 0)
            {
                string prefix = n == 1 ? key : key + "#" + n;
                int take = Math.Max(1, Lim - prefix.Length - 1);
                string chunk = value.Substring(0, Math.Min(take, value.Length));
                value = value.Length > chunk.Length ? value.Substring(chunk.Length) : "";
                yield return prefix + "=" + chunk;
                n++;
            }
        }

        static string Get(Dictionary<string, string> map, string key)
        {
            string v;
            return map.TryGetValue(key, out v) ? v : "";
        }

        static void TryPt(Dictionary<string, string> map, string kx, string ky, ref Point3d pt, ref bool has)
        {
            string sx = Get(map, kx), sy = Get(map, ky);
            double x, y;
            if (TryD(sx, out x) && TryD(sy, out y))
            {
                pt = new Point3d(x, y, 0.0);
                has = true;
            }
        }

        static void ParseLvls(string s, Point3d[] pts, bool[] has)
        {
            if (string.IsNullOrEmpty(s) || pts == null || has == null) return;
            var parts = s.Split(';');
            for (int i = 0; i < 5 && i < parts.Length; i++)
            {
                string tok = parts[i];
                int c = tok.IndexOf(',');
                if (c <= 0) { has[i] = false; continue; }
                double x, y;
                if (TryD(tok.Substring(0, c), out x) && TryD(tok.Substring(c + 1), out y))
                {
                    pts[i] = new Point3d(x, y, 0.0);
                    has[i] = true;
                }
                else has[i] = false;
            }
        }

        static string FmtLvls(Point3d[] pts, bool[] has)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) sb.Append(';');
                if (has != null && has[i])
                    sb.Append(F(pts[i].X)).Append(',').Append(F(pts[i].Y));
            }
            return sb.ToString();
        }

        static void ApplyNums(string s, ref List<double> dest)
        {
            var t = SplitNums(s);
            if (t.Count >= 1) dest = t;
        }

        static List<double> SplitNums(string s)
        {
            var r = new List<double>();
            if (string.IsNullOrWhiteSpace(s)) return r;
            foreach (var tok in s.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double x;
                if (TryD(tok, out x)) r.Add(x);
            }
            return r;
        }

        static string JoinNums(List<double> a)
        {
            if (a == null || a.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < a.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(a[i].ToString("0.######", Inv));
            }
            return sb.ToString();
        }

        static string F(double x)
        {
            return x.ToString("0.####", Inv);
        }

        static bool TryD(string s, out double x)
        {
            x = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (double.TryParse(s, NumberStyles.Float, Inv, out x)) return true;
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out x);
        }
    }
}
