using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>
    /// Прямоугольник окна листа (метры модели). Кластеризация:
    /// два профиля на одном слое NVK-EP-* не склеиваются в один
    /// гигантский видовой, если между ними зазор больше Gap.
    /// </summary>
    public struct WinBox
    {
        public double MinX, MinY, MaxX, MaxY;

        public WinBox(double x0, double y0, double x1, double y1)
        {
            if (x1 < x0) { double t = x0; x0 = x1; x1 = t; }
            if (y1 < y0) { double t = y0; y0 = y1; y1 = t; }
            MinX = x0; MinY = y0; MaxX = x1; MaxY = y1;
        }

        public bool Valid
        {
            get { return MaxX > MinX + 1e-6 && MaxY > MinY + 1e-6; }
        }

        public double Width { get { return Valid ? MaxX - MinX : 0; } }
        public double Height { get { return Valid ? MaxY - MinY : 0; } }

        public bool Overlaps(WinBox other, double gap)
        {
            if (gap < 0) gap = 0;
            if (MaxX + gap < other.MinX) return false;
            if (other.MaxX + gap < MinX) return false;
            if (MaxY + gap < other.MinY) return false;
            if (other.MaxY + gap < MinY) return false;
            return true;
        }

        public WinBox Union(WinBox other)
        {
            return new WinBox(
                Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY),
                Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
        }
    }

    public static class WinCluster
    {
        /// <summary>
        /// Зазор, м: внутри одного профиля стыки/колодцы ближе,
        /// два участка (разные вставки) — на сотни метров и дальше.
        /// </summary>
        public const double EpGapM = 100.0;

        public static List<WinBox> Cluster(IList<WinBox> boxes, double gap)
        {
            var r = new List<WinBox>();
            if (boxes == null) return r;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Valid) r.Add(boxes[i]);
            }
            if (r.Count <= 1) return r;
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < r.Count; i++)
                {
                    for (int j = i + 1; j < r.Count; j++)
                    {
                        if (!r[i].Overlaps(r[j], gap)) continue;
                        r[i] = r[i].Union(r[j]);
                        r.RemoveAt(j);
                        merged = true;
                        break;
                    }
                    if (merged) break;
                }
            }
            r.Sort(Compare);
            return r;
        }

        static int Compare(WinBox a, WinBox b)
        {
            int cx = a.MinX.CompareTo(b.MinX);
            if (cx != 0) return cx;
            return a.MinY.CompareTo(b.MinY);
        }
    }
}
