using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Типы линий и штриховки, которые реально есть в чертеже / acad.lin / acad.pat.</summary>
    static class DrawAssets
    {
        public static List<string> Linetypes()
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("Continuous");
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var lt = (LinetypeTable)tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);
                        foreach (ObjectId id in lt)
                        {
                            var rec = (LinetypeTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            if (rec != null && !string.IsNullOrEmpty(rec.Name) && rec.Name[0] != '*')
                                set.Add(rec.Name);
                        }
                        tr.Commit();
                    }
                }
                catch (Exc) { }
            }
            ParseStarNames(FindSupport("acad.lin"), set);
            ParseStarNames(FindSupport("acadiso.lin"), set);
            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static List<string> Hatches()
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("SOLID");
            ParseStarNames(FindSupport("acad.pat"), set);
            ParseStarNames(FindSupport("acadiso.pat"), set);
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                try
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        foreach (ObjectId id in ms)
                        {
                            var h = tr.GetObject(id, OpenMode.ForRead) as Hatch;
                            if (h != null && !string.IsNullOrEmpty(h.PatternName))
                                set.Add(h.PatternName);
                        }
                        tr.Commit();
                    }
                }
                catch (Exc) { }
            }
            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        static void ParseStarNames(string path, SortedSet<string> set)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.Length < 2 || line[0] != '*') continue;
                    int c = line.IndexOf(',');
                    string n = (c > 1 ? line.Substring(1, c - 1) : line.Substring(1)).Trim();
                    if (n.Length > 0) set.Add(n);
                }
            }
            catch (Exc) { }
        }

        static string FindSupport(string file)
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    string p = HostApplicationServices.Current.FindFile(file, doc.Database, FindFileHint.Default);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                }
            }
            catch (Exc) { }
            try
            {
                string root = AcadApp.GetSystemVariable("ACADPREFIX") as string;
                if (!string.IsNullOrEmpty(root))
                {
                    foreach (var dir in root.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string p = Path.Combine(dir.Trim(), file);
                        if (File.Exists(p)) return p;
                    }
                }
            }
            catch (Exc) { }
            return null;
        }
    }
}
