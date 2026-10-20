using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Снимок бумаги листов НВК: видовые, линии, тексты.
    /// Стандарт — presets/sheets/snap.json, иначе StdPage A3.
    /// </summary>
    public sealed class SheetSnapFile
    {
        public int Version { get; set; }
        /// <summary>Параметры листа по умолчанию — на все вкладки.</summary>
        public SheetSnapPage Page { get; set; }
        public List<SheetSnapLayout> Layouts { get; set; } = new List<SheetSnapLayout>();
    }

    public sealed class SheetSnapPage
    {
        public string Device { get; set; }
        public string Media { get; set; }
        public string Units { get; set; }
        public string PlotType { get; set; }
        public string Rotation { get; set; }
        public double Ox { get; set; }
        public double Oy { get; set; }
        public bool StdScale { get; set; }
        public string ScaleType { get; set; }
        public double ScaleNum { get; set; }
        public double ScaleDen { get; set; }
        public string Style { get; set; }
        public bool PlotStyles { get; set; }
        public bool ScaleLw { get; set; }
        public bool Centered { get; set; }
        public bool Lineweights { get; set; }
        public bool VpBorders { get; set; }
        public double PaperW { get; set; }
        public double PaperH { get; set; }
        /// <summary>Окно печати (PlotType Window), единицы бумаги.</summary>
        public double Wx0 { get; set; }
        public double Wy0 { get; set; }
        public double Wx1 { get; set; }
        public double Wy1 { get; set; }
    }

    public sealed class SheetSnapLayout
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public SheetSnapPage Page { get; set; }
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

        public static bool HasPage(SheetSnapPage page)
        {
            return page != null && !string.IsNullOrWhiteSpace(page.Device);
        }

        /// <summary>
        /// Стандарт листа — снимок 0.222: A3 297×420, DWG To PDF, поворот 90°.
        /// </summary>
        public static SheetSnapPage StdPage()
        {
            return new SheetSnapPage
            {
                Device = "DWG To PDF.pc3",
                Media = "ISO_full_bleed_A3_(297.00_x_420.00_MM)",
                Units = "Millimeters",
                PlotType = "Window",
                Rotation = "Degrees090",
                Ox = 0.0,
                Oy = 0.0,
                StdScale = false,
                ScaleType = "StdScale1To1",
                ScaleNum = 1.0,
                ScaleDen = 1.0,
                Style = "acad.ctb",
                PlotStyles = false,
                ScaleLw = false,
                Centered = false,
                Lineweights = true,
                VpBorders = false,
                PaperW = 297.0,
                PaperH = 420.0,
                Wx0 = 0.0,
                Wy0 = 0.0,
                Wx1 = 297.0,
                Wy1 = 420.0
            };
        }

        /// <summary>Общий плот: поле файла, иначе Разработка, иначе первый лист, иначе StdPage.</summary>
        public static SheetSnapPage DefaultPage(SheetSnapFile file)
        {
            if (HasPage(file != null ? file.Page : null)) return file.Page;
            var raz = Find(file, "Razrabotka");
            if (raz != null && HasPage(raz.Page)) return raz.Page;
            if (file != null && file.Layouts != null)
            {
                for (int i = 0; i < file.Layouts.Count; i++)
                {
                    var lay = file.Layouts[i];
                    if (lay != null && HasPage(lay.Page)) return lay.Page;
                }
            }
            return StdPage();
        }

        public static SheetSnapPage PageOf(SheetSnapFile file, string kind)
        {
            var lay = Find(file, kind);
            if (lay != null && HasPage(lay.Page)) return lay.Page;
            return DefaultPage(file);
        }

        public static bool HasWindow(SheetSnapPage page)
        {
            return page != null
                && page.Wx1 > page.Wx0 + 10.0
                && page.Wy1 > page.Wy0 + 10.0;
        }

        /// <summary>Окно печати: поле страницы, иначе рамка Defpoints / бумага снимка.</summary>
        public static void FillWindow(SheetSnapPage page, SheetSnapLayout lay)
        {
            if (page == null || HasWindow(page)) return;
            double x0, y0, x1, y1;
            if (!TryFrame(lay, out x0, out y0, out x1, out y1)) return;
            page.Wx0 = x0;
            page.Wy0 = y0;
            page.Wx1 = x1;
            page.Wy1 = y1;
        }

        public static bool TryFrame(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            if (TryDefpointsFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            return TryPaperBox(lay, out x0, out y0, out x1, out y1);
        }

        public static bool IsPaperCoord(double x, double y)
        {
            return x > -200.0 && x < 5000.0 && y > -200.0 && y < 5000.0;
        }

        public static bool IsNearPaper(SheetSnapEnt e, double x0, double y0, double x1, double y1)
        {
            if (e == null) return false;
            double pad = 80.0;
            double ax = x0 - pad, ay = y0 - pad, bx = x1 + pad, by = y1 + pad;
            bool any = false, near = false;
            EachPoint(e, (x, y) =>
            {
                any = true;
                if (x >= ax && x <= bx && y >= ay && y <= by)
                    near = true;
            });
            return any && near;
        }

        /// <summary>Высота вида модели при масштабе 1:N. Бумага в мм, модель в м.</summary>
        public static double ViewHeightAtScale(double paperH, int scaleN)
        {
            int n = scaleN < 1 ? 1 : scaleN;
            double mm = 1000.0 / n;
            if (mm < 1e-9) mm = 1.0;
            double h = paperH / mm;
            return h < 1e-9 ? 1.0 : h;
        }

        static bool TryDefpointsFrame(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;
            if (lay == null || lay.Ents == null) return false;
            bool any = false;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null) continue;
                if (!string.Equals(e.T, "LINE", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsDefpoints(e.Layer)) continue;
                if (!IsPaperCoord(e.X, e.Y) || !IsPaperCoord(e.X1, e.Y1))
                    continue;
                Acc(e.X, e.Y, ref any, ref x0, ref y0, ref x1, ref y1);
                Acc(e.X1, e.Y1, ref any, ref x0, ref y0, ref x1, ref y1);
            }
            return any && x1 - x0 > 50.0 && y1 - y0 > 50.0;
        }

        static bool TryPaperBox(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;
            if (lay == null) return false;
            bool any = false;
            double a0 = 0.0, b0 = 0.0, a1 = 0.0, b1 = 0.0;
            if (lay.Vps != null)
            {
                for (int i = 0; i < lay.Vps.Count; i++)
                {
                    var v = lay.Vps[i];
                    if (v == null || v.W < 1.0 || v.H < 1.0) continue;
                    double ax = v.Cx - v.W * 0.5, ay = v.Cy - v.H * 0.5;
                    double bx = v.Cx + v.W * 0.5, by = v.Cy + v.H * 0.5;
                    if (!IsPaperCoord(ax, ay) || !IsPaperCoord(bx, by)) continue;
                    Acc(ax, ay, ref any, ref a0, ref b0, ref a1, ref b1);
                    Acc(bx, by, ref any, ref a0, ref b0, ref a1, ref b1);
                }
            }
            if (lay.Ents != null)
            {
                for (int i = 0; i < lay.Ents.Count; i++)
                {
                    var e = lay.Ents[i];
                    if (e == null || IsSheetTitle(e)) continue;
                    EachPoint(e, (x, y) =>
                    {
                        if (IsPaperCoord(x, y))
                            Acc(x, y, ref any, ref a0, ref b0, ref a1, ref b1);
                    });
                }
            }
            if (!any || a1 - a0 <= 50.0 || b1 - b0 <= 50.0) return false;
            x0 = a0; y0 = b0; x1 = a1; y1 = b1;
            return true;
        }

        static bool IsDefpoints(string layer)
        {
            return string.Equals(layer, "Defpoints", StringComparison.OrdinalIgnoreCase);
        }

        static void Acc(double x, double y, ref bool any,
            ref double x0, ref double y0, ref double x1, ref double y1)
        {
            if (!any)
            {
                x0 = x1 = x;
                y0 = y1 = y;
                any = true;
                return;
            }
            if (x < x0) x0 = x;
            if (y < y0) y0 = y;
            if (x > x1) x1 = x;
            if (y > y1) y1 = y;
        }

        static void EachPoint(SheetSnapEnt e, Action<double, double> add)
        {
            if (e == null || add == null) return;
            string t = e.T ?? "";
            if (string.Equals(t, "LINE", StringComparison.OrdinalIgnoreCase))
            {
                add(e.X, e.Y);
                add(e.X1, e.Y1);
                return;
            }
            if (string.Equals(t, "PL", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(e.Pts))
            {
                var parts = e.Pts.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    int c = p.IndexOf(',');
                    if (c <= 0) continue;
                    double x, y;
                    if (!double.TryParse(p.Substring(0, c),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out x))
                        continue;
                    if (!double.TryParse(p.Substring(c + 1),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out y))
                        continue;
                    add(x, y);
                }
                return;
            }
            add(e.X, e.Y);
        }
    }
}
