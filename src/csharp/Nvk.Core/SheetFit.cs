using System;

namespace Nvk.Core
{
    /// <summary>Видовые рамки на листе: сдвиг и масштаб, чтобы все влезли в область печати.</summary>
    public static class SheetFit
    {
        /// <summary>
        /// Если рамки уже внутри (x0,y0)–(x1,y1) — без изменений, 1.
        /// Иначе масштаб ≤1 и прижатие к верху-леву области.
        /// </summary>
        public static double Fit(double[] cx, double[] cy, double[] w, double[] h,
            double x0, double y0, double x1, double y1)
        {
            if (cx == null || cy == null || w == null || h == null) return 1.0;
            int n = cx.Length;
            if (n == 0 || n != cy.Length || n != w.Length || n != h.Length) return 1.0;
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }
            double pw = x1 - x0, ph = y1 - y0;
            if (pw < 1.0) pw = 1.0;
            if (ph < 1.0) ph = 1.0;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double ww = w[i] < 0.0 ? 0.0 : w[i];
                double hh = h[i] < 0.0 ? 0.0 : h[i];
                double l = cx[i] - ww * 0.5, r = cx[i] + ww * 0.5;
                double b = cy[i] - hh * 0.5, t = cy[i] + hh * 0.5;
                if (l < minX) minX = l;
                if (b < minY) minY = b;
                if (r > maxX) maxX = r;
                if (t > maxY) maxY = t;
            }
            if (minX >= x0 - 1e-6 && minY >= y0 - 1e-6
                && maxX <= x1 + 1e-6 && maxY <= y1 + 1e-6)
                return 1.0;

            double bw = maxX - minX, bh = maxY - minY;
            double scale = 1.0;
            if (bw > 1e-9) scale = Math.Min(scale, pw / bw);
            if (bh > 1e-9) scale = Math.Min(scale, ph / bh);
            if (scale > 1.0) scale = 1.0;
            if (scale < 0.05) scale = 0.05;

            for (int i = 0; i < n; i++)
            {
                double l = cx[i] - w[i] * 0.5;
                double t = cy[i] + h[i] * 0.5;
                double nl = x0 + (l - minX) * scale;
                double nt = y1 - (maxY - t) * scale;
                w[i] *= scale;
                h[i] *= scale;
                if (w[i] < 10.0) w[i] = 10.0;
                if (h[i] < 10.0) h[i] = 10.0;
                cx[i] = nl + w[i] * 0.5;
                cy[i] = nt - h[i] * 0.5;
            }
            return scale;
        }
    }
}
