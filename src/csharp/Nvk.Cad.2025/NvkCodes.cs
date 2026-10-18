using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>
    /// Коды штампа: шаблон %%SEG%% и остальные в XData TPL=,
    /// на листе подстановка значений. CustomDP пишем для ручных полей.
    /// SetField с живым Field стирал текст — не ставим.
    /// Рамку не рисуем.
    /// </summary>
    static class NvkCodes
    {
        public static string Apply(Database db, Transaction tr,
            string net, string seg, string tol, string type)
        {
            if (db == null || tr == null) return "[НВК] коды: нет чертежа";
            PutProps(db, net, seg, tol, type);
            int n = 0, nLay = 0;
            var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict)
            {
                if (string.IsNullOrEmpty(e.Key)) continue;
                Layout layout;
                try { layout = tr.GetObject(e.Value, OpenMode.ForRead) as Layout; }
                catch (Exc) { continue; }
                if (layout == null) continue;
                nLay++;
                n += WalkSpace(tr, layout.BlockTableRecordId, net, seg, tol, type);
            }
            string msg = "[НВК] коды net=\"" + (net ?? "") + "\" seg=\"" + (seg ?? "")
                + "\"  листов " + nLay.ToString()
                + "  подстановок " + n.ToString();
            return msg;
        }

        static void PutProps(Database db, string net, string seg, string tol, string type)
        {
            DatabaseSummaryInfoBuilder b;
            try { b = new DatabaseSummaryInfoBuilder(db.SummaryInfo); }
            catch (Exc) { b = new DatabaseSummaryInfoBuilder(); }
            var table = b.CustomPropertyTable;
            PutProp(table, SheetCodes.PropNet, net);
            PutProp(table, SheetCodes.PropSeg, seg);
            PutProp(table, SheetCodes.PropTol, tol);
            PutProp(table, SheetCodes.PropType, type);
            db.SummaryInfo = b.ToDatabaseSummaryInfo();
        }

        static void PutProp(System.Collections.IDictionary table, string key, string val)
        {
            if (table == null || string.IsNullOrEmpty(key)) return;
            if (table.Contains(key)) table.Remove(key);
            table.Add(key, val ?? "");
        }

        static int WalkSpace(Transaction tr, ObjectId btrId,
            string net, string seg, string tol, string type)
        {
            if (btrId.IsNull) return 0;
            BlockTableRecord btr;
            try { btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead); }
            catch (Exc) { return 0; }
            int n = 0;
            foreach (ObjectId id in btr)
            {
                if (id.IsNull) continue;
                Entity ent;
                try { ent = tr.GetObject(id, OpenMode.ForRead) as Entity; }
                catch (Exc) { continue; }
                if (ent == null || ent.IsErased) continue;
                var mt = ent as MText;
                if (mt != null)
                {
                    n += Put(tr, mt, mt.Contents, v => mt.Contents = v, net, seg, tol, type);
                    continue;
                }
                var tx = ent as DBText;
                if (tx != null)
                {
                    n += Put(tr, tx, tx.TextString, v => tx.TextString = v, net, seg, tol, type);
                    continue;
                }
                var br = ent as BlockReference;
                if (br != null)
                    n += WalkAttribs(tr, br, net, seg, tol, type);
            }
            return n;
        }

        static int WalkAttribs(Transaction tr, BlockReference br,
            string net, string seg, string tol, string type)
        {
            int n = 0;
            AttributeCollection col;
            try { col = br.AttributeCollection; }
            catch (Exc) { return 0; }
            if (col == null) return 0;
            foreach (ObjectId id in col)
            {
                if (id.IsNull) continue;
                AttributeReference att;
                try { att = tr.GetObject(id, OpenMode.ForRead) as AttributeReference; }
                catch (Exc) { continue; }
                if (att == null || att.IsErased) continue;
                n += Put(tr, att, att.TextString, v => att.TextString = v, net, seg, tol, type);
            }
            return n;
        }

        static int Put(Transaction tr, DBObject obj, string old, Action<string> set,
            string net, string seg, string tol, string type)
        {
            if (obj == null) return 0;
            string tpl = NvkTag.Get1000(obj, NvkTag.TplKey);
            if (SheetCodes.HasCode(old))
                tpl = old;
            else if (string.IsNullOrEmpty(tpl) && SheetCodes.HasField(old))
                tpl = SheetCodes.FromFieldText(old);
            if (string.IsNullOrEmpty(tpl) || !SheetCodes.HasCode(tpl))
                return 0;
            try { NvkTag.Put1000(tr, obj, NvkTag.TplKey, tpl); }
            catch (Exc ex) { NvkLog.Write("[НВК] коды TPL: " + ex.Message); }

            string neu = SheetCodes.Subst(tpl, net, seg, tol, type);
            try
            {
                if (!obj.IsWriteEnabled) obj.UpgradeOpen();
                ClearField(tr, obj);
                set(neu);
                return 1;
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] коды: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Снять пустое поле 0.210, иначе Contents не видно.</summary>
        static void ClearField(Transaction tr, DBObject obj)
        {
            if (obj == null) return;
            try
            {
                if (!obj.HasFields) return;
                ObjectId fid = ObjectId.Null;
                try { fid = obj.GetField(); }
                catch (Exc) { }
                obj.RemoveField();
                if (fid.IsNull || tr == null) return;
                try
                {
                    var f = tr.GetObject(fid, OpenMode.ForWrite, false) as Field;
                    if (f != null && !f.IsErased) f.Erase();
                }
                catch (Exc) { }
            }
            catch (Exc) { }
        }

        public static void Collect(out string net, out string seg, out string tol, out string type)
        {
            var names = new List<string>();
            var nets = new List<string>();
            var sites = NvkSites.All;
            if (sites != null)
            {
                for (int i = 0; i < sites.Count; i++)
                {
                    var s = sites[i];
                    if (s == null) continue;
                    if (!string.IsNullOrEmpty(s.Name))
                        names.Add(SiteTitle.CleanTitle(s.Name, NvkUiState.MhStripParen));
                    if (s.Runs == null) continue;
                    for (int j = 0; j < s.Runs.Count; j++)
                    {
                        var r = s.Runs[j];
                        if (r != null && !string.IsNullOrEmpty(r.NetName))
                            nets.Add(r.NetName);
                    }
                }
            }
            if (names.Count == 0 && NvkSites.Current != null
                && !string.IsNullOrEmpty(NvkSites.Current.Name))
                names.Add(SiteTitle.CleanTitle(NvkSites.Current.Name, NvkUiState.MhStripParen));
            if (nets.Count == 0)
            {
                var runs = NvkSession.AllRuns();
                if (runs != null)
                {
                    for (int i = 0; i < runs.Count; i++)
                    {
                        if (runs[i] != null && !string.IsNullOrEmpty(runs[i].NetName))
                            nets.Add(runs[i].NetName);
                    }
                }
            }
            if (nets.Count == 0 && NvkSession.Last != null
                && !string.IsNullOrEmpty(NvkSession.Last.NetName))
                nets.Add(NvkSession.Last.NetName);
            net = SheetCodes.JoinParts(nets);
            seg = SheetCodes.JoinParts(names);
            type = SheetCodes.TypeNameMany(nets);
            tol = SheetCodes.TolTextMany(nets);
        }
    }
}
