using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Снимок бумаги листов НВК: видовые, линии, тексты.
    /// Временный шаблон — пока нет готовой рамки.
    /// </summary>
    public sealed class SheetSnapFile
    {
        public int Version { get; set; }
        public List<SheetSnapLayout> Layouts { get; set; } = new List<SheetSnapLayout>();
    }

    public sealed class SheetSnapLayout
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public List<SheetSnapVp> Vps { get; set; } = new List<SheetSnapVp>();
        public List<SheetSnapEnt> Ents { get; set; } = new List<SheetSnapEnt>();
    }

    public sealed class SheetSnapVp
    {
        public string Key { get; set; }
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double W { get; set; }
        public double H { get; set; }
    }

    public sealed class SheetSnapEnt
    {
        public string T { get; set; }
        public string Layer { get; set; }
        public short Color { get; set; }
        public string Lt { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double H { get; set; }
        public double W { get; set; }
        public double R { get; set; }
        public double A0 { get; set; }
        public double A1 { get; set; }
        public double Rot { get; set; }
        public double Sx { get; set; }
        public double Sy { get; set; }
        public int Att { get; set; }
        public bool Closed { get; set; }
        public string Txt { get; set; }
        public string Tpl { get; set; }
        public string Name { get; set; }
        public string Pts { get; set; }
        public string Style { get; set; }
    }

    public static class SheetSnap
    {
        public static SheetSnapLayout Find(SheetSnapFile file, string kind)
        {
            if (file == null || file.Layouts == null || string.IsNullOrEmpty(kind))
                return null;
            for (int i = 0; i < file.Layouts.Count; i++)
            {
                var lay = file.Layouts[i];
                if (lay != null
                    && string.Equals(lay.Kind, kind, StringComparison.OrdinalIgnoreCase))
                    return lay;
            }
            return null;
        }

        public static bool TryVp(SheetSnapLayout lay, string key,
            out double cx, out double cy, out double w, out double h)
        {
            cx = 0.0;
            cy = 0.0;
            w = 0.0;
            h = 0.0;
            if (lay == null || lay.Vps == null || string.IsNullOrEmpty(key))
                return false;
            for (int i = 0; i < lay.Vps.Count; i++)
            {
                var v = lay.Vps[i];
                if (v == null) continue;
                if (!string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (v.W < 1.0 || v.H < 1.0) return false;
                cx = v.Cx;
                cy = v.Cy;
                w = v.W;
                h = v.H;
                return true;
            }
            return false;
        }

        public static SheetSnapEnt TitleEnt(SheetSnapLayout lay)
        {
            if (lay == null || lay.Ents == null) return null;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null) continue;
                if (!string.Equals(e.T, "MTEXT", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(e.Layer, "NVK-SHEET", StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            return null;
        }

        public static bool IsSheetTitle(SheetSnapEnt e)
        {
            if (e == null) return false;
            return string.Equals(e.T, "MTEXT", StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Layer, "NVK-SHEET", StringComparison.OrdinalIgnoreCase);
        }
    }
}
