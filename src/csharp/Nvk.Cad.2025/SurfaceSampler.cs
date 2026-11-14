using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class SurfaceSampler
    {
        public static List<SurfInfo> ListAll(Database db, Transaction tr)
        {
            var result = new List<SurfInfo>();
            ObjectIdCollection ids = null;
            try
            {
                var civil = CivilApplication.ActiveDocument;
                if (civil != null) ids = civil.GetSurfaceIds();
            }
            catch (Exc) { }
            if (ids == null) return result;
            foreach (ObjectId id in ids)
            {
                try
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    var name = Safe.PropString(obj, "Name");
                    if (name != null)
                        result.Add(new SurfInfo { Id = id, Handle = id.Handle.ToString(), Name = name });
                }
                catch (Exc) { }
            }
            return result;
        }

        /// <summary>
        /// Треугольники TIN в прямоугольнике плана. Для бровки по рёбрам перелома.
        /// Пустой список — нет поверхности или API не отдал сетку.
        /// </summary>
        public static List<TinTri> TrianglesInBox(Transaction tr, ObjectId surfId,
            double minX, double minY, double maxX, double maxY)
        {
            var list = new List<TinTri>();
            if (surfId.IsNull) return list;
            try
            {
                var surf = tr.GetObject(surfId, OpenMode.ForRead) as TinSurface;
                if (surf == null) return list;
                var tris = surf.GetTriangles(false);
                if (tris == null) return list;
                foreach (TinSurfaceTriangle t in tris)
                {
                    var a = t.Vertex1.Location;
                    var b = t.Vertex2.Location;
                    var c = t.Vertex3.Location;
                    double tminx = a.X; if (b.X < tminx) tminx = b.X; if (c.X < tminx) tminx = c.X;
                    double tmaxx = a.X; if (b.X > tmaxx) tmaxx = b.X; if (c.X > tmaxx) tmaxx = c.X;
                    double tminy = a.Y; if (b.Y < tminy) tminy = b.Y; if (c.Y < tminy) tminy = c.Y;
                    double tmaxy = a.Y; if (b.Y > tmaxy) tmaxy = b.Y; if (c.Y > tmaxy) tmaxy = c.Y;
                    if (tmaxx < minX || tminx > maxX || tmaxy < minY || tminy > maxY) continue;
                    list.Add(new TinTri(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z));
                }
            }
            catch (Exc) { }
            return list;
        }

        /// <summary>Z в точке плана. Civil FindElevationAtXY, без выгрузки треугольников.</summary>
        public static double? ElevationAt(Transaction tr, ObjectId surfId, double x, double y)
        {
            if (surfId.IsNull) return null;
            try
            {
                var surf = tr.GetObject(surfId, OpenMode.ForRead) as TinSurface;
                if (surf == null)
                {
                    var any = tr.GetObject(surfId, OpenMode.ForRead);
                    var z = InvokeElev(any, x, y);
                    return z;
                }
                return surf.FindElevationAtXY(x, y);
            }
            catch (Exc)
            {
                return null;
            }
        }

        static double? InvokeElev(object surf, double x, double y)
        {
            try
            {
                var mi = surf.GetType().GetMethod("FindElevationAtXY", new[] { typeof(double), typeof(double) });
                if (mi == null) return null;
                var z = mi.Invoke(surf, new object[] { x, y });
                if (z == null) return null;
                return Convert.ToDouble(z);
            }
            catch
            {
                return null;
            }
        }

        public static ObjectId ByHandle(Database db, string handle)
        {
            if (string.IsNullOrWhiteSpace(handle)) return ObjectId.Null;
            try
            {
                var h = new Handle(Convert.ToInt64(handle, 16));
                ObjectId id;
                if (db.TryGetObjectId(h, out id)) return id;
            }
            catch (Exc) { }
            return ObjectId.Null;
        }
    }

    struct SurfInfo
    {
        public ObjectId Id;
        public string Handle;
        public string Name;
    }
}
