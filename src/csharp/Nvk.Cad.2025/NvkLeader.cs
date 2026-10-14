using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Мультивыноска: стрелка в arrow, текст у text. Без отката в голый MTEXT.</summary>
    static class NvkLeader
    {
        static int _ok, _fail;
        static string _lastErr;

        public static MLeader Make(BlockTableRecord btr, Transaction tr, string layer,
            double arrowX, double arrowY, double textX, double textY, string contents)
        {
            return Make(btr, tr, layer, arrowX, arrowY, textX, textY, contents, 0.0);
        }

        public static MLeader Make(BlockTableRecord btr, Transaction tr, string layer,
            double arrowX, double arrowY, double textX, double textY, string contents, double rot)
        {
            if (btr == null || string.IsNullOrEmpty(contents)) return null;
            var db = btr.Database;
            var arrow = new Point3d(arrowX, arrowY, 0.0);
            var text = new Point3d(textX, textY, 0.0);
            if (arrow.DistanceTo(text) < 1e-3)
                text = new Point3d(textX + 5.0, textY, 0.0);

            var ml = TryManaged(btr, tr, db, layer, arrow, text, contents, rot);
            if (ml != null) { _ok++; return ml; }

            if (TryCom(layer, arrow, text, contents, rot)) { _ok++; return null; }

            if (TryOldLeader(btr, tr, layer, arrow, text, contents, rot))
            {
                _ok++;
                return null;
            }
            _fail++;
            return null;
        }

        public static Dictionary<string, LeaderPose> Collect(Database db, Transaction tr, string layer)
        {
            var map = new Dictionary<string, LeaderPose>(StringComparer.OrdinalIgnoreCase);
            if (db == null || tr == null) return map;
            try
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (!string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var ml = ent as MLeader;
                    if (ml != null)
                    {
                        Add(map, FromMLeader(ml));
                        continue;
                    }
                    var mt = ent as MText;
                    if (mt != null)
                    {
                        Add(map, FromMText(mt));
                        continue;
                    }
                    var ld = ent as Leader;
                    if (ld != null)
                        Add(map, FromOldLeader(ld, tr));
                }
            }
            catch (Exc) { }
            return map;
        }

        static void Add(Dictionary<string, LeaderPose> map, LeaderPose p)
        {
            if (p == null || string.IsNullOrEmpty(p.Key)) return;
            map[p.Key] = p;
        }

        static LeaderPose FromMLeader(MLeader ml)
        {
            try
            {
                string key = null;
                double rot = 0.0, x = 0.0, y = 0.0;
                var mt = ml.MText;
                if (mt != null)
                {
                    key = Plain(mt.Contents);
                    rot = mt.Rotation;
                    x = mt.Location.X;
                    y = mt.Location.Y;
                }
                try
                {
                    var tl = ml.TextLocation;
                    x = tl.X;
                    y = tl.Y;
                }
                catch (Exc) { }
                if (string.IsNullOrEmpty(key)) return null;
                var pose = new LeaderPose { Key = key, X = x, Y = y, Rot = rot };
                double ax, ay;
                if (TryArrow(ml, x, y, out ax, out ay))
                {
                    pose.Ax = ax;
                    pose.Ay = ay;
                    pose.HasArrow = true;
                }
                return pose;
            }
            catch (Exc) { return null; }
        }

        static bool TryArrow(MLeader ml, double tx, double ty, out double ax, out double ay)
        {
            ax = 0.0;
            ay = 0.0;
            var text = new Point3d(tx, ty, 0.0);
            Point3d? best = null;
            double bestDist = -1.0;
            foreach (int line in LeaderLines(ml))
            {
                Point3d? a = null, b = null;
                try { a = ml.GetFirstVertex(line); } catch (Exc) { }
                try { b = ml.GetLastVertex(line); } catch (Exc) { }
                Consider(a, text, ref best, ref bestDist);
                Consider(b, text, ref best, ref bestDist);
                try
                {
                    var v0 = ml.GetVertex(line, 0);
                    Consider(v0, text, ref best, ref bestDist);
                }
                catch (Exc) { }
            }
            if (best == null) return false;
            ax = best.Value.X;
            ay = best.Value.Y;
            return true;
        }

        static void Consider(Point3d? p, Point3d text, ref Point3d? best, ref double bestDist)
        {
            if (p == null) return;
            double d = p.Value.DistanceTo(text);
            if (best == null || d > bestDist)
            {
                best = p;
                bestDist = d;
            }
        }

        static List<int> LeaderLines(MLeader ml)
        {
            var lines = new List<int>();
            try
            {
                foreach (int li in Ints(ml.GetLeaderIndexes()))
                {
                    bool any = false;
                    try
                    {
                        foreach (int line in Ints(ml.GetLeaderLineIndexes(li)))
                        {
                            lines.Add(line);
                            any = true;
                        }
                    }
                    catch (Exc) { }
                    if (!any) lines.Add(li);
                }
            }
            catch (Exc) { }
            if (lines.Count == 0)
            {
                for (int i = 0; i < 8; i++)
                    lines.Add(i);
            }
            return lines;
        }

        static List<int> Ints(object col)
        {
            var dst = new List<int>();
            if (col == null) return dst;
            var en = col as System.Collections.IEnumerable;
            if (en == null)
            {
                try { dst.Add(Convert.ToInt32(col)); } catch (Exc) { }
                return dst;
            }
            foreach (object o in en)
            {
                if (o == null || o is string) continue;
                try { dst.Add(Convert.ToInt32(o)); } catch (Exc) { }
            }
            return dst;
        }

        static LeaderPose FromMText(MText mt)
        {
            try
            {
                string key = Plain(mt.Contents);
                if (string.IsNullOrEmpty(key)) return null;
                return new LeaderPose { Key = key, X = mt.Location.X, Y = mt.Location.Y, Rot = mt.Rotation };
            }
            catch (Exc) { return null; }
        }

        static LeaderPose FromOldLeader(Leader ld, Transaction tr)
        {
            try
            {
                string key = null;
                double rot = 0.0, x = 0.0, y = 0.0;
                if (tr != null && !ld.Annotation.IsNull)
                {
                    var mt = tr.GetObject(ld.Annotation, OpenMode.ForRead) as MText;
                    if (mt != null)
                    {
                        key = Plain(mt.Contents);
                        rot = mt.Rotation;
                        x = mt.Location.X;
                        y = mt.Location.Y;
                    }
                }
                if (string.IsNullOrEmpty(key) || ld.NumVertices < 1) return null;
                var a = ld.VertexAt(0);
                return new LeaderPose
                {
                    Key = key, X = x, Y = y, Rot = rot,
                    Ax = a.X, Ay = a.Y, HasArrow = true
                };
            }
            catch (Exc) { return null; }
        }

        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\\P", " ").Replace("\\p", " ");
            var sb = new System.Text.StringBuilder();
            bool skip = false;
            foreach (char c in s)
            {
                if (c == '{') { skip = true; continue; }
                if (c == '}') { skip = false; continue; }
                if (skip && c == ';') { skip = false; continue; }
                if (!skip) sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static void LogStyle(Database db, Transaction tr)
        {
            _ok = 0;
            _fail = 0;
            _lastErr = null;
            string name = "";
            bool anno = false;
            if (db != null && tr != null && !db.MLeaderstyle.IsNull)
            {
                try
                {
                    var st = tr.GetObject(db.MLeaderstyle, OpenMode.ForRead) as MLeaderStyle;
                    if (st != null)
                    {
                        name = st.Name ?? "";
                        anno = st.Annotative == AnnotativeStates.True;
                    }
                }
                catch (Exc) { }
            }
            NvkLog.Write("[НВК] MLEADERSTYLE  имя=" + (string.IsNullOrEmpty(name) ? "(текущий)" : name)
                + "  анно=" + (anno ? "да" : "нет"));
        }

        public static void LogCounts()
        {
            NvkLog.Write("[НВК] MLEADER  ок=" + _ok.ToString()
                + "  сбой=" + _fail.ToString()
                + (string.IsNullOrEmpty(_lastErr) ? "" : "  err=" + _lastErr));
        }

        static MLeader TryManaged(BlockTableRecord btr, Transaction tr, Database db,
            string layer, Point3d arrow, Point3d text, string contents, double rot)
        {
            try
            {
                var ml = new MLeader();
                if (db != null) ml.SetDatabaseDefaults(db);
                else ml.SetDatabaseDefaults();
                try
                {
                    if (db != null && !db.MLeaderstyle.IsNull)
                        ml.MLeaderStyle = db.MLeaderstyle;
                }
                catch (Exc) { }
                ml.ContentType = ContentType.MTextContent;

                double th = 2.5, asz = 1.25;
                ObjectId txtSt = ObjectId.Null;
                ReadStyle(db, tr, ref th, ref asz, ref txtSt);
                if (th < 0.05) th = 2.5;
                if (asz < 0.01) asz = th * 0.5;

                var mt = new MText();
                if (db != null) mt.SetDatabaseDefaults(db);
                else mt.SetDatabaseDefaults();
                mt.Contents = contents;
                mt.Location = text;
                mt.TextHeight = th;
                mt.Attachment = AttachmentPoint.MiddleLeft;
                mt.Rotation = rot;
                if (!txtSt.IsNull)
                {
                    try { mt.TextStyleId = txtSt; } catch (Exc) { }
                }
                else if (db != null)
                {
                    try { mt.TextStyleId = db.Textstyle; } catch (Exc) { }
                }
                try
                {
                    mt.BackgroundFill = true;
                    mt.UseBackgroundColor = true;
                    mt.BackgroundScaleFactor = 1.1;
                }
                catch (Exc) { }
                ml.MText = mt;
                try { ml.ArrowSize = asz; } catch (Exc) { }
                try { ml.EnableLanding = true; } catch (Exc) { }
                try { ml.EnableDogleg = true; } catch (Exc) { }
                try { ml.DoglegLength = Math.Max(th, asz); } catch (Exc) { }
                try { ml.LandingGap = 0.5 * th; } catch (Exc) { }
                try { ml.LeaderLineType = LeaderType.StraightLeader; } catch (Exc) { }

                int line = AddLine(ml);
                ml.AddFirstVertex(line, arrow);
                ml.AddLastVertex(line, text);
                try { ml.TextLocation = text; } catch (Exc) { }

                if (!string.IsNullOrEmpty(layer)) ml.Layer = layer;
                btr.AppendEntity(ml);
                tr.AddNewlyCreatedDBObject(ml, true);
                try
                {
                    var mt2 = ml.MText;
                    if (mt2 != null)
                    {
                        mt2.Rotation = rot;
                        mt2.Location = text;
                        ml.MText = mt2;
                    }
                    ml.TextLocation = text;
                }
                catch (Exc) { }
                ApplyAnno(ml, db, tr);
                NvkTag.Stamp(tr, ml);
                return ml;
            }
            catch (Exc ex)
            {
                _lastErr = "managed: " + ex.Message;
                return null;
            }
        }

        static int AddLine(MLeader ml)
        {
            try { return ml.AddLeaderLine(0); }
            catch (Exc)
            {
                int li = ml.AddLeader();
                try { return ml.AddLeaderLine(li); }
                catch (Exc)
                {
                    return li;
                }
            }
        }

        static bool TryCom(string layer, Point3d arrow, Point3d text, string contents, double rot)
        {
            try
            {
                dynamic app = AcadApp.AcadApplication;
                if (app == null) return false;
                dynamic ms = app.ActiveDocument.ModelSpace;
                double[] pts = { text.X, text.Y, 0.0, arrow.X, arrow.Y, 0.0 };
                dynamic vla = ms.AddMLeader(pts, 0);
                vla.TextString = contents;
                if (!string.IsNullOrEmpty(layer)) vla.Layer = layer;
                try { vla.TextBackgroundFill = true; } catch (Exc) { }
                try { vla.TextRotation = rot; } catch (Exc) { }
                return true;
            }
            catch (Exc ex)
            {
                _lastErr = "com: " + ex.Message;
                return false;
            }
        }

        static bool TryOldLeader(BlockTableRecord btr, Transaction tr, string layer,
            Point3d arrow, Point3d text, string contents, double rot)
        {
            try
            {
                var ld = new Leader();
                ld.SetDatabaseDefaults();
                if (!string.IsNullOrEmpty(layer)) ld.Layer = layer;
                ld.HasArrowHead = true;
                ld.AppendVertex(arrow);
                ld.AppendVertex(text);
                btr.AppendEntity(ld);
                tr.AddNewlyCreatedDBObject(ld, true);
                NvkTag.Stamp(tr, ld);
                NvkText.Make(btr, tr, layer, text.X, text.Y, contents, 2.5, AttachmentPoint.MiddleLeft, rot);
                _lastErr = (_lastErr ?? "") + " → LEADER";
                return true;
            }
            catch (Exc ex)
            {
                _lastErr = "leader: " + ex.Message;
                return false;
            }
        }

        static void ReadStyle(Database db, Transaction tr, ref double th, ref double asz, ref ObjectId txtSt)
        {
            if (db == null || tr == null || db.MLeaderstyle.IsNull) return;
            try
            {
                var st = tr.GetObject(db.MLeaderstyle, OpenMode.ForRead) as MLeaderStyle;
                if (st == null) return;
                if (st.TextHeight > 1e-6) th = st.TextHeight;
                if (st.ArrowSize > 1e-6) asz = st.ArrowSize;
                if (!st.TextStyleId.IsNull) txtSt = st.TextStyleId;
            }
            catch (Exc) { }
        }

        static void ApplyAnno(Entity e, Database db, Transaction tr)
        {
            if (db == null || tr == null || db.MLeaderstyle.IsNull) return;
            try
            {
                var st = tr.GetObject(db.MLeaderstyle, OpenMode.ForRead) as MLeaderStyle;
                if (st != null && st.Annotative == AnnotativeStates.True)
                    NvkAnno.Apply(e);
            }
            catch (Exc) { }
        }
    }
}