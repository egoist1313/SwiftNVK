using System;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Подстановка %%NET%% %%SEG%% %%TOL%% %%TYPE%% в листах и модели. Рамку не рисуем.</summary>
    static class NvkCodes
    {
        public static string Apply(Database db, Transaction tr, string net, string seg)
        {
            if (db == null || tr == null) return "[НВК] коды: нет чертежа";
            string tol = SheetCodes.TolText(net);
            string type = SheetCodes.TypeName(net);
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
                + "  заменено " + n.ToString();
            return msg;
        }

        static int WalkSpace(Transaction tr, ObjectId btrId, string net, string seg, string tol, string type)
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

        static int WalkAttribs(Transaction tr, BlockReference br, string net, string seg, string tol, string type)
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
            if (obj == null || !SheetCodes.HasCode(old)) return 0;
            string neu = SheetCodes.Subst(old, net, seg, tol, type);
            if (string.Equals(neu, old, StringComparison.Ordinal)) return 0;
            try
            {
                if (!obj.IsWriteEnabled) obj.UpgradeOpen();
                set(neu);
                return 1;
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] коды: " + ex.Message);
                return 0;
            }
        }
    }
}
