using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class ProfileSampler
    {
        /// <summary>Список всех Profile-объектов во всех трассах документа.</summary>
        public static List<ProfileInfo> ListAll(Database db, Transaction tr)
        {
            var result = new List<ProfileInfo>();
            ObjectIdCollection alignIds = null;
            try
            {
                var civil = CivilApplication.ActiveDocument;
                if (civil != null) alignIds = civil.GetAlignmentIds();
            }
            catch (Exc) { }
            if (alignIds == null) return result;

            foreach (ObjectId alignId in alignIds)
            {
                string alignName = "";
                ObjectIdCollection profileIds = null;
                try
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align == null) continue;
                    alignName = align.Name ?? "";
                    profileIds = align.GetProfileIds();
                }
                catch (Exc) { }
                if (profileIds == null) continue;

                foreach (ObjectId profileId in profileIds)
                {
                    try
                    {
                        var obj = tr.GetObject(profileId, OpenMode.ForRead);
                        var name = Safe.PropString(obj, "Name");
                        if (name == null) continue;
                        result.Add(new ProfileInfo
                        {
                            Id        = profileId,
                            Handle    = profileId.Handle.ToString(),
                            Name      = name,
                            AlignName = alignName,
                            DisplayName = string.IsNullOrEmpty(alignName) ? name : alignName + " › " + name
                        });
                    }
                    catch (Exc) { }
                }
            }
            return result;
        }

        /// <summary>Отметка профиля на заданном пикетаже (м). null если профиль не найден или ошибка.</summary>
        public static double? ElevationAt(Transaction tr, ObjectId profileId, double station)
        {
            if (profileId.IsNull) return null;
            try
            {
                var obj = tr.GetObject(profileId, OpenMode.ForRead);
                var profile = obj as Profile;
                if (profile != null)
                    return profile.ElevationAt(station);
                return InvokeElev(obj, station);
            }
            catch (Exc) { return null; }
        }

        static double? InvokeElev(object profile, double station)
        {
            try
            {
                var mi = profile.GetType().GetMethod("ElevationAt", new[] { typeof(double) });
                if (mi == null) return null;
                var r = mi.Invoke(profile, new object[] { station });
                if (r == null) return null;
                return Convert.ToDouble(r);
            }
            catch { return null; }
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

    struct ProfileInfo
    {
        public ObjectId Id;
        public string   Handle;
        public string   Name;
        public string   AlignName;
        public string   DisplayName;
    }
}
