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
        /// <summary>Центр вида в модели (ViewTarget).</summary>
        public double Vx { get; set; }
        public double Vy { get; set; }
        /// <summary>Высота вида в модели, м.</summary>
        public double Vh { get; set; }
        /// <summary>Кручение вида, рад.</summary>
        public double Twist { get; set; }
        /// <summary>1:N с CustomScale (бумага мм / модель м).</summary>
        public int ScaleN { get; set; }
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

    public sealed class NorthSlot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool Sit { get; set; }
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
        /// Стандарт листа — A3 420×297 альбом, DWG To PDF, печать Layout.
        /// </summary>
        public static SheetSnapPage StdPage()
        {
            return new SheetSnapPage
            {
                Device = "DWG To PDF.pc3",
                Media = "ISO_full_bleed_A3_(297.00_x_420.00_MM)",
                Units = "Millimeters",
                PlotType = "Layout",
                Rotation = "Degrees000",
                Ox = 0.0,
                Oy = 0.0,
                StdScale = true,
                ScaleType = "StdScale1To1",
                ScaleNum = 1.0,
                ScaleDen = 1.0,
                Style = "acad.ctb",
                PlotStyles = false,
                ScaleLw = false,
                Centered = false,
                Lineweights = true,
                VpBorders = false,
                PaperW = 420.0,
                PaperH = 297.0,
                Wx0 = 0.0,
                Wy0 = 0.0,
                Wx1 = 420.0,
                Wy1 = 297.0
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

        public const string FrameLayer = "ЭТА РАМКА";

        /// <summary>Окно печати: поле страницы, иначе рамка слоя / Defpoints.</summary>
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

        public static bool IsFrameLayer(string layer)
        {
            return !string.IsNullOrEmpty(layer)
                && string.Equals(layer, FrameLayer, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryFrame(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            if (TryNamedFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            if (TryRedFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            if (TryDefpointsFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            return TryPaperBox(lay, out x0, out y0, out x1, out y1);
        }

        /// <summary>Рамка этого листа, иначе «ЭТА РАМКА» с Разработки.</summary>
        public static bool TryFrame(SheetSnapFile file, string kind,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;
            var lay = Find(file, kind);
            if (TryNamedFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            var raz = Find(file, "Razrabotka");
            if (TryNamedFrame(raz, out x0, out y0, out x1, out y1))
                return true;
            if (TryFrame(lay, out x0, out y0, out x1, out y1))
                return true;
            return TryFrame(raz, out x0, out y0, out x1, out y1);
        }

        /// <summary>Сдвиг снимка: левый низ рамки → (0,0). fw×fh — размер рамки.</summary>
        public static bool ShiftOf(SheetSnapFile file, string kind,
            out double dx, out double dy, out double fw, out double fh)
        {
            dx = 0.0;
            dy = 0.0;
            fw = 420.0;
            fh = 297.0;
            double x0, y0, x1, y1;
            if (!TryFrame(file, kind, out x0, out y0, out x1, out y1))
                return false;
            fw = x1 - x0;
            fh = y1 - y0;
            if (fw < 50.0) fw = 420.0;
            if (fh < 50.0) fh = 297.0;
            dx = -x0;
            dy = -y0;
            return true;
        }

        public static void FitPage(SheetSnapPage page, double fw, double fh)
        {
            if (page == null) return;
            if (fw < 50.0) fw = 420.0;
            if (fh < 50.0) fh = 297.0;
            page.PlotType = "Layout";
            page.Rotation = "Degrees000";
            page.StdScale = true;
            page.ScaleType = "StdScale1To1";
            page.ScaleNum = 1.0;
            page.ScaleDen = 1.0;
            page.Centered = false;
            page.Ox = 0.0;
            page.Oy = 0.0;
            page.PaperW = fw;
            page.PaperH = fh;
            page.Wx0 = 0.0;
            page.Wy0 = 0.0;
            page.Wx1 = fw;
            page.Wy1 = fh;
            page.Media = fw + 1.0 >= fh
                ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)"
                : "ISO_full_bleed_A3_(297.00_x_420.00_MM)";
        }

        public static SheetSnapPage CopyPage(SheetSnapPage src)
        {
            if (src == null) return StdPage();
            return new SheetSnapPage
            {
                Device = src.Device,
                Media = src.Media,
                Units = src.Units,
                PlotType = src.PlotType,
                Rotation = src.Rotation,
                Ox = src.Ox,
                Oy = src.Oy,
                StdScale = src.StdScale,
                ScaleType = src.ScaleType,
                ScaleNum = src.ScaleNum,
                ScaleDen = src.ScaleDen,
                Style = src.Style,
                PlotStyles = src.PlotStyles,
                ScaleLw = src.ScaleLw,
                Centered = src.Centered,
                Lineweights = src.Lineweights,
                VpBorders = src.VpBorders,
                PaperW = src.PaperW,
                PaperH = src.PaperH,
                Wx0 = src.Wx0,
                Wy0 = src.Wy0,
                Wx1 = src.Wx1,
                Wy1 = src.Wy1
            };
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

        /// <summary>CustomScale ≈ 1000/N (мм бумаги на метр модели).</summary>
        public static int ScaleNFromCustom(double customScale)
        {
            if (customScale < 1e-9) return 0;
            int n = (int)Math.Round(1000.0 / customScale);
            return n < 1 ? 0 : n;
        }

        public static int KindNo(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return 0;
            if (string.Equals(kind, "Razrabotka", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(kind, "Pesok", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(kind, "Truba", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(kind, "Zashita", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(kind, "Zasypka", StringComparison.OrdinalIgnoreCase)) return 5;
            int n;
            if (int.TryParse(kind, out n) && n >= 1 && n <= 5) return n;
            return 0;
        }

        public static bool IsSitKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith("SIT:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExtraKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith("EXTRA:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasSitVp(SheetSnapLayout lay)
        {
            if (lay == null || lay.Vps == null) return false;
            for (int i = 0; i < lay.Vps.Count; i++)
            {
                var v = lay.Vps[i];
                if (v != null && IsSitKey(v.Key)) return true;
            }
            return false;
        }

        public static bool ContainsKey(IList<string> have, string key)
        {
            if (have == null || string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < have.Count; i++)
            {
                if (string.Equals(have[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static bool HasEpKey(IList<string> have, int kindNo)
        {
            if (kindNo < 1) return false;
            string p = "EP:" + kindNo.ToString();
            if (ContainsKey(have, p)) return true;
            if (have == null) return false;
            string pre = p + ":";
            for (int i = 0; i < have.Count; i++)
            {
                if (have[i] != null
                    && have[i].StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Метка снимка: своя VP= остаётся; 1:5000 → SIT; свободный 1:N профиля → EP;
        /// иначе EXTRA:n.
        /// </summary>
        public static string InferVpKey(string tagged, int scaleN, string kind, IList<string> have)
        {
            if (!string.IsNullOrEmpty(tagged)) return tagged;
            int k = KindNo(kind);
            string sit = k > 0 ? "SIT:" + k.ToString() : "SIT";
            if (scaleN >= 2500 && scaleN <= 20000 && !ContainsKey(have, sit))
                return sit;
            if (scaleN >= 400 && scaleN <= 2000 && k > 0 && !HasEpKey(have, k))
                return "EP:" + k.ToString();
            int n = 1;
            while (n < 100)
            {
                string x = "EXTRA:" + n.ToString();
                if (!ContainsKey(have, x)) return x;
                n++;
            }
            return "EXTRA:" + n.ToString();
        }

        /// <summary>Блок $5$Arrow3 / *U… — из другого чертежа, на вставку не опираемся.</summary>
        public static bool IsTransientBlock(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name[0] == '*') return true;
            if (name.Length > 3 && name[0] == '$')
            {
                int i = 1;
                while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
                return i > 1 && i < name.Length && name[i] == '$';
            }
            return false;
        }

        public static string BlockBaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            if (name.Length > 3 && name[0] == '$')
            {
                int i = 1;
                while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
                if (i > 1 && i < name.Length && name[i] == '$')
                    return name.Substring(i + 1);
            }
            return name;
        }

        /// <summary>
        /// Пустой вид в (0,0) на бумаге в нуле, мельче 12 мм или больше листа A3 — не снимкаем.
        /// Вид модели в нуле при живой рамке бумаги оставляем: слот EP/VOL.
        /// </summary>
        public static bool IsJunkVp(SheetSnapVp v)
        {
            if (v == null) return true;
            if (v.W < 12.0 || v.H < 8.0) return true;
            if (v.W > 430.0 || v.H > 310.0) return true;
            if (Math.Abs(v.Vx) < 1.0 && Math.Abs(v.Vy) < 1.0
                && Math.Abs(v.Cx) < 12.0 && Math.Abs(v.Cy) < 8.0)
                return true;
            return false;
        }

        public static bool IsEpKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith("EP:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVolKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith("VOL:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Бумажный слот профиля с любой вкладки снимка (EP:n, иначе EP:n:0).</summary>
        public static bool TryEpPaper(SheetSnapFile file,
            out double cx, out double cy, out double w, out double h)
        {
            cx = 0.0;
            cy = 0.0;
            w = 0.0;
            h = 0.0;
            if (file == null || file.Layouts == null) return false;
            SheetSnapVp named = null;
            SheetSnapVp indexed = null;
            for (int i = 0; i < file.Layouts.Count; i++)
            {
                var lay = file.Layouts[i];
                if (lay == null || lay.Vps == null) continue;
                for (int j = 0; j < lay.Vps.Count; j++)
                {
                    var v = lay.Vps[j];
                    if (v == null || !IsEpKey(v.Key) || v.W < 12.0 || v.H < 8.0)
                        continue;
                    string rest = v.Key.Substring(3);
                    if (rest.IndexOf(':') < 0)
                    {
                        named = v;
                        break;
                    }
                    if (indexed == null && rest.EndsWith(":0", StringComparison.Ordinal))
                        indexed = v;
                }
                if (named != null) break;
            }
            var pick = named ?? indexed;
            if (pick == null) return false;
            cx = pick.Cx;
            cy = pick.Cy;
            w = pick.W;
            h = pick.H;
            return true;
        }

        /// <summary>Сдвиг рамки в (x0,y0)–(x1,y1). Размер 1:N не трогаем.</summary>
        public static void ClampVp(ref double cx, ref double cy, double w, double h,
            double x0, double y0, double x1, double y1)
        {
            if (w < 1.0) w = 1.0;
            if (h < 1.0) h = 1.0;
            if (x1 < x0) { double sw = x0; x0 = x1; x1 = sw; }
            if (y1 < y0) { double sh = y0; y0 = y1; y1 = sh; }
            double l = cx - w * 0.5, r = cx + w * 0.5;
            double b = cy - h * 0.5, t = cy + h * 0.5;
            double dx = 0.0, dy = 0.0;
            double pw = x1 - x0, ph = y1 - y0;
            if (w <= pw + 1e-6)
            {
                if (l < x0) dx = x0 - l;
                else if (r > x1) dx = x1 - r;
            }
            else dx = x0 - l;
            if (h <= ph + 1e-6)
            {
                if (b < y0) dy = y0 - b;
                else if (t > y1) dy = y1 - t;
            }
            else dy = y1 - t;
            cx += dx;
            cy += dy;
        }

        /// <summary>Видовые, которые пакуем сами — из снимка чужими не докладываем.</summary>
        public static bool IsPackedVpKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (string.Equals(key, "COORD", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(key, "SAMP", StringComparison.OrdinalIgnoreCase)) return true;
            return key.StartsWith("EP:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("CS:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("PLAN:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("VOL:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNorthBlockName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string b = BlockBaseName(name);
            if (b.IndexOf("HEAD", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (b.IndexOf("North", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (string.Equals(b, "ArrowN", StringComparison.OrdinalIgnoreCase))
                return true;
            if (b.Length < 6 || !b.StartsWith("Arrow", StringComparison.OrdinalIgnoreCase))
                return false;
            for (int i = 5; i < b.Length; i++)
            {
                if (b[i] < '0' || b[i] > '9') return false;
            }
            return true;
        }

        public static bool IsNorthLetter(SheetSnapEnt e)
        {
            if (e == null) return false;
            if (!string.Equals(e.Txt, "N", StringComparison.Ordinal)) return false;
            string st = e.Style ?? "";
            return st.IndexOf("North", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Разобранная стрелка севера (линии/N) — ставим объект Arrow3.</summary>
        public static bool IsNorthDebris(SheetSnapEnt e)
        {
            if (e == null) return false;
            if (IsNorthLetter(e)) return true;
            string st = e.Style ?? "";
            if (st.IndexOf("North Arrow", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.Equals(e.Lt, "ByBlock", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(e.Layer, "0", StringComparison.OrdinalIgnoreCase))
                return false;
            string t = e.T ?? "";
            return string.Equals(t, "PL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "SOLID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "LINE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "ARC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "CIRCLE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "TEXT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Layout North Arrow в мм бумаги (блок в дюймах).</summary>
        public const double NorthScale = 25.4;

        /// <summary>Смещение от буквы N разобранной стрелки к точке вставки Arrow3.</summary>
        public const double NorthOx = 0.3838;
        public const double NorthOy = -5.7142;

        public static void NorthInsertXY(SheetSnapEnt letter, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            if (letter == null) return;
            double rot = letter.Rot;
            double c = Math.Cos(rot);
            double s = Math.Sin(rot);
            x = letter.X + NorthOx * c - NorthOy * s;
            y = letter.Y + NorthOx * s + NorthOy * c;
        }

        public static bool IsPlanKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.StartsWith("PLAN:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNorthInsert(SheetSnapEnt e)
        {
            if (e == null) return false;
            if (!string.Equals(e.T, "INSERT", StringComparison.OrdinalIgnoreCase))
                return false;
            return IsNorthBlockName(e.Name);
        }

        /// <summary>
        /// Места стрелок из снимка. Левая — ситуационник (север вверх), правая — план.
        /// Угол живого видового подставляем при постановке, из снимка не берём.
        /// </summary>
        public static List<NorthSlot> NorthSlots(SheetSnapLayout lay)
        {
            var pts = new List<NorthSlot>();
            if (lay == null || lay.Ents == null) return pts;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null) continue;
                double x, y;
                if (IsNorthLetter(e))
                    NorthInsertXY(e, out x, out y);
                else if (IsNorthInsert(e))
                {
                    x = e.X;
                    y = e.Y;
                }
                else continue;
                pts.Add(new NorthSlot { X = x, Y = y, Sit = true });
            }
            pts.Sort((a, b) => a.X.CompareTo(b.X));
            if (pts.Count >= 2)
            {
                pts[0].Sit = true;
                for (int i = 1; i < pts.Count; i++)
                    pts[i].Sit = false;
            }
            else if (pts.Count == 1)
                pts[0].Sit = NearerToSit(lay, pts[0].X, pts[0].Y);
            return pts;
        }

        static bool NearerToSit(SheetSnapLayout lay, double x, double y)
        {
            if (lay == null || lay.Vps == null) return true;
            SheetSnapVp sit = null, plan = null;
            for (int i = 0; i < lay.Vps.Count; i++)
            {
                var v = lay.Vps[i];
                if (v == null) continue;
                if (IsSitKey(v.Key)) sit = v;
                if (IsPlanKey(v.Key)) plan = v;
            }
            if (sit == null) return false;
            if (plan == null) return true;
            double ds = (x - sit.Cx) * (x - sit.Cx) + (y - sit.Cy) * (y - sit.Cy);
            double dp = (x - plan.Cx) * (x - plan.Cx) + (y - plan.Cy) * (y - plan.Cy);
            return ds <= dp;
        }

        /// <summary>Один профиль EP:4, в снимке два окна EP:4:0.</summary>
        public static string EpFallbackKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!key.StartsWith("EP:", StringComparison.OrdinalIgnoreCase)) return null;
            string rest = key.Substring(3);
            for (int i = 0; i < rest.Length; i++)
            {
                if (rest[i] == ':') return null;
                if (rest[i] < '0' || rest[i] > '9') return null;
            }
            if (rest.Length == 0) return null;
            return key + ":0";
        }

        static bool TryNamedFrame(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;
            if (lay == null || lay.Ents == null) return false;
            bool any = false;
            double a0 = 0.0, b0 = 0.0, a1 = 0.0, b1 = 0.0;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null || !IsFrameLayer(e.Layer)) continue;
                EachPoint(e, (x, y) =>
                {
                    if (IsPaperCoord(x, y))
                        Acc(x, y, ref any, ref a0, ref b0, ref a1, ref b1);
                });
            }
            if (!any || a1 - a0 <= 50.0 || b1 - b0 <= 50.0) return false;
            x0 = a0; y0 = b0; x1 = a1; y1 = b1;
            return true;
        }

        static bool TryRedFrame(SheetSnapLayout lay,
            out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = x1 = y1 = 0.0;
            if (lay == null || lay.Ents == null) return false;
            bool any = false;
            double a0 = 0.0, b0 = 0.0, a1 = 0.0, b1 = 0.0;
            for (int i = 0; i < lay.Ents.Count; i++)
            {
                var e = lay.Ents[i];
                if (e == null || e.Color != 1) continue;
                if (!string.Equals(e.T, "LINE", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(e.T, "PL", StringComparison.OrdinalIgnoreCase))
                    continue;
                EachPoint(e, (x, y) =>
                {
                    if (IsPaperCoord(x, y))
                        Acc(x, y, ref any, ref a0, ref b0, ref a1, ref b1);
                });
            }
            if (!any) return false;
            double w = a1 - a0, h = b1 - b0;
            if (w <= 200.0 || w >= 500.0 || h <= 200.0 || h >= 450.0) return false;
            x0 = a0; y0 = b0; x1 = a1; y1 = b1;
            return true;
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
            if ((string.Equals(t, "PL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "SOLID", StringComparison.OrdinalIgnoreCase))
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
