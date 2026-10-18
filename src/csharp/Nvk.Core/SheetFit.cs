using System;

namespace Nvk.Core
{
    /// <summary>Видовые рамки на листе: только сдвиг в область печати. Размер 1:N не трогаем.</summary>
    public static class SheetFit
    {
        /// <summary>
        /// Сдвиг кластера в (x0,y0)–(x1,y1). Ширина и высота рамок те же.
        /// Если кластер больше области — прижимаем к верху-леву, лишнее вылезает.
        /// Возвращает 1.
        /// </summary>
        public static double Fit(double[] cx, double[] cy, double[] w, double[] h,
            double x0, double y0, double x1, double y1)
        {
            if (cx == null || cy == null || w == null || h == null) return 1.0;
            int n = cx.Length;
            if (n == 0 || n != cy.Length || n != w.Length || n != h.Length) return 1.0;
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }

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
            double pw = x1 - x0, ph = y1 - y0;
            double dx = 0.0, dy = 0.0;
            if (bw <= pw + 1e-6)
            {
                if (minX < x0) dx = x0 - minX;
                else if (maxX > x1) dx = x1 - maxX;
            }
            else
                dx = x0 - minX;
            if (bh <= ph + 1e-6)
            {
                if (maxY > y1) dy = y1 - maxY;
                else if (minY < y0) dy = y0 - minY;
            }
            else
                dy = y1 - maxY;

            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return 1.0;
            for (int i = 0; i < n; i++)
            {
                cx[i] += dx;
                cy[i] += dy;
            }
            return 1.0;
        }
    }
}
