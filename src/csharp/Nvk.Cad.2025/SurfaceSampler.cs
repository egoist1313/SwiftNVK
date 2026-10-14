using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
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
