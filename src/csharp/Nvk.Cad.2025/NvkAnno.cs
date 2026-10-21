using System;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Снимок текущего DIMSTYLE / DIM* — то, что видит пользователь как «стандартный размер».</summary>
    sealed class DimSnap
    {
        public string Name = "";
        public ObjectId StyleId;
        public bool Anno;
        public bool AnnoSys;
        public int Lunit = 2;
        public int Tad = 1;
        public int Dec = 2;
        public double Txt = 2.5;
        public double Asz = 2.5;
        public double Scale = 1.0;
        public double Lfac = 1.0;
        public string Post = "";
        public int Tfill;
        public bool Tih;
        public bool Toh;
        public double Gap = 0.625;
        public double Exe = 1.25;
        public double Exo = 0.625;
        public string Cannoscale = "";
        public ObjectId TextStyleId;
    }

    /// <summary>Аннотативный флаг AutoCAD + текущий DIMSTYLE на размер.</summary>
    static class NvkAnno
    {
        static DimSnap _snap;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DimSnap LastSnap { get { return _snap; } }

        public static bool DimStyleAnnotative(Database db, Transaction tr)
        {
            var s = _snap ?? CaptureDim(db, tr);
            return s != null && (s.Anno || s.AnnoSys);
        }

        public static DimSnap CaptureDim(Database db, Transaction tr)
        {
            var s = new DimSnap();
            try { s.Cannoscale = StrVar("CANNOSCALE"); } catch (Exc) { }
            try { s.AnnoSys = NumVar("DIMANNO") != 0.0; } catch (Exc) { }
            if (db != null)
            {
                s.StyleId = db.Dimstyle;
                if (string.IsNullOrEmpty(s.Cannoscale) && db.Cannoscale != null)
                {
                    try { s.Cannoscale = db.Cannoscale.Name; } catch (Exc) { }
                }
            }
            DimStyleTableRecord rec = null;
            if (tr != null && db != null && !db.Dimstyle.IsNull)
            {
                try { rec = tr.GetObject(db.Dimstyle, OpenMode.ForRead) as DimStyleTableRecord; }
                catch (Exc) { }
            }
            if (rec != null)
            {
                try { s.Name = rec.Name ?? ""; } catch (Exc) { }
                try { s.Anno = rec.Annotative == AnnotativeStates.True; } catch (Exc) { }
                try { s.Lunit = rec.Dimlunit; } catch (Exc) { }
                try { s.Tad = rec.Dimtad; } catch (Exc) { }
                try { s.Dec = rec.Dimdec; } catch (Exc) { }
                try { s.Txt = rec.Dimtxt; } catch (Exc) { }
                try { s.Asz = rec.Dimasz; } catch (Exc) { }
                try { s.Scale = rec.Dimscale; } catch (Exc) { }
                try { s.Lfac = rec.Dimlfac; } catch (Exc) { }
                try { s.Post = rec.Dimpost ?? ""; } catch (Exc) { }
                try { s.Tfill = rec.Dimtfill; } catch (Exc) { }
                try { s.Tih = rec.Dimtih; } catch (Exc) { }
                try { s.Toh = rec.Dimtoh; } catch (Exc) { }
                try { s.Gap = rec.Dimgap; } catch (Exc) { }
                try { s.Exe = rec.Dimexe; } catch (Exc) { }
                try { s.Exo = rec.Dimexo; } catch (Exc) { }
                try { s.TextStyleId = rec.Dimtxsty; } catch (Exc) { }
            }
            OverlaySys(s);
            _snap = s;
            return s;
        }

        static void OverlaySys(DimSnap s)
        {
            try { s.Lunit = (int)NumVar("DIMLUNIT"); } catch (Exc) { }
            try { s.Tad = (int)NumVar("DIMTAD"); } catch (Exc) { }
            try { s.Dec = (int)NumVar("DIMDEC"); } catch (Exc) { }
            try { s.Txt = NumVar("DIMTXT"); } catch (Exc) { }
            try { s.Asz = NumVar("DIMASZ"); } catch (Exc) { }
            try { s.Scale = NumVar("DIMSCALE"); } catch (Exc) { }
            try { s.Lfac = NumVar("DIMLFAC"); } catch (Exc) { }
            try { s.Post = StrVar("DIMPOST"); } catch (Exc) { }
            try { s.Tfill = (int)NumVar("DIMTFILL"); } catch (Exc) { }
            try { s.Tih = NumVar("DIMTIH") != 0.0; } catch (Exc) { }
            try { s.Toh = NumVar("DIMTOH") != 0.0; } catch (Exc) { }
            try { s.Gap = NumVar("DIMGAP"); } catch (Exc) { }
            try { s.Exe = NumVar("DIMEXE"); } catch (Exc) { }
            try { s.Exo = NumVar("DIMEXO"); } catch (Exc) { }
        }

        public static void LogDim()
        {
            var s = _snap;
            if (s == null) return;
            NvkLog.Write("[НВК] DIMSTYLE ----------");
            NvkLog.Write("[НВК] DIMSTYLE  имя=" + (string.IsNullOrEmpty(s.Name) ? "(текущий)" : s.Name));
            NvkLog.Write("[НВК] DIMSTYLE  единицы=" + LunitName(s.Lunit)
                + "  DIMLUNIT=" + s.Lunit.ToString(Inv));
            NvkLog.Write("[НВК] DIMSTYLE  текст=" + TadName(s.Tad)
                + "  DIMTAD=" + s.Tad.ToString(Inv));
            NvkLog.Write("[НВК] DIMSTYLE  DIMTXT=" + s.Txt.ToString("0.###", Inv)
                + "  DIMASZ=" + s.Asz.ToString("0.###", Inv)
                + "  DIMSCALE=" + s.Scale.ToString("0.###", Inv)
                + "  DIMDEC=" + s.Dec.ToString(Inv));
            NvkLog.Write("[НВК] DIMSTYLE  DIMLFAC=" + s.Lfac.ToString("0.######", Inv)
                + "  DIMPOST=" + (string.IsNullOrEmpty(s.Post) ? "(нет)" : s.Post)
                + "  DIMTFILL=" + s.Tfill.ToString(Inv));
            NvkLog.Write("[НВК] DIMSTYLE  DIMTIH=" + (s.Tih ? "1" : "0")
                + "  DIMTOH=" + (s.Toh ? "1" : "0")
                + "  DIMGAP=" + s.Gap.ToString("0.###", Inv)
                + "  DIMEXE=" + s.Exe.ToString("0.###", Inv)
                + "  DIMEXO=" + s.Exo.ToString("0.###", Inv));
            NvkLog.Write("[НВК] DIMSTYLE  анно.стиль=" + (s.Anno ? "да" : "нет")
                + "  DIMANNO=" + (s.AnnoSys ? "1" : "0")
                + "  CANNOSCALE=" + (string.IsNullOrEmpty(s.Cannoscale) ? "?" : s.Cannoscale));
            NvkLog.Write("[НВК] DIMSTYLE ----------");
        }

        /// <summary>Повесить на размер свойства текущего стиля (метры, текст сверху, анно и т.д.).</summary>
        public static void ApplyDim(Dimension dim, Transaction tr)
        {
            if (dim == null) return;
            var db = dim.Database;
            var s = _snap ?? CaptureDim(db, tr);
            if (s == null) return;
            try
            {
                if (!s.StyleId.IsNull)
                {
                    dim.DimensionStyle = s.StyleId;
                    try
                    {
                        var rec = tr != null
                            ? tr.GetObject(s.StyleId, OpenMode.ForRead) as DimStyleTableRecord
                            : null;
                        if (rec != null) dim.SetDimstyleData(rec);
                    }
                    catch (Exc) { }
                }
            }
            catch (Exc) { }
            try { dim.Dimlunit = s.Lunit; } catch (Exc) { }
            try { dim.Dimtad = s.Tad; } catch (Exc) { }
            try { dim.Dimdec = s.Dec; } catch (Exc) { }
            try { dim.Dimtxt = s.Txt; } catch (Exc) { }
            try { dim.Dimasz = s.Asz; } catch (Exc) { }
            try { dim.Dimscale = s.Scale; } catch (Exc) { }
            try { dim.Dimlfac = s.Lfac; } catch (Exc) { }
            try { dim.Dimpost = s.Post ?? ""; } catch (Exc) { }
            try { dim.Dimtfill = s.Tfill; } catch (Exc) { }
            try { dim.Dimtih = s.Tih; } catch (Exc) { }
            try { dim.Dimtoh = s.Toh; } catch (Exc) { }
            try { dim.Dimgap = s.Gap; } catch (Exc) { }
            try { dim.Dimexe = s.Exe; } catch (Exc) { }
            try { dim.Dimexo = s.Exo; } catch (Exc) { }
            if (s.Anno || s.AnnoSys)
            {
                Apply(dim);
                EnsureVisible(dim);
            }
            try
            {
                if (!s.StyleId.IsNull)
                    dim.DimensionStyle = s.StyleId;
            }
            catch (Exc) { }
        }

        static bool _hatchAnnoOnce;

        public static void Apply(Entity e)
        {
            if (e == null) return;
            try { e.Annotative = AnnotativeStates.True; }
            catch (Exc) { }
            EnsureVisible(e);
        }

        /// <summary>
        /// Штриховка: Annotative до SetHatchPattern/EvaluateHatch,
        /// иначе флаг не встаёт и OBJECTSCALE пустой.
        /// </summary>
        public static void HatchStart(Hatch h)
        {
            if (h == null) return;
            TryHpAnnoOn();
            ForceAnnotative(h);
            AddPaperScales(h);
        }

        /// <summary>
        /// После EvaluateHatch снова Annotative и масштабы
        /// 1:200 1:500 1:1000 1:5000 — геттер у Hatch врёт, не ждём его.
        /// </summary>
        public static void HatchEnd(Hatch h)
        {
            HatchEnd(h, 0.0);
        }

        public static void HatchEnd(Hatch h, double patternScale)
        {
            if (h == null) return;
            ForceAnnotative(h);
            try { h.EvaluateHatch(true); }
            catch (Exc) { }
            ForceAnnotative(h);
            if (patternScale > 1e-9)
            {
                try { h.PatternScale = patternScale < 0.001 ? 0.001 : patternScale; }
                catch (Exc) { }
            }
            AddPaperScales(h);
            LogHatchOnce(h);
        }

        /// <summary>Выноска: флаг аннотативности и масштабы листов, иначе на ВЭ другого масштаба её не видно.</summary>
        public static void ApplyLeader(Entity e)
        {
            Apply(e);
            EnsureVisible(e);
        }

        /// <summary>
        /// Если объект аннотативный — повесить масштабы листов (CS/EP/план)
        /// и текущий CANNOSCALE, иначе на вкладке 1:200 его не видно
        /// (создан при 1:100).
        /// </summary>
        public static void EnsureVisible(Entity e)
        {
            if (e == null) return;
            try
            {
                if (e.Annotative != AnnotativeStates.True) return;
            }
            catch (Exc) { return; }
            AddPaperScales(e);
        }

        static string _collNameUsed;
        static bool _collLogged;
        static MethodInfo _addCtxMethod;

        public static int EnsureDefaultScales(Database db)
        {
            _hatchAnnoOnce = false;
            _collLogged = false;
            TryHpAnnoOn();
            if (db == null) return 0;
            int n = 0;
            try
            {
                var coll = ScaleColl(db);
                if (coll == null)
                {
                    NvkLog.Write("[НВК] анно.коллекция не найдена (ждали "
                        + AnnoScales.CollName + ")");
                    return 0;
                }
                for (int i = 0; i < AnnoScales.Default.Length; i++)
                {
                    if (EnsureScale(coll, AnnoScales.Default[i]))
                        n++;
                }
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] анно.масштабы: " + ex.Message);
            }
            NvkLog.Write("[НВК] анно.коллекция " + (_collNameUsed ?? "?")
                + "  +" + n.ToString(Inv)
                + "  1:200 1:500 1:1000 1:5000");
            return n;
        }

        /// <summary>
        /// Аннотативный масштаб видового = 1:N (тот же, что вид).
        /// Ставить после AppendEntity. ViewHeight после этого вернуть —
        /// сеттер AnnotationScale может сбить CustomScale.
        /// </summary>
        public static void ApplyToViewport(Viewport vp, int scaleN)
        {
            if (vp == null || vp.Database == null || scaleN < 1) return;
            var db = vp.Database;
            var coll = ScaleColl(db);
            if (coll == null)
            {
                EnsureDefaultScales(db);
                coll = ScaleColl(db);
            }
            if (coll == null) return;
            string name = AnnoScales.NameOf(scaleN);
            AnnotationScale sc = null;
            try
            {
                if (coll.HasContext(name))
                    sc = coll.GetContext(name) as AnnotationScale;
            }
            catch (Exc) { }
            if (sc == null)
            {
                try
                {
                    foreach (ObjectContext oc in coll)
                    {
                        var have = oc as AnnotationScale;
                        if (have == null) continue;
                        if (Near(DenOf(have), scaleN))
                        {
                            sc = have;
                            break;
                        }
                    }
                }
                catch (Exc) { }
            }
            if (sc == null) return;
            try { vp.AnnotationScale = sc; }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] VP анно 1:" + scaleN.ToString(Inv) + ": " + ex.Message);
            }
        }

        public static string ViewportAnnoName(Viewport vp)
        {
            if (vp == null) return "";
            try
            {
                var sc = vp.AnnotationScale;
                if (sc != null && !string.IsNullOrEmpty(sc.Name)) return sc.Name;
            }
            catch (Exc) { }
            return "";
        }

        static ObjectContextCollection ScaleColl(Database db)
        {
            if (db == null) return null;
            var ocm = db.ObjectContextManager;
            if (ocm == null) return null;
            var names = new System.Collections.Generic.List<string>();
            try
            {
                var p = typeof(AnnotationScale).GetProperty("CollectionName",
                    BindingFlags.Public | BindingFlags.Static);
                if (p != null)
                {
                    var v = p.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(v)) names.Add(v);
                }
            }
            catch (Exc) { }
            names.Add(AnnoScales.CollName);
            names.Add(AnnoScales.CollNameAlt);
            ObjectContextCollection empty = null;
            string emptyName = null;
            for (int i = 0; i < names.Count; i++)
            {
                string nm = names[i];
                if (string.IsNullOrEmpty(nm)) continue;
                ObjectContextCollection c = null;
                try { c = ocm.GetContextCollection(nm); }
                catch (Exc) { }
                if (c == null) continue;
                int k = 0;
                try
                {
                    foreach (ObjectContext oc in c) k++;
                }
                catch (Exc) { }
                if (k > 0)
                {
                    _collNameUsed = nm;
                    LogCollOnce(nm, k);
                    return c;
                }
                if (empty == null)
                {
                    empty = c;
                    emptyName = nm;
                }
            }
            if (empty != null)
            {
                _collNameUsed = emptyName;
                LogCollOnce(emptyName, 0);
            }
            return empty;
        }

        static void LogCollOnce(string name, int n)
        {
            if (_collLogged) return;
            _collLogged = true;
            NvkLog.Write("[НВК] анно.коллекция " + name
                + "  шкал " + n.ToString(Inv));
        }

        static void TryHpAnnoOn()
        {
            try { AcadApp.SetSystemVariable("HPANNOTATIVE", 1); }
            catch (Exc) { }
        }

        static void ForceAnnotative(Entity e)
        {
            if (e == null) return;
            try { e.Annotative = AnnotativeStates.True; }
            catch (Exc ex)
            {
                if (!_hatchAnnoOnce)
                    NvkLog.Write("[НВК] штриховка Annotative: " + ex.Message);
            }
            try
            {
                dynamic com = e.AcadObject;
                if (com != null) com.Annotative = true;
            }
            catch (Exc)
            {
                try
                {
                    dynamic com = e.AcadObject;
                    if (com != null) com.Annotative = 1;
                }
                catch (Exc) { }
            }
        }

        static void LogHatchOnce(Hatch h)
        {
            if (_hatchAnnoOnce || h == null) return;
            _hatchAnnoOnce = true;
            try
            {
                bool ok = false;
                try { ok = h.Annotative == AnnotativeStates.True; }
                catch (Exc) { }
                int n = 0;
                try
                {
                    var db = h.Database;
                    if (db != null && db.ObjectContextManager != null)
                    {
                        var coll = ScaleColl(db);
                        if (coll != null)
                        {
                            foreach (ObjectContext oc in coll)
                            {
                                var sc = oc as AnnotationScale;
                                if (sc == null) continue;
                                if (!WantScale(DenOf(sc))) continue;
                                try { if (h.HasContext(sc)) n++; } catch (Exc) { }
                            }
                        }
                    }
                }
                catch (Exc) { }
                NvkLog.Write("[НВК] штриховка анно=" + (ok ? "да" : "нет")
                    + "  масштабов=" + n.ToString(Inv)
                    + "  (1:200 1:500 1:1000 1:5000)");
            }
            catch (Exc) { }
        }

        static bool EnsureScale(ObjectContextCollection coll, double den)
        {
            if (coll == null || den < 1.0) return false;
            string name = AnnoScales.NameOf(den);
            try
            {
                if (coll.HasContext(name)) return false;
            }
            catch (Exc) { }
            foreach (ObjectContext oc in coll)
            {
                var have = oc as AnnotationScale;
                if (have == null) continue;
                double d = DenOf(have);
                if (d > 0.0 && Near(d, den)) return false;
            }
            try
            {
                var sc = new AnnotationScale();
                sc.Name = name;
                sc.PaperUnits = 1.0;
                sc.DrawingUnits = den;
                coll.AddContext(sc);
                return true;
            }
            catch (Exc)
            {
                return false;
            }
        }

        static double DenOf(AnnotationScale sc)
        {
            if (sc == null) return 0.0;
            try
            {
                double p = sc.PaperUnits;
                if (p < 1e-9) p = 1.0;
                return sc.DrawingUnits / p;
            }
            catch (Exc) { return 0.0; }
        }

        /// <summary>
        /// После коммита графики: флаг + 1:200 1:500 1:1000 1:5000 на штриховках,
        /// размерах, выносках в модели и блоках NVK-*. Без False/True — он
        /// обнуляет OBJECTSCALE при ANNOAUTOSCALE=0.
        /// </summary>
        public static int ApplyAll(Database db)
        {
            if (db == null) return 0;
            EnsureDefaultScales(db);
            int n = 0;
            int with = 0;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    int a, b;
                    StampSpace(tr, bt[BlockTableRecord.ModelSpace], out a, out b);
                    n += a; with += b;
                    foreach (ObjectId bid in bt)
                    {
                        var rec = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                        if (rec == null || rec.IsLayout || rec.IsAnonymous) continue;
                        string name = rec.Name ?? "";
                        if (name.Length < 4
                            || name.IndexOf("NVK", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        StampSpace(tr, bid, out a, out b);
                        n += a; with += b;
                    }
                    tr.Commit();
                }
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] анно.масштабы объекты: " + ex.Message);
            }
            BroadcastScales(db);
            ProbeOne(db);
            NvkLog.Write("[НВК] анно.масштабы объектов " + n.ToString(Inv)
                + "  с шкалами " + with.ToString(Inv)
                + "  1:200 1:500 1:1000 1:5000");
            return n;
        }

        static void StampSpace(Transaction tr, ObjectId spaceId, out int visited, out int with)
        {
            visited = 0;
            with = 0;
            if (tr == null || spaceId.IsNull) return;
            var rec = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
            foreach (ObjectId id in rec)
            {
                Entity e = null;
                try { e = tr.GetObject(id, OpenMode.ForWrite) as Entity; }
                catch (Exc) { continue; }
                if (e == null || e.IsErased) continue;
                bool want = e is Hatch || e is Dimension || e is MLeader;
                bool anno = false;
                try { anno = e.Annotative == AnnotativeStates.True; }
                catch (Exc) { }
                if (!anno && !want) continue;
                if (!anno) ForceAnnotative(e);
                AddPaperScales(e);
                visited++;
                if (CountWanted(e) > 0) with++;
            }
        }

        static void AddPaperScales(Entity e)
        {
            if (e == null) return;
            var db = e.Database;
            if (db == null) return;
            try
            {
                if (e.Annotative != AnnotativeStates.True)
                    e.Annotative = AnnotativeStates.True;
            }
            catch (Exc) { }
            var coll = ScaleColl(db);
            if (db.Cannoscale != null)
                Attach(e, db.Cannoscale);
            if (coll != null)
            {
                for (int i = 0; i < AnnoScales.Default.Length; i++)
                {
                    ObjectContext ctx = ContextOf(coll, AnnoScales.Default[i]);
                    if (ctx != null) Attach(e, ctx);
                }
                foreach (ObjectContext oc in coll)
                {
                    var sc = oc as AnnotationScale;
                    if (sc == null) continue;
                    if (!WantScale(DenOf(sc))) continue;
                    Attach(e, oc);
                }
            }
            AddPaperScalesCom(e);
        }

        static ObjectContext ContextOf(ObjectContextCollection coll, double den)
        {
            if (coll == null) return null;
            string name = AnnoScales.NameOf(den);
            try
            {
                var ctx = coll.GetContext(name);
                if (ctx != null) return ctx;
            }
            catch (Exc) { }
            foreach (ObjectContext oc in coll)
            {
                var sc = oc as AnnotationScale;
                if (sc == null) continue;
                if (Near(DenOf(sc), den)) return oc;
            }
            return null;
        }

        static void Attach(Entity e, ObjectContext ctx)
        {
            if (e == null || ctx == null) return;
            try
            {
                if (!e.HasContext(ctx))
                    e.AddContext(ctx);
            }
            catch (Exc) { }
            TryInternalAdd(e, ctx);
        }

        static void TryInternalAdd(Entity e, ObjectContext ctx)
        {
            if (e == null || ctx == null) return;
            try
            {
                if (_addCtxMethod == null)
                {
                    Type t = null;
                    try { t = typeof(Database).Assembly.GetType("Autodesk.AutoCAD.Internal.ObjectContexts"); }
                    catch (Exc) { }
                    if (t == null)
                    {
                        try { t = typeof(AcadApp).Assembly.GetType("Autodesk.AutoCAD.Internal.ObjectContexts"); }
                        catch (Exc) { }
                    }
                    if (t == null) return;
                    _addCtxMethod = t.GetMethod("AddContext",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(DBObject), typeof(ObjectContext) },
                        null);
                    if (_addCtxMethod == null)
                        _addCtxMethod = t.GetMethod("AddContext",
                            BindingFlags.Public | BindingFlags.Static);
                }
                if (_addCtxMethod != null)
                    _addCtxMethod.Invoke(null, new object[] { e, ctx });
            }
            catch (Exc) { }
        }

        static int CountWanted(Entity e)
        {
            if (e == null) return 0;
            int n = 0;
            try
            {
                var coll = ScaleColl(e.Database);
                if (coll == null) return 0;
                foreach (ObjectContext oc in coll)
                {
                    var sc = oc as AnnotationScale;
                    if (sc == null) continue;
                    if (!WantScale(DenOf(sc))) continue;
                    try { if (e.HasContext(oc)) n++; } catch (Exc) { }
                }
            }
            catch (Exc) { }
            return n;
        }

        static void AddPaperScalesCom(Entity e)
        {
            if (e == null) return;
            try
            {
                dynamic com = e.AcadObject;
                if (com == null) return;
                for (int i = 0; i < AnnoScales.Default.Length; i++)
                {
                    string nm = AnnoScales.NameOf(AnnoScales.Default[i]);
                    try { com.ObjectScaleAdd(nm); }
                    catch (Exc)
                    {
                        try { com.AddScale(nm); }
                        catch (Exc) { }
                    }
                }
            }
            catch (Exc) { }
        }

        /// <summary>
        /// Смена CANNOSCALE при ANNOAUTOSCALE=-4 вешает шкалу на все
        /// аннотативные объекты, в том числе внутри блоков NVK-*.
        /// </summary>
        static void BroadcastScales(Database db)
        {
            if (db == null) return;
            object oldAuto = null, oldCanno = null;
            try { oldAuto = AcadApp.GetSystemVariable("ANNOAUTOSCALE"); } catch (Exc) { }
            try { oldCanno = AcadApp.GetSystemVariable("CANNOSCALE"); } catch (Exc) { }
            try { AcadApp.SetSystemVariable("ANNOAUTOSCALE", -4); }
            catch (Exc)
            {
                try { AcadApp.SetSystemVariable("ANNOAUTOSCALE", 4); }
                catch (Exc) { }
            }
            int ok = 0;
            for (int i = 0; i < AnnoScales.Default.Length; i++)
            {
                string nm = AnnoScales.NameOf(AnnoScales.Default[i]);
                try
                {
                    AcadApp.SetSystemVariable("CANNOSCALE", nm);
                    ok++;
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] CANNOSCALE " + nm + ": " + ex.Message);
                }
            }
            try
            {
                if (oldCanno != null)
                    AcadApp.SetSystemVariable("CANNOSCALE", oldCanno);
            }
            catch (Exc) { }
            try
            {
                if (oldAuto != null)
                    AcadApp.SetSystemVariable("ANNOAUTOSCALE", oldAuto);
            }
            catch (Exc) { }
            NvkLog.Write("[НВК] анно.рассылка CANNOSCALE " + ok.ToString(Inv)
                + "/4");
        }

        static void ProbeOne(Database db)
        {
            if (db == null) return;
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    Hatch h = FindHatch(tr, bt[BlockTableRecord.ModelSpace]);
                    if (h == null)
                    {
                        foreach (ObjectId bid in bt)
                        {
                            var rec = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                            if (rec == null || rec.IsLayout || rec.IsAnonymous) continue;
                            string name = rec.Name ?? "";
                            if (name.IndexOf("NVK", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            h = FindHatch(tr, bid);
                            if (h != null) break;
                        }
                    }
                    if (h == null)
                    {
                        NvkLog.Write("[НВК] анно.проверка: штриховки нет");
                        tr.Commit();
                        return;
                    }
                    bool anno = false;
                    try { anno = h.Annotative == AnnotativeStates.True; }
                    catch (Exc) { }
                    int n = CountWanted(h);
                    int comN = -1;
                    try
                    {
                        dynamic com = h.AcadObject;
                        if (com != null)
                        {
                            try { comN = (int)com.ObjectScaleCount; }
                            catch (Exc)
                            {
                                try { comN = (int)com.ScaleCount; }
                                catch (Exc) { }
                            }
                        }
                    }
                    catch (Exc) { }
                    NvkLog.Write("[НВК] анно.проверка штриховка анно="
                        + (anno ? "да" : "нет")
                        + "  HasContext=" + n.ToString(Inv)
                        + "  COM=" + comN.ToString(Inv)
                        + "  коллекция " + (_collNameUsed ?? "?"));
                    tr.Commit();
                }
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] анно.проверка: " + ex.Message);
            }
        }

        static Hatch FindHatch(Transaction tr, ObjectId spaceId)
        {
            if (tr == null || spaceId.IsNull) return null;
            var rec = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
            foreach (ObjectId id in rec)
            {
                Hatch h = null;
                try { h = tr.GetObject(id, OpenMode.ForRead) as Hatch; }
                catch (Exc) { }
                if (h != null && !h.IsErased) return h;
            }
            return null;
        }

        static bool WantScale(double den)
        {
            if (den < 1.0) return false;
            if (AnnoScales.IsDefault(den)) return true;
            if (Near(den, 100)) return true;
            if (Near(den, NvkSession.Cannoscale)) return true;
            if (Near(den, NvkUiState.VpScaleCs)) return true;
            if (Near(den, NvkUiState.VpScaleEp)) return true;
            if (Near(den, NvkUiState.VpScalePlan)) return true;
            return false;
        }

        static bool Near(double a, double b)
        {
            if (b < 1.0) return false;
            return Math.Abs(a - b) < 0.6;
        }

        /// <summary>
        /// Если текущий DIMSTYLE аннотативный — тот же флаг на объекте и CANNOSCALE.
        /// </summary>
        public static void ApplyFromDimStyle(Entity e, Transaction tr)
        {
            var dim = e as Dimension;
            if (dim != null)
            {
                ApplyDim(dim, tr);
                return;
            }
            if (e == null) return;
            if (!DimStyleAnnotative(e.Database, tr)) return;
            Apply(e);
        }

        static string LunitName(int u)
        {
            switch (u)
            {
                case 1: return "научные";
                case 2: return "десятичные (м)";
                case 3: return "инженерные";
                case 4: return "архитектурные";
                case 5: return "дробные";
                case 6: return "Windows";
                default: return "ед." + u.ToString(Inv);
            }
        }

        static string TadName(int t)
        {
            switch (t)
            {
                case 0: return "по центру";
                case 1: return "сверху";
                case 2: return "снаружи";
                case 3: return "JIS";
                case 4: return "снизу";
                default: return "пол." + t.ToString(Inv);
            }
        }

        static double NumVar(string name)
        {
            object v = AcadApp.GetSystemVariable(name);
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is short s) return s;
            if (v is string str)
            {
                double x;
                if (double.TryParse(str.Replace(',', '.'), NumberStyles.Float, Inv, out x))
                    return x;
            }
            return 0.0;
        }

        static string StrVar(string name)
        {
            object v = AcadApp.GetSystemVariable(name);
            return v == null ? "" : Convert.ToString(v, Inv) ?? "";
        }
    }
}
