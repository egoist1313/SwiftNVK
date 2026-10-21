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
            if (file.Layouts != null)
            {
                for (int i = 0; i < file.Layouts.Count; i++)
                {
                    var L = file.Layouts[i];
                    if (L != null) SheetSnap.FillWindow(L.Page, L);
                }
            }
            SheetSnap.FillWindow(file.Page, SheetSnap.Find(file, "Razrabotka"));
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

        public static SheetSnapPage PageForApply(ProfileKind kind,
            out double dx, out double dy, out double fw, out double fh)
        {
            var file = Load();
            var page = SheetSnap.CopyPage(SheetSnap.PageOf(file, kind.ToString()));
            if (!SheetSnap.ShiftOf(file, kind.ToString(), out dx, out dy, out fw, out fh))
            {
                dx = 0.0;
                dy = 0.0;
                fw = 420.0;
                fh = 297.0;
            }
            SheetSnap.FitPage(page, fw, fh);
            return page;
        }

        static SheetSnapLayout ReadLayout(Transaction tr, Layout layout, ProfileKind kind, string name)
        {
            var snap = new SheetSnapLayout { Kind = kind.ToString(), Name = name };
            snap.Page = ReadPage(layout);
            var ps = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            var have = new List<string>();
            foreach (ObjectId id in ps)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased) continue;
                var vp = ent as Viewport;
                if (vp != null)
                {
                    if (vp.Number == 1) continue;
                    var rec = ReadVp(tr, vp, kind.ToString(), have);
                    if (rec == null) continue;
                    snap.Vps.Add(rec);
                    if (!string.IsNullOrEmpty(rec.Key)) have.Add(rec.Key);
                    continue;
                }
                CaptureEnt(tr, ent, snap.Ents);
            }
            return snap;
        }

        static SheetSnapVp ReadVp(Transaction tr, Viewport vp, string kind, List<string> have)
        {
            if (vp == null) return null;
            int scaleN = 0;
            try { scaleN = SheetSnap.ScaleNFromCustom(vp.CustomScale); }
            catch { }
            var rec = new SheetSnapVp
            {
                Cx = vp.CenterPoint.X,
                Cy = vp.CenterPoint.Y,
                W = vp.Width,
                H = vp.Height,
                ScaleN = scaleN
            };
            try
            {
                rec.Vx = vp.ViewTarget.X;
                rec.Vy = vp.ViewTarget.Y;
            }
            catch { }
            try { rec.Vh = vp.ViewHeight; } catch { }
            try { rec.Twist = vp.TwistAngle; } catch { }
            if (SheetSnap.IsJunkVp(rec))
            {
                NvkLog.Write("[НВК] снимок VP мимо "
                    + rec.W.ToString("0") + "x" + rec.H.ToString("0"));
                return null;
            }
            string tagged = NvkTag.VpKey(vp);
            string key = SheetSnap.InferVpKey(tagged, scaleN, kind, have);
            if (string.IsNullOrEmpty(key)) return null;
            rec.Key = key;
            if (string.IsNullOrEmpty(tagged))
            {
                try
                {
                    if (!vp.IsWriteEnabled) vp.UpgradeOpen();
                    NvkTag.StampVp(tr, vp, key);
                    NvkLog.Write("[НВК] снимок VP метка " + key
                        + "  1:" + scaleN.ToString());
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] снимок VP метка: " + ex.Message);
                }
            }
            return rec;
        }

        static void CaptureEnt(Transaction tr, Entity ent, List<SheetSnapEnt> ents)
        {
            if (ent == null || ents == null) return;
            var br = ent as BlockReference;
            if (br != null)
            {
                string bn = BlockName(tr, br);
                if (SheetSnap.IsNorthBlockName(bn)
                    || (SheetSnap.IsTransientBlock(bn) && LooksLikeNorth(tr, br)))
                {
                    ents.Add(NorthInsert(tr, br));
                    NvkLog.Write("[НВК] снимок стрелка севера " + (bn ?? "Arrow3"));
                    return;
                }
                if (SheetSnap.IsTransientBlock(bn))
                {
                    int n = ExplodeInto(tr, br, ents, 0);
                    if (n > 0)
                    {
                        NvkLog.Write("[НВК] снимок блок " + bn + " → " + n.ToString() + " частей");
                        return;
                    }
                }
            }
            var se = FromEnt(tr, ent);
            if (se != null) ents.Add(se);
        }

        static SheetSnapEnt NorthInsert(Transaction tr, BlockReference br)
        {
            var se = new SheetSnapEnt
            {
                T = "INSERT",
                Name = "Arrow3",
                Layer = br.Layer ?? "0",
                Color = ColorOf(br),
                Lt = br.Linetype
            };
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
                se.Sx = 25.4;
                se.Sy = 25.4;
            }
            if (Math.Abs(se.Sx) < 1e-6) se.Sx = 25.4;
            if (Math.Abs(se.Sy) < 1e-6) se.Sy = 25.4;
            return se;
        }

        static bool LooksLikeNorth(Transaction tr, BlockReference br)
        {
            if (br == null) return false;
            DBObjectCollection bits;
            try
            {
                bits = new DBObjectCollection();
                br.Explode(bits);
            }
            catch { return false; }
            bool hit = false;
            foreach (DBObject o in bits)
            {
                var dt = o as DBText;
                if (dt != null && string.Equals(dt.TextString, "N", StringComparison.Ordinal))
                    hit = true;
                var mt = o as MText;
                if (mt != null && mt.Contents != null
                    && mt.Contents.IndexOf("N", StringComparison.Ordinal) >= 0
                    && mt.Contents.Length < 8)
                    hit = true;
                try { o.Dispose(); } catch { }
            }
            return hit;
        }

        static int ExplodeInto(Transaction tr, BlockReference br, List<SheetSnapEnt> ents, int depth)
        {
            if (br == null || ents == null || depth > 4) return 0;
            DBObjectCollection bits;
            try
            {
                bits = new DBObjectCollection();
                br.Explode(bits);
            }
            catch { return 0; }
            if (bits == null) return 0;
            int n = 0;
            foreach (DBObject o in bits)
            {
                var e = o as Entity;
                if (e == null)
                {
                    try { o.Dispose(); } catch { }
                    continue;
                }
                var nested = e as BlockReference;
                if (nested != null)
                {
                    n += ExplodeInto(tr, nested, ents, depth + 1);
                    try { e.Dispose(); } catch { }
                    continue;
                }
                var se = FromEnt(tr, e);
                if (se != null)
                {
                    ents.Add(se);
                    n++;
                }
                try { e.Dispose(); } catch { }
            }
            return n;
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
            var sol = ent as Solid;
            if (sol != null)
            {
                se.T = "SOLID";
                try
                {
                    var sb = new StringBuilder();
                    for (short i = 0; i < 4; i++)
                    {
                        var p = sol.GetPointAt(i);
                        if (sb.Length > 0) sb.Append(';');
                        sb.Append(p.X.ToString("G", Inv));
                        sb.Append(',');
                        sb.Append(p.Y.ToString("G", Inv));
                    }
                    se.Pts = sb.ToString();
                }
                catch { return null; }
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
            double dx, dy, fw, fh;
            SheetSnap.ShiftOf(Load(), kind.ToString(), out dx, out dy, out fw, out fh);
            try { title.Location = new Point3d(t.X + dx, t.Y + dy, 0.0); } catch { }
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
            double fx0, fy0, fx1, fy1;
            bool frame = SheetSnap.TryFrame(Load(), kind.ToString(), out fx0, out fy0, out fx1, out fy1);
            double dx, dy, fw, fh;
            SheetSnap.ShiftOf(Load(), kind.ToString(), out dx, out dy, out fw, out fh);
            int n = 0, nSkip = 0, nDup = 0;
            ObjectId northId = EnsureNorthBlock(db, tr);
            if (!northId.IsNull)
                EraseNorthDebris(tr, ps);
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null || SheetSnap.IsSheetTitle(e)) continue;
                if (frame && !SheetSnap.IsNearPaper(e, fx0, fy0, fx1, fy1))
                {
                    nSkip++;
                    continue;
                }
                if (SheetSnap.IsNorthDebris(e) || SheetSnap.IsNorthInsert(e))
                {
                    if (northId.IsNull && SheetSnap.IsNorthDebris(e))
                    {
                        try
                        {
                            if (PutEnt(db, tr, ps, e, dx, dy)) n++;
                        }
                        catch (Exc ex)
                        {
                            NvkLog.Write("[НВК] снимок ent " + (e.T ?? "?") + ": " + ex.Message);
                        }
                    }
                    continue;
                }
                if (HasTwin(tr, ps, e, dx, dy))
                {
                    nDup++;
                    continue;
                }
                try
                {
                    if (PutEnt(db, tr, ps, e, dx, dy)) n++;
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] снимок ent " + (e.T ?? "?") + ": " + ex.Message);
                }
            }
            if (nSkip > 0)
                NvkLog.Write("[НВК] снимок " + kind.ToString()
                    + " мимо рамки " + nSkip.ToString());
            NvkLog.Write("[НВК] снимок " + kind.ToString()
                + " объектов +" + n.ToString()
                + (nDup > 0 ? "  уже " + nDup.ToString() : ""));
            return n;
        }

        static bool HasTwin(Transaction tr, BlockTableRecord ps, SheetSnapEnt e, double dx, double dy)
        {
            if (ps == null || e == null) return false;
            double tx = e.X + dx;
            double ty = e.Y + dy;
            string t = e.T ?? "";
            foreach (ObjectId id in ps)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased || ent is Viewport) continue;
                if (string.Equals(t, "INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    var br = ent as BlockReference;
                    if (br == null) continue;
                    if (Dist2(br.Position.X, br.Position.Y, tx, ty) > 6.25) continue;
                    if (SameBlock(BlockName(tr, br), e.Name)) return true;
                    continue;
                }
                if (string.Equals(t, "MTEXT", StringComparison.OrdinalIgnoreCase))
                {
                    var mt = ent as MText;
                    if (mt == null) continue;
                    if (Dist2(mt.Location.X, mt.Location.Y, tx, ty) > 6.25) continue;
                    return true;
                }
                if (string.Equals(t, "TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    var dt = ent as DBText;
                    if (dt == null) continue;
                    if (Dist2(dt.Position.X, dt.Position.Y, tx, ty) > 6.25) continue;
                    return true;
                }
                if (string.Equals(t, "LINE", StringComparison.OrdinalIgnoreCase))
                {
                    var ln = ent as Line;
                    if (ln == null) continue;
                    double x1 = e.X1 + dx, y1 = e.Y1 + dy;
                    if (Dist2(ln.StartPoint.X, ln.StartPoint.Y, tx, ty) < 1.0
                        && Dist2(ln.EndPoint.X, ln.EndPoint.Y, x1, y1) < 1.0)
                        return true;
                    if (Dist2(ln.StartPoint.X, ln.StartPoint.Y, x1, y1) < 1.0
                        && Dist2(ln.EndPoint.X, ln.EndPoint.Y, tx, ty) < 1.0)
                        return true;
                    continue;
                }
                if (string.Equals(t, "CIRCLE", StringComparison.OrdinalIgnoreCase))
                {
                    var c = ent as Circle;
                    if (c == null) continue;
                    if (Dist2(c.Center.X, c.Center.Y, tx, ty) < 1.0) return true;
                    continue;
                }
                if (string.Equals(t, "PL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "SOLID", StringComparison.OrdinalIgnoreCase))
                {
                    var pts = SplitPts(e.Pts);
                    if (pts.Count == 0) continue;
                    double px = pts[0].X + dx, py = pts[0].Y + dy;
                    var pl = ent as Polyline;
                    if (pl != null && pl.NumberOfVertices > 0)
                    {
                        var p0 = pl.GetPoint2dAt(0);
                        if (Dist2(p0.X, p0.Y, px, py) < 1.0) return true;
                    }
                    var sol = ent as Solid;
                    if (sol != null)
                    {
                        try
                        {
                            var p0 = sol.GetPointAt(0);
                            if (Dist2(p0.X, p0.Y, px, py) < 1.0) return true;
                        }
                        catch { }
                    }
                }
            }
            return false;
        }

        static double Dist2(double x0, double y0, double x1, double y1)
        {
            double dx = x0 - x1, dy = y0 - y1;
            return dx * dx + dy * dy;
        }

        static bool SameBlock(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(SheetSnap.BlockBaseName(a), SheetSnap.BlockBaseName(b),
                StringComparison.OrdinalIgnoreCase);
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

        static bool PutEnt(Database db, Transaction tr, BlockTableRecord ps, SheetSnapEnt e,
            double dx, double dy)
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
            else if (string.Equals(t, "SOLID", StringComparison.OrdinalIgnoreCase))
            {
                var pts = SplitPts(e.Pts);
                if (pts.Count < 3) return false;
                var p0 = new Point3d(pts[0].X, pts[0].Y, 0.0);
                var p1 = new Point3d(pts[1].X, pts[1].Y, 0.0);
                var p2 = new Point3d(pts[2].X, pts[2].Y, 0.0);
                var p3 = pts.Count > 3
                    ? new Point3d(pts[3].X, pts[3].Y, 0.0)
                    : p2;
                ent = new Solid(p0, p1, p2, p3);
            }
            else if (string.Equals(t, "INSERT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(e.Name)) return false;
                ObjectId bid = ObjectId.Null;
                if (SheetSnap.IsNorthBlockName(e.Name))
                    bid = EnsureNorthBlock(db, tr);
                if (bid.IsNull)
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(e.Name)) bid = bt[e.Name];
                    else
                    {
                        string baseName = SheetSnap.BlockBaseName(e.Name);
                        if (!string.IsNullOrEmpty(baseName) && bt.Has(baseName))
                            bid = bt[baseName];
                    }
                }
                if (bid.IsNull)
                {
                    NvkLog.Write("[НВК] снимок нет блока " + e.Name);
                    return false;
                }
                var br = new BlockReference(new Point3d(e.X, e.Y, 0.0), bid);
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
            if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
            {
                try { ent.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, 0.0))); }
                catch { }
            }
            if (!string.IsNullOrEmpty(e.Tpl))
                NvkTag.Put1000(tr, ent, NvkTag.TplKey, e.Tpl);
            return true;
        }

        static ObjectId EnsureNorthBlock(Database db, Transaction tr)
        {
            ObjectId id = FindNorthBlock(db, tr);
            if (id.IsNull)
            {
                if (!ImportNorthDwg(db)) return ObjectId.Null;
                id = FindNorthBlock(db, tr);
            }
            if (!id.IsNull)
            {
                try
                {
                    var rec = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    NvkLog.Write("[НВК] стрелка севера блок " + (rec != null ? rec.Name : "?"));
                }
                catch { }
            }
            return id;
        }

        static ObjectId FindNorthBlock(Database db, Transaction tr)
        {
            if (db == null || tr == null) return ObjectId.Null;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            string[] prefer = { "Arrow3", "Arrow2", "Arrow1", "ArrowN", "North" };
            for (int i = 0; i < prefer.Length; i++)
            {
                if (bt.Has(prefer[i])) return bt[prefer[i]];
            }
            foreach (ObjectId bid in bt)
            {
                var rec = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                if (rec == null || rec.IsLayout || rec.IsAnonymous) continue;
                if (SheetSnap.IsNorthBlockName(rec.Name)) return bid;
            }
            return ObjectId.Null;
        }

        static bool ImportNorthDwg(Database dest)
        {
            if (dest == null) return false;
            foreach (string path in NorthDwgPaths())
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                try
                {
                    using (var src = new Database(false, true))
                    {
                        src.ReadDwgFile(path, FileShare.ReadWrite, true, "");
                        src.CloseInput(true);
                        var ids = new ObjectIdCollection();
                        using (var str = src.TransactionManager.StartTransaction())
                        {
                            CollectNorthIds(str, src, ids);
                            str.Commit();
                        }
                        if (ids.Count == 0)
                        {
                            NvkLog.Write("[НВК] стрелка севера пусто " + path);
                            continue;
                        }
                        var map = new IdMapping();
                        dest.WblockCloneObjects(ids, dest.BlockTableId, map,
                            DuplicateRecordCloning.Ignore, false);
                        NvkLog.Write("[НВК] стрелка севера из " + Path.GetFileName(path)
                            + " блоков " + ids.Count.ToString());
                        return true;
                    }
                }
                catch (Exc ex)
                {
                    NvkLog.Write("[НВК] стрелка севера " + path + ": " + ex.Message);
                }
            }
            NvkLog.Write("[НВК] стрелка севера: нет Arrow3");
            return false;
        }

        static void CollectNorthIds(Transaction tr, Database src, ObjectIdCollection ids)
        {
            if (tr == null || src == null || ids == null) return;
            var bt = (BlockTable)tr.GetObject(src.BlockTableId, OpenMode.ForRead);
            ObjectId pick = ObjectId.Null;
            if (bt.Has("Arrow3")) pick = bt["Arrow3"];
            if (pick.IsNull)
            {
                string[] prefer = { "Arrow2", "Arrow1", "ArrowN", "North" };
                for (int i = 0; i < prefer.Length; i++)
                {
                    if (bt.Has(prefer[i])) { pick = bt[prefer[i]]; break; }
                }
            }
            if (pick.IsNull)
            {
                foreach (ObjectId bid in bt)
                {
                    var rec = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                    if (rec == null || rec.IsLayout || rec.IsAnonymous) continue;
                    if (SheetSnap.IsNorthBlockName(rec.Name))
                    {
                        pick = bid;
                        break;
                    }
                }
            }
            if (pick.IsNull)
                pick = NorthFromSpace(tr, bt, BlockTableRecord.ModelSpace);
            if (pick.IsNull)
                pick = NorthFromSpace(tr, bt, BlockTableRecord.PaperSpace);
            if (!pick.IsNull) ids.Add(pick);
        }

        static ObjectId NorthFromSpace(Transaction tr, BlockTable bt, string space)
        {
            if (tr == null || bt == null || string.IsNullOrEmpty(space) || !bt.Has(space))
                return ObjectId.Null;
            var rec = (BlockTableRecord)tr.GetObject(bt[space], OpenMode.ForRead);
            foreach (ObjectId id in rec)
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null) continue;
                string bn = null;
                try { bn = br.Name; } catch { }
                if (string.IsNullOrEmpty(bn))
                {
                    try
                    {
                        var def = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        bn = def.Name;
                    }
                    catch { }
                }
                if (SheetSnap.IsNorthBlockName(bn))
                    return br.BlockTableRecord;
            }
            return ObjectId.Null;
        }

        static IEnumerable<string> NorthDwgPaths()
        {
            string root = NvkSession.RootDir;
            if (!string.IsNullOrEmpty(root))
                yield return Path.Combine(root, "presets", "sheets", "north.dwg");
            yield return @"E:\Programs\ACAD2025\AutoCAD 2025\Map\Sample\Symbols\Map - North Arrows.dwg";
        }

        public static int PlaceNorth(Database db, Transaction tr, BlockTableRecord ps,
            ProfileKind kind, double sitTwist, double planTwist)
        {
            if (db == null || tr == null || ps == null) return 0;
            var slots = SheetSnap.NorthSlots(LayoutOf(kind));
            if (slots == null || slots.Count == 0) return 0;
            double dx, dy, fw, fh;
            SheetSnap.ShiftOf(Load(), kind.ToString(), out dx, out dy, out fw, out fh);
            ObjectId northId = EnsureNorthBlock(db, tr);
            if (northId.IsNull) return 0;
            EraseNorthDebris(tr, ps);
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                double twist = slots[i].Sit ? sitTwist : planTwist;
                if (PutNorthAt(tr, ps, slots[i].X + dx, slots[i].Y + dy, twist, northId, slots[i].Sit))
                    n++;
            }
            return n;
        }

        static bool PutNorthAt(Transaction tr, BlockTableRecord ps,
            double x, double y, double twistRad, ObjectId northId, bool sit)
        {
            if (tr == null || ps == null || northId.IsNull) return false;
            EraseJunkArrow(tr, ps, x, y);
            var twin = FindNorthTwin(tr, ps, x, y);
            if (twin != null)
            {
                if (!twin.IsWriteEnabled) twin.UpgradeOpen();
                if (twin.BlockTableRecord != northId)
                {
                    twin.Erase();
                    twin = null;
                }
                else
                {
                    twin.Rotation = twistRad;
                    NvkLog.Write("[НВК] стрелка севера "
                        + (sit ? "SIT" : "PLAN")
                        + " поворот " + (twistRad * 180.0 / Math.PI).ToString("0.##"));
                    return true;
                }
            }
            var br = new BlockReference(new Point3d(x, y, 0.0), northId);
            try { br.ScaleFactors = new Scale3d(SheetSnap.NorthScale, SheetSnap.NorthScale, 1.0); }
            catch { }
            br.Rotation = twistRad;
            br.SetDatabaseDefaults();
            ps.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            NvkLog.Write("[НВК] стрелка севера Arrow3 "
                + (sit ? "SIT" : "PLAN")
                + " @ " + x.ToString("0.0") + "," + y.ToString("0.0")
                + " поворот " + (twistRad * 180.0 / Math.PI).ToString("0.##"));
            return true;
        }

        static void EraseJunkArrow(Transaction tr, BlockTableRecord ps, double x, double y)
        {
            if (tr == null || ps == null) return;
            var kill = new List<ObjectId>();
            foreach (ObjectId id in ps)
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null || br.IsErased) continue;
                if (Dist2(br.Position.X, br.Position.Y, x, y) > 36.0) continue;
                string bn = BlockName(tr, br);
                if (string.IsNullOrEmpty(bn)) continue;
                if (bn.IndexOf("HEAD", StringComparison.OrdinalIgnoreCase) >= 0)
                    kill.Add(id);
            }
            for (int i = 0; i < kill.Count; i++)
            {
                try
                {
                    var ent = tr.GetObject(kill[i], OpenMode.ForWrite) as Entity;
                    if (ent != null && !ent.IsErased) ent.Erase();
                }
                catch { }
            }
            if (kill.Count > 0)
                NvkLog.Write("[НВК] стрелка севера снята HEAD " + kill.Count.ToString());
        }

        static BlockReference FindNorthTwin(Transaction tr, BlockTableRecord ps, double x, double y)
        {
            if (tr == null || ps == null) return null;
            foreach (ObjectId id in ps)
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null || br.IsErased) continue;
                if (Dist2(br.Position.X, br.Position.Y, x, y) > 36.0) continue;
                string bn = BlockName(tr, br);
                if (SheetSnap.IsNorthBlockName(bn)) return br;
            }
            return null;
        }

        static int EraseNorthDebris(Transaction tr, BlockTableRecord ps)
        {
            if (tr == null || ps == null) return 0;
            var letters = new List<Point2d>();
            var kill = new List<ObjectId>();
            foreach (ObjectId id in ps)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased || ent is Viewport) continue;
                double x, y;
                if (LiveNorthLetter(tr, ent, out x, out y))
                {
                    letters.Add(new Point2d(x, y));
                    kill.Add(id);
                }
            }
            if (letters.Count == 0) return 0;
            foreach (ObjectId id in ps)
            {
                if (kill.Contains(id)) continue;
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null || ent.IsErased || ent is Viewport || ent is BlockReference)
                    continue;
                if (!LiveNorthGeom(ent)) continue;
                Point3d p;
                try { p = ent.GeometricExtents.MinPoint; }
                catch { continue; }
                for (int i = 0; i < letters.Count; i++)
                {
                    if (Dist2(p.X, p.Y, letters[i].X, letters[i].Y) < 256.0)
                    {
                        kill.Add(id);
                        break;
                    }
                }
            }
            int n = 0;
            for (int i = 0; i < kill.Count; i++)
            {
                try
                {
                    var ent = tr.GetObject(kill[i], OpenMode.ForWrite) as Entity;
                    if (ent == null || ent.IsErased) continue;
                    ent.Erase();
                    n++;
                }
                catch { }
            }
            if (n > 0)
                NvkLog.Write("[НВК] снимок снята разобранная стрелка " + n.ToString());
            return n;
        }

        static bool LiveNorthLetter(Transaction tr, Entity ent, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            var dt = ent as DBText;
            if (dt == null) return false;
            if (!string.Equals(dt.TextString, "N", StringComparison.Ordinal)) return false;
            string st = "";
            try
            {
                var rec = (TextStyleTableRecord)tr.GetObject(dt.TextStyleId, OpenMode.ForRead);
                if (rec != null) st = rec.Name ?? "";
            }
            catch { }
            if (st.IndexOf("North", StringComparison.OrdinalIgnoreCase) < 0) return false;
            x = dt.Position.X;
            y = dt.Position.Y;
            return true;
        }

        static bool LiveNorthGeom(Entity ent)
        {
            if (ent == null) return false;
            try
            {
                if (!string.Equals(ent.Layer, "0", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.Equals(ent.Linetype, "ByBlock", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { return false; }
            return ent is Polyline || ent is Solid || ent is Line
                || ent is Arc || ent is Circle || ent is DBText;
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
            try
            {
                Extents2d w = layout.PlotWindowArea;
                p.Wx0 = w.MinPoint.X;
                p.Wy0 = w.MinPoint.Y;
                p.Wx1 = w.MaxPoint.X;
                p.Wy1 = w.MaxPoint.Y;
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
                else
                {
                    string alt = PickA3Media(psv, layout, page.PaperW, page.PaperH);
                    if (!string.IsNullOrEmpty(alt))
                    {
                        try { psv.SetCanonicalMediaName(layout, alt); } catch { }
                    }
                }
                PlotPaperUnit units;
                if (TryEnum(page.Units, PlotPaperUnit.Millimeters, out units))
                    try { psv.SetPlotPaperUnits(layout, units); } catch { }
                bool asLayout = string.Equals(page.PlotType, "Layout", StringComparison.OrdinalIgnoreCase);
                try { psv.SetZoomToPaperOnUpdate(layout, asLayout); } catch { }
                double wx0 = page.Wx0, wy0 = page.Wy0, wx1 = page.Wx1, wy1 = page.Wy1;
                if (!SheetSnap.HasWindow(page))
                {
                    ProfileKind kind;
                    if (NvkSheets.TryKind(name, out kind))
                    {
                        var snap = LayoutOf(kind);
                        SheetSnap.FillWindow(page, snap);
                        if (SheetSnap.HasWindow(page))
                        {
                            wx0 = page.Wx0; wy0 = page.Wy0;
                            wx1 = page.Wx1; wy1 = page.Wy1;
                        }
                    }
                }
                bool win = SheetSnap.HasWindow(page)
                    && string.Equals(page.PlotType, "Window", StringComparison.OrdinalIgnoreCase);
                Extents2d winExt = win
                    ? new Extents2d(wx0, wy0, wx1, wy1)
                    : new Extents2d(0, 0, 1, 1);
                if (win)
                {
                    try { psv.SetPlotWindowArea(layout, winExt); } catch { }
                }
                PlotType ptype;
                if (TryEnum(page.PlotType, PlotType.Layout, out ptype))
                    try { psv.SetPlotType(layout, ptype); } catch { }
                if (win)
                {
                    try { psv.SetPlotWindowArea(layout, winExt); } catch { }
                }
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
                    + "  " + (layout.CanonicalMediaName ?? media)
                    + (win
                        ? "  окно " + wx0.ToString("0.0", Inv) + "," + wy0.ToString("0.0", Inv)
                            + ".." + wx1.ToString("0.0", Inv) + "," + wy1.ToString("0.0", Inv)
                        : ""));
                if (win) ZoomToWindow(layout, wx0, wy0, wx1, wy1);
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
            var file = Load();
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
                    SheetSnapPage one = page;
                    ProfileKind kind;
                    double dx = 0.0, dy = 0.0, fw = 420.0, fh = 297.0;
                    if (NvkSheets.TryKind(e.Key, out kind))
                    {
                        one = PageForApply(kind, out dx, out dy, out fw, out fh);
                    }
                    else
                    {
                        one = SheetSnap.CopyPage(page);
                        SheetSnap.FitPage(one, fw, fh);
                    }
                    if (ApplyPage(layout, one, e.Key)) nOk++;
                    else nFail++;
                    if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
                        ShiftLivePaper(tr, layout, dx, dy);
                }
                tr.Commit();
            }
            string msg = "[НВК] параметры листа всем: ок " + nOk.ToString();
            if (nFail > 0) msg += "  сбой " + nFail.ToString();
            msg += "  " + (page.Device ?? "") + "  " + (page.Media ?? "");
            NvkLog.Write(msg);
            return msg;
        }

        public static int ShiftLivePaper(Transaction tr, Layout layout, double dx, double dy)
        {
            if (tr == null || layout == null) return 0;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return 0;
            try
            {
                var ps = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (ps == null) return 0;
                double minX = 0.0;
                bool any = false;
                foreach (ObjectId id in ps)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;
                    var vp = ent as Viewport;
                    if (vp != null)
                    {
                        int num = 0;
                        try { num = vp.Number; } catch { }
                        if (num == 1) continue;
                        AccMin(vp.CenterPoint.X - vp.Width * 0.5, ref any, ref minX);
                        continue;
                    }
                    if (SheetSnap.IsFrameLayer(ent.Layer))
                    {
                        try
                        {
                            Extents3d ext = ent.GeometricExtents;
                            AccMin(ext.MinPoint.X, ref any, ref minX);
                        }
                        catch { }
                        continue;
                    }
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        if (SheetSnap.IsPaperCoord(ext.MinPoint.X, ext.MinPoint.Y))
                            AccMin(ext.MinPoint.X, ref any, ref minX);
                    }
                    catch { }
                }
                if (any && minX < 80.0) return 0;
                var move = Matrix3d.Displacement(new Vector3d(dx, dy, 0.0));
                int n = 0;
                foreach (ObjectId id in ps)
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent == null || ent.IsErased) continue;
                    var vp = ent as Viewport;
                    if (vp != null)
                    {
                        int num = 0;
                        try { num = vp.Number; } catch { }
                        if (num == 1) continue;
                    }
                    try
                    {
                        ent.TransformBy(move);
                        n++;
                    }
                    catch { }
                }
                if (n > 0)
                    NvkLog.Write("[НВК] лист " + (layout.LayoutName ?? "")
                        + " сдвиг бумаги " + dx.ToString("0.0", Inv) + "," + dy.ToString("0.0", Inv)
                        + "  объектов " + n.ToString());
                return n;
            }
            catch (Exc ex)
            {
                NvkLog.Write("[НВК] сдвиг бумаги: " + ex.Message);
                return 0;
            }
        }

        static void AccMin(double x, ref bool any, ref double minX)
        {
            if (!any) { minX = x; any = true; return; }
            if (x < minX) minX = x;
        }

        static void ZoomToWindow(Layout layout, double x0, double y0, double x1, double y1)
        {
            if (layout == null || x1 - x0 < 10.0 || y1 - y0 < 10.0) return;
            try
            {
                var tr = layout.Database.TransactionManager.TopTransaction;
                if (tr == null) return;
                var ps = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                if (ps == null) return;
                double cx = (x0 + x1) * 0.5;
                double cy = (y0 + y1) * 0.5;
                double h = y1 - y0;
                if (h < 10.0) h = 297.0;
                h *= 1.12;
                foreach (ObjectId id in ps)
                {
                    var vp = tr.GetObject(id, OpenMode.ForWrite) as Viewport;
                    if (vp == null || vp.IsErased) continue;
                    int n = 0;
                    try { n = vp.Number; } catch { continue; }
                    if (n != 1) continue;
                    try { vp.ViewCenter = new Point2d(cx, cy); } catch { }
                    try { vp.ViewHeight = h; } catch { }
                    break;
                }
            }
            catch { }
        }

        static string PickA3Media(PlotSettingsValidator psv, Layout layout, double fw, double fh)
        {
            System.Collections.Specialized.StringCollection names = null;
            try { names = psv.GetCanonicalMediaNameList(layout); } catch { }
            if (names == null) return null;
            bool land = fw + 1.0 >= fh;
            string best = null;
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                string u = n.ToUpperInvariant();
                if (u.IndexOf("A3", StringComparison.Ordinal) < 0) continue;
                int i420 = u.IndexOf("420", StringComparison.Ordinal);
                int i297 = u.IndexOf("297", StringComparison.Ordinal);
                if (i420 < 0 || i297 < 0) continue;
                bool nLand = i420 < i297;
                if (land == nLand)
                {
                    if (u.IndexOf("FULL_BLEED", StringComparison.Ordinal) >= 0) return n;
                    if (best == null) best = n;
                }
                else if (best == null) best = n;
            }
            return best;
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
