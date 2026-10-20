using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>
    /// Снимок бумаги пяти листов: видовые + линии/тексты/вставки.
    /// Файл presets/sheets/snap.json — стандартная расстановка (A3).
    /// Нет файла — SheetSnap.StdPage (тот же плот).
    /// </summary>
    static class SheetSnapStore
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        static SheetSnapFile _mem;

        public static string PathOf()
        {
            string root = NvkSession.RootDir;
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nvk");
            string d = Path.Combine(root, "presets", "sheets");
            Directory.CreateDirectory(d);
            return Path.Combine(d, "snap.json");
        }

        public static SheetSnapFile Load()
        {
            string p = PathOf();
            if (!File.Exists(p))
            {
                _mem = null;
                return null;
            }
            try
            {
                _mem = JsonSerializer.Deserialize<SheetSnapFile>(File.ReadAllText(p), Opt);
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] снимок листа: " + ex.Message);
                _mem = null;
            }
            return _mem;
        }

        public static SheetSnapLayout LayoutOf(ProfileKind kind)
        {
            return SheetSnap.Find(Load(), kind.ToString());
        }

        public static string Capture(Document doc)
        {
            if (doc == null) return "[НВК] снимок: нет документа";
            var db = doc.Database;
            var file = new SheetSnapFile { Version = 2 };
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                {
                    if (string.IsNullOrEmpty(e.Key)) continue;
                    if (string.Equals(e.Key, "Model", StringComparison.OrdinalIgnoreCase))
                        continue;
                    ProfileKind kind;
                    if (!NvkSheets.TryKind(e.Key, out kind)) continue;
                    var layout = tr.GetObject(e.Value, OpenMode.ForRead) as Layout;
                    if (layout == null) continue;
                    var snap = ReadLayout(tr, layout, kind, e.Key);
                    if (snap != null) file.Layouts.Add(snap);
                }
                tr.Commit();
            }
            FillDefaultPage(file);
            string path = PathOf();
            File.WriteAllText(path, JsonSerializer.Serialize(file, Opt), new UTF8Encoding(false));
            _mem = file;
            int nVp = 0, nEnt = 0;
            for (int i = 0; i < file.Layouts.Count; i++)
            {
                var L = file.Layouts[i];
                nVp += L.Vps != null ? L.Vps.Count : 0;
                nEnt += L.Ents != null ? L.Ents.Count : 0;
            }
            string msg = "[НВК] снимок листа: вкладок " + file.Layouts.Count.ToString()
                + "  видовых " + nVp.ToString()
                + "  объектов " + nEnt.ToString();
            if (SheetSnap.HasPage(file.Page))
                msg += "  плот " + (file.Page.Device ?? "")
                    + "  " + (file.Page.Media ?? "")
                    + "  бумага " + file.Page.PaperW.ToString("0", Inv)
                    + "x" + file.Page.PaperH.ToString("0", Inv);
            msg += "  " + path;
            NvkLog.Write(msg);
            return msg;
        }

        static void FillDefaultPage(SheetSnapFile file)
        {
            if (file == null) return;
            file.Page = SheetSnap.DefaultPage(file);
        }

        public static SheetSnapPage PageOf(ProfileKind kind)
        {
            return SheetSnap.PageOf(Load(), kind.ToString());
        }

        static SheetSnapLayout ReadLayout(Transaction tr, Layout layout, ProfileKind kind, string name)
        {
            var snap = new SheetSnapLayout { Kind = kind.ToString(), Name = name };
            snap.Page = ReadPage(layout);
            var ps = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            foreach (ObjectId id in ps)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                var vp = ent as Viewport;
                if (vp != null)
                {
                    if (vp.Number == 1) continue;
                    string key = NvkTag.VpKey(vp);
                    if (string.IsNullOrEmpty(key)) continue;
                    snap.Vps.Add(new SheetSnapVp
                    {
                        Key = key,
                        Cx = vp.CenterPoint.X,
                        Cy = vp.CenterPoint.Y,
                        W = vp.Width,
                        H = vp.Height
                    });
                    continue;
                }
                var se = FromEnt(tr, ent);
                if (se != null) snap.Ents.Add(se);
            }
            return snap;
        }

        static SheetSnapEnt FromEnt(Transaction tr, Entity ent)
        {
            if (ent is AttributeDefinition || ent is AttributeReference)
                return null;
            var se = new SheetSnapEnt
            {
                Layer = ent.Layer ?? "0",
                Color = ColorOf(ent),
                Lt = ent.Linetype
            };
            var ln = ent as Line;
            if (ln != null)
            {
                se.T = "LINE";
                se.X = ln.StartPoint.X;
                se.Y = ln.StartPoint.Y;
                se.X1 = ln.EndPoint.X;
                se.Y1 = ln.EndPoint.Y;
                return se;
            }
            var pl = ent as Polyline;
            if (pl != null)
            {
                se.T = "PL";
                se.Closed = pl.Closed;
                se.Pts = PtsOf(pl);
                return se;
            }
            var p2 = ent as Polyline2d;
            if (p2 != null)
            {
                se.T = "PL";
                se.Closed = p2.Closed;
                se.Pts = PtsOf2d(tr, p2);
                return se;
            }
            var c = ent as Circle;
            if (c != null)
            {
                se.T = "CIRCLE";
                se.X = c.Center.X;
                se.Y = c.Center.Y;
                se.R = c.Radius;
                return se;
            }
            var a = ent as Arc;
            if (a != null)
            {
                se.T = "ARC";
                se.X = a.Center.X;
                se.Y = a.Center.Y;
                se.R = a.Radius;
                se.A0 = a.StartAngle;
                se.A1 = a.EndAngle;
                return se;
            }
            var dt = ent as DBText;
            if (dt != null)
            {
                se.T = "TEXT";
                se.X = dt.Position.X;
                se.Y = dt.Position.Y;
                se.H = dt.Height;
                se.Rot = dt.Rotation;
                se.Txt = dt.TextString ?? "";
                se.Style = StyleName(tr, dt.TextStyleId);
                se.Att = (int)dt.HorizontalMode;
                return se;
            }
            var mt = ent as MText;
            if (mt != null)
            {
                se.T = "MTEXT";
                se.X = mt.Location.X;
                se.Y = mt.Location.Y;
                se.H = mt.TextHeight;
                se.W = mt.Width;
                se.Rot = mt.Rotation;
                se.Att = (int)mt.Attachment;
                se.Txt = mt.Contents ?? "";
                se.Tpl = NvkTag.Get1000(mt, NvkTag.TplKey);
                se.Style = StyleName(tr, mt.TextStyleId);
                return se;
            }
            var br = ent as BlockReference;
            if (br != null)
            {
                string bn = BlockName(tr, br);
                if (string.IsNullOrEmpty(bn) || bn.StartsWith("*", StringComparison.Ordinal))
                    return null;
                se.T = "INSERT";
                se.Name = bn;
                se.X = br.Position.X;
                se.Y = br.Position.Y;
                se.Rot = br.Rotation;
                try
                {
                    se.Sx = br.ScaleFactors.X;
                    se.Sy = br.ScaleFactors.Y;
                }
                catch
                {
                    se.Sx = 1.0;
                    se.Sy = 1.0;
                }
                return se;
            }
            return null;
        }

        static string PtsOf(Polyline pl)
        {
            var sb = new StringBuilder();
            int n = pl.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                var p = pl.GetPoint2dAt(i);
                if (sb.Length > 0) sb.Append(';');
                sb.Append(p.X.ToString("G", Inv));
                sb.Append(',');
                sb.Append(p.Y.ToString("G", Inv));
            }
            return sb.ToString();
        }

        static string PtsOf2d(Transaction tr, Polyline2d p2)
        {
            var sb = new StringBuilder();
            foreach (ObjectId id in p2)
            {
                var v = tr.GetObject(id, OpenMode.ForRead) as Vertex2d;
                if (v == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(v.Position.X.ToString("G", Inv));
                sb.Append(',');
                sb.Append(v.Position.Y.ToString("G", Inv));
            }
            return sb.ToString();
        }

        static string StyleName(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return "";
            try
            {
                var rec = tr.GetObject(id, OpenMode.ForRead) as TextStyleTableRecord;
                return rec != null ? rec.Name : "";
            }
            catch { return ""; }
        }

        static string BlockName(Transaction tr, BlockReference br)
        {
            try
            {
                var rec = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                return rec != null ? rec.Name : "";
            }
            catch { return ""; }
        }

        static short ColorOf(Entity ent)
        {
            try
            {
                if (ent.Color == null || ent.Color.IsByLayer) return 256;
                return (short)ent.Color.ColorIndex;
            }
            catch { return 256; }
        }

        public static void PlaceTitle(MText title, ProfileKind kind)
        {
            if (title == null) return;
            var t = SheetSnap.TitleEnt(LayoutOf(kind));
            if (t == null) return;
            try { title.Location = new Point3d(t.X, t.Y, 0.0); } catch { }
            if (t.H > 0.05)
            {
                try { title.TextHeight = t.H; } catch { }
            }
            if (t.W > 0.0)
            {
                try { title.Width = t.W; } catch { }
            }
        }

        public static int Spawn(Database db, Transaction tr, BlockTableRecord ps, ProfileKind kind)
        {
            var lay = LayoutOf(kind);
            if (lay == null || lay.Ents == null || lay.Ents.Count == 0) return 0;
            int nPaper = CountPaper(tr, ps);
            if (nPaper > 2)
            {
                NvkLog.Write("[НВК] снимок " + kind.ToString()
                    + " бумага уже с объектами (" + nPaper.ToString() + "), линии не дублируем");
                return 0;
            }
            int n = 0;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null || SheetSnap.IsSheetTitle(e)) continue;
                try
                {
                    if (PutEnt(db, tr, ps, e)) n++;
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] снимок ent " + (e.T ?? "?") + ": " + ex.Message);
                }
            }
            NvkLog.Write("[НВК] снимок " + kind.ToString() + " объектов +" + n.ToString());
            return n;
        }

        static int CountPaper(Transaction tr, BlockTableRecord ps)
        {
            int n = 0;
            foreach (ObjectId id in ps)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                if (ent is Viewport) continue;
                var mt = ent as MText;
                if (mt != null
                    && string.Equals(mt.Layer, "NVK-SHEET", StringComparison.OrdinalIgnoreCase))
                    continue;
                n++;
            }
            return n;
        }

        static bool PutEnt(Database db, Transaction tr, BlockTableRecord ps, SheetSnapEnt e)
        {
            EnsureLayer(db, tr, e.Layer, e.Color);
            Entity ent = null;
            string t = e.T ?? "";
            if (string.Equals(t, "LINE", StringComparison.OrdinalIgnoreCase))
            {
                var ln = new Line(new Point3d(e.X, e.Y, 0.0), new Point3d(e.X1, e.Y1, 0.0));
                ent = ln;
            }
            else if (string.Equals(t, "PL", StringComparison.OrdinalIgnoreCase))
            {
                var pl = new Polyline();
                var pts = SplitPts(e.Pts);
                for (int i = 0; i < pts.Count; i++)
                    pl.AddVertexAt(i, pts[i], 0.0, 0.0, 0.0);
                pl.Closed = e.Closed;
                ent = pl;
            }
            else if (string.Equals(t, "CIRCLE", StringComparison.OrdinalIgnoreCase))
            {
                ent = new Circle(new Point3d(e.X, e.Y, 0.0), Vector3d.ZAxis, e.R > 1e-9 ? e.R : 1.0);
            }
            else if (string.Equals(t, "ARC", StringComparison.OrdinalIgnoreCase))
            {
                ent = new Arc(new Point3d(e.X, e.Y, 0.0), e.R > 1e-9 ? e.R : 1.0, e.A0, e.A1);
            }
            else if (string.Equals(t, "TEXT", StringComparison.OrdinalIgnoreCase))
            {
                var dt = new DBText();
                dt.SetDatabaseDefaults();
                dt.Position = new Point3d(e.X, e.Y, 0.0);
                dt.Height = e.H > 0.05 ? e.H : 2.5;
                dt.Rotation = e.Rot;
                dt.TextString = e.Txt ?? "";
                ApplyStyle(tr, db, dt, e.Style);
                try { dt.Annotative = AnnotativeStates.False; } catch { }
                ent = dt;
            }
            else if (string.Equals(t, "MTEXT", StringComparison.OrdinalIgnoreCase))
            {
                var mt = new MText();
                mt.SetDatabaseDefaults();
                mt.Location = new Point3d(e.X, e.Y, 0.0);
                mt.TextHeight = e.H > 0.05 ? e.H : 2.5;
                mt.Width = e.W > 0.0 ? e.W : 0.0;
                mt.Rotation = e.Rot;
                try { mt.Attachment = (AttachmentPoint)e.Att; } catch { }
                mt.Contents = e.Txt ?? "";
                ApplyStyle(tr, db, mt, e.Style);
                try { mt.Annotative = AnnotativeStates.False; } catch { }
                ent = mt;
            }
            else if (string.Equals(t, "INSERT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(e.Name)) return false;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (!bt.Has(e.Name))
                {
                    NvkLog.Write("[НВК] снимок нет блока " + e.Name);
                    return false;
                }
                var br = new BlockReference(new Point3d(e.X, e.Y, 0.0), bt[e.Name]);
                double sx = e.Sx != 0.0 ? e.Sx : 1.0;
                double sy = e.Sy != 0.0 ? e.Sy : 1.0;
                try { br.ScaleFactors = new Scale3d(sx, sy, 1.0); } catch { }
                br.Rotation = e.Rot;
                ent = br;
            }
            if (ent == null) return false;
            ent.SetDatabaseDefaults();
            if (!string.IsNullOrEmpty(e.Layer))
            {
                try { ent.Layer = e.Layer; } catch { }
            }
            ApplyColor(ent, e.Color);
            ApplyLt(tr, db, ent, e.Lt);
            ps.AppendEntity(ent);
            tr.AddNewlyCreatedDBObject(ent, true);
            if (!string.IsNullOrEmpty(e.Tpl))
                NvkTag.Put1000(tr, ent, NvkTag.TplKey, e.Tpl);
            return true;
        }

        static List<Point2d> SplitPts(string s)
        {
            var r = new List<Point2d>();
            if (string.IsNullOrEmpty(s)) return r;
            var parts = s.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (string.IsNullOrEmpty(p)) continue;
                int c = p.IndexOf(',');
                if (c <= 0) continue;
                double x, y;
                if (!double.TryParse(p.Substring(0, c), NumberStyles.Float, Inv, out x)) continue;
                if (!double.TryParse(p.Substring(c + 1), NumberStyles.Float, Inv, out y)) continue;
                r.Add(new Point2d(x, y));
            }
            return r;
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            if (string.IsNullOrEmpty(name)) return;
            short aci = color > 0 && color < 256 ? color : (short)7;
            NvkLayers.EnsureOne(db, tr, name, aci, false, false);
        }

        static void ApplyColor(Entity ent, short color)
        {
            try
            {
                if (color == 256)
                    ent.Color = AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
                else if (color > 0)
                    ent.Color = AcadColor.FromColorIndex(ColorMethod.ByAci, color);
            }
            catch { }
        }

        static void ApplyLt(Transaction tr, Database db, Entity ent, string lt)
        {
            if (string.IsNullOrEmpty(lt) || string.Equals(lt, "ByLayer", StringComparison.OrdinalIgnoreCase))
                return;
            try
            {
                var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (ltt.Has(lt)) ent.Linetype = lt;
            }
            catch { }
        }

        static void ApplyStyle(Transaction tr, Database db, Entity ent, string style)
        {
            try
            {
                ObjectId sid = db.Textstyle;
                if (!string.IsNullOrEmpty(style))
                {
                    var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    if (tst.Has(style)) sid = tst[style];
                }
                var mt = ent as MText;
                if (mt != null) { mt.TextStyleId = sid; return; }
                var dt = ent as DBText;
                if (dt != null) dt.TextStyleId = sid;
            }
            catch { }
        }

        static SheetSnapPage ReadPage(Layout layout)
        {
            if (layout == null) return null;
            var p = new SheetSnapPage();
            try { p.Device = layout.PlotConfigurationName ?? ""; } catch { }
            try { p.Media = layout.CanonicalMediaName ?? ""; } catch { }
            try { p.Units = layout.PlotPaperUnits.ToString(); } catch { p.Units = "Millimeters"; }
            try { p.PlotType = layout.PlotType.ToString(); } catch { p.PlotType = "Layout"; }
            try { p.Rotation = layout.PlotRotation.ToString(); } catch { p.Rotation = "Degrees000"; }
            try
            {
                Point2d o = layout.PlotOrigin;
                p.Ox = o.X;
                p.Oy = o.Y;
            }
            catch { }
            try { p.StdScale = layout.UseStandardScale; } catch { p.StdScale = true; }
            try { p.ScaleType = layout.StdScaleType.ToString(); } catch { }
            try
            {
                CustomScale cs = layout.CustomPrintScale;
                p.ScaleNum = cs.Numerator;
                p.ScaleDen = cs.Denominator;
            }
            catch { }
            try { p.Style = layout.CurrentStyleSheet ?? ""; } catch { }
            try { p.PlotStyles = layout.PlotPlotStyles; } catch { }
            try { p.ScaleLw = layout.ScaleLineweights; } catch { }
            try { p.Centered = layout.PlotCentered; } catch { }
            try { p.Lineweights = layout.PrintLineweights; } catch { }
            try { p.VpBorders = layout.PlotViewportBorders; } catch { }
            try
            {
                Point2d sz = layout.PlotPaperSize;
                p.PaperW = sz.X;
                p.PaperH = sz.Y;
            }
            catch { }
            return SheetSnap.HasPage(p) ? p : null;
        }

        public static bool ApplyPage(Layout layout, SheetSnapPage page, string name)
        {
            if (layout == null || !SheetSnap.HasPage(page)) return false;
            try
            {
                var psv = PlotSettingsValidator.Current;
                psv.RefreshLists(layout);
                string dev = PickDevice(psv, page.Device);
                if (string.IsNullOrEmpty(dev)) return false;
                string media = page.Media ?? "";
                bool set = false;
                if (!string.IsNullOrEmpty(media))
                {
                    try { psv.SetPlotConfigurationName(layout, dev, media); set = true; }
                    catch { }
                }
                if (!set)
                {
                    try { psv.SetPlotConfigurationName(layout, dev, ""); set = true; }
                    catch { }
                }
                if (!set) return false;
                try { psv.RefreshLists(layout); } catch { }
                if (!string.IsNullOrEmpty(media)
                    && HasName(psv.GetCanonicalMediaNameList(layout), media))
                {
                    try { psv.SetCanonicalMediaName(layout, media); } catch { }
                }
                PlotPaperUnit units;
                if (TryEnum(page.Units, PlotPaperUnit.Millimeters, out units))
                    try { psv.SetPlotPaperUnits(layout, units); } catch { }
                PlotType ptype;
                if (TryEnum(page.PlotType, PlotType.Layout, out ptype))
                    try { psv.SetPlotType(layout, ptype); } catch { }
                PlotRotation rot;
                if (TryEnum(page.Rotation, PlotRotation.Degrees000, out rot))
                    try { psv.SetPlotRotation(layout, rot); } catch { }
                try { psv.SetPlotCentered(layout, page.Centered); } catch { }
                if (!page.Centered)
                {
                    try { psv.SetPlotOrigin(layout, new Point2d(page.Ox, page.Oy)); } catch { }
                }
                try { psv.SetUseStandardScale(layout, page.StdScale); } catch { }
                if (page.StdScale)
                {
                    StdScaleType st;
                    if (TryEnum(page.ScaleType, StdScaleType.StdScale1To1, out st))
                        try { psv.SetStdScaleType(layout, st); } catch { }
                }
                else if (page.ScaleNum > 1e-9 && page.ScaleDen > 1e-9)
                {
                    try { psv.SetCustomPrintScale(layout, new CustomScale(page.ScaleNum, page.ScaleDen)); }
                    catch { }
                }
                if (!string.IsNullOrEmpty(page.Style))
                {
                    try
                    {
                        if (HasName(psv.GetPlotStyleSheetList(), page.Style))
                            psv.SetCurrentStyleSheet(layout, page.Style);
                    }
                    catch { }
                }
                try { layout.PlotPlotStyles = page.PlotStyles; } catch { }
                try { layout.ScaleLineweights = page.ScaleLw; } catch { }
                try { layout.PrintLineweights = page.Lineweights; } catch { }
                try { layout.PlotViewportBorders = page.VpBorders; } catch { }
                NvkLog.Write("[НВК] лист " + name + " плот " + dev
                    + "  " + (layout.CanonicalMediaName ?? media));
                return true;
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] лист " + name + " плот: " + ex.Message);
                return false;
            }
        }

        public static string ApplyDefaultToAll(Document doc)
        {
            if (doc == null) return "[НВК] параметры листа: нет документа";
            var db = doc.Database;
            SheetSnapPage page = SheetSnap.DefaultPage(Load());
            if (!SheetSnap.HasPage(page))
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var lm = LayoutManager.Current;
                        string cur = lm.CurrentLayout;
                        if (!string.IsNullOrEmpty(cur)
                            && !string.Equals(cur, "Model", StringComparison.OrdinalIgnoreCase))
                        {
                            var lay = tr.GetObject(lm.GetLayoutId(cur), OpenMode.ForRead) as Layout;
                            page = ReadPage(lay);
                        }
                    }
                    catch { }
                    tr.Commit();
                }
            }
            if (!SheetSnap.HasPage(page))
                return "[НВК] нет параметров листа. Сначала снимок или открой вкладку с нужным плотом.";

            int nOk = 0, nFail = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in dict)
                {
                    if (string.IsNullOrEmpty(e.Key)) continue;
                    if (string.Equals(e.Key, "Model", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Layout layout;
                    try { layout = tr.GetObject(e.Value, OpenMode.ForWrite) as Layout; }
                    catch { nFail++; continue; }
                    if (layout == null) { nFail++; continue; }
                    if (ApplyPage(layout, page, e.Key)) nOk++;
                    else nFail++;
                }
                tr.Commit();
            }
            string msg = "[НВК] параметры листа всем: ок " + nOk.ToString();
            if (nFail > 0) msg += "  сбой " + nFail.ToString();
            msg += "  " + (page.Device ?? "") + "  " + (page.Media ?? "");
            NvkLog.Write(msg);
            return msg;
        }

        static string PickDevice(PlotSettingsValidator psv, string want)
        {
            System.Collections.Specialized.StringCollection have = null;
            try { have = psv.GetPlotDeviceList(); } catch { }
            if (HasName(have, want)) return want;
            if (have == null) return want;
            foreach (string d in have)
            {
                if (string.IsNullOrEmpty(d)) continue;
                if (d.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                    return d;
            }
            return want;
        }

        static bool HasName(System.Collections.Specialized.StringCollection list, string name)
        {
            if (list == null || string.IsNullOrEmpty(name)) return false;
            foreach (string s in list)
            {
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static bool TryEnum<T>(string s, T fallback, out T value) where T : struct
        {
            if (!string.IsNullOrEmpty(s) && Enum.TryParse(s, true, out value))
                return true;
            value = fallback;
            return true;
        }
    }
}
