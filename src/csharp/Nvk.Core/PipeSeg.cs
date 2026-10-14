using System;

namespace Nvk.Core
{
    /// <summary>Один сегмент трубы. Плановые XY и лоток в метрах.</summary>
    public sealed class PipeSeg
    {
        public string Handle { get; set; }
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double InvertStart { get; set; }
        public double InvertEnd { get; set; }
        public double Diam { get; set; }
        public string StartName { get; set; }
        public string EndName { get; set; }
        public double StartR { get; set; }
        public double EndR { get; set; }
        public double StartSump { get; set; }
        public double EndSump { get; set; }

        public double Length2d
        {
            get
            {
                double dx = X1 - X0, dy = Y1 - Y0;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>Длина трубы без колодцев: план − Rнач − Rкон. Как pb-pipe-lengths-by-diam.</summary>
        public double NetLength
        {
            get { return Math.Max(0.0, Length2d - StartR - EndR); }
        }

        /// <summary>Радиус колодца на начале/конце. 0 если сооружения нет.</summary>
        public double ManholeR(bool start)
        {
            double r = start ? StartR : EndR;
            if (r > 0.05) return r;
            string n = start ? StartName : EndName;
            if (!string.IsNullOrEmpty(n))
                return Math.Max(0.5, Diam > 0.0 ? Diam : 0.5);
            return 0.0;
        }

        public int DiamMm
        {
            get { return (int)Math.Floor(Diam * 1000.0 + 0.5); }
        }

        public PipeSeg Reversed()
        {
            return new PipeSeg
            {
                Handle = Handle,
                X0 = X1, Y0 = Y1, X1 = X0, Y1 = Y0,
                InvertStart = InvertEnd,
                InvertEnd = InvertStart,
                Diam = Diam,
                StartName = EndName,
                EndName = StartName,
                StartR = EndR,
                EndR = StartR,
                StartSump = EndSump,
                EndSump = StartSump
            };
        }

        public double DistStartTo(double x, double y)
        {
            double dx = X0 - x, dy = Y0 - y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double DistEndTo(double x, double y)
        {
            double dx = X1 - x, dy = Y1 - y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
