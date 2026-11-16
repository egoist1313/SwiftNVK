using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Контуры разреза элемента колодца. Наружный и внутренний замкнутые
    /// прямоугольники (трапеции у конуса), чтобы стык кольца / шахты / плиты
    /// давал сквозную линию по диаметру, а не две независимые половинки стенки.
    /// X — поперёк оси, Y — отметка.
    /// </summary>
    public static class WellSection
    {
        public struct Pt
        {
            public double X;
            public double Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        public static List<Pt[]> LoopsOf(WellPartInst p, double z0, double z1)
        {
            var loops = new List<Pt[]>();
            if (p == null) return loops;
            double ax0 = p.AxisX;
            double ax1 = p.TopInnerR > 0.01 || Math.Abs(p.TopAxisX - p.AxisX) > 1e-6
                ? p.TopAxisX : p.AxisX;
            double o0 = p.OuterR;
            double o1 = p.TopOuterR > 0.02 ? p.TopOuterR : p.OuterR;
            double i0 = p.HoleR > 0.02 ? p.HoleR : p.InnerR;
            double i1 = p.TopInnerR > 0.02 ? p.TopInnerR : i0;
            bool taper = p.Kind == WellPartKind.Cone
                || Math.Abs(o1 - o0) > 1e-4
                || Math.Abs(i1 - i0) > 1e-4
                || Math.Abs(ax1 - ax0) > 1e-4;
            if (p.Kind == WellPartKind.Bottom)
            {
                loops.Add(Rect(ax0 - o0, z0, ax0 + o0, z1));
                return loops;
            }
            if (p.Kind == WellPartKind.Base)
            {
                double floor = Math.Min(0.08, 0.4 * Math.Max(0.0, z1 - z0));
                loops.Add(Rect(ax0 - o0, z0, ax0 + o0, z1));
                if (i0 > 0.02)
                    loops.Add(Rect(ax0 - i0, z0 + floor, ax0 + i0, z1));
                return loops;
            }
            if (taper)
            {
                loops.Add(Quad(ax0 - o0, ax0 + o0, z0, ax1 - o1, ax1 + o1, z1));
                if (i0 > 0.02 && i1 > 0.02)
                    loops.Add(Quad(ax0 - i0, ax0 + i0, z0, ax1 - i1, ax1 + i1, z1));
                return loops;
            }
            loops.Add(Rect(ax0 - o0, z0, ax0 + o0, z1));
            if (i0 > 0.02 && i0 + 1e-4 < o0)
                loops.Add(Rect(ax0 - i0, z0, ax0 + i0, z1));
            return loops;
        }

        static Pt[] Rect(double x0, double z0, double x1, double z1)
        {
            return new[]
            {
                new Pt(x0, z0), new Pt(x1, z0), new Pt(x1, z1), new Pt(x0, z1)
            };
        }

        static Pt[] Quad(double x0l, double x0r, double z0, double x1l, double x1r, double z1)
        {
            return new[]
            {
                new Pt(x0l, z0), new Pt(x0r, z0), new Pt(x1r, z1), new Pt(x1l, z1)
            };
        }
    }
}
