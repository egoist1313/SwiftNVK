using System;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
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
                Apply(dim);
            try
            {
                if (!s.StyleId.IsNull)
                    dim.DimensionStyle = s.StyleId;
            }
            catch (Exc) { }
        }

        public static void Apply(Entity e)
        {
            if (e == null) return;
            try { e.Annotative = AnnotativeStates.True; }
            catch (Exc) { return; }
            try
            {
                var db = e.Database;
                if (db == null) return;
                var scale = db.Cannoscale;
                if (scale == null) return;
                e.AddContext(scale);
            }
            catch (Exc) { }
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
