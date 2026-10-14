using System;

namespace Nvk.Core
{
    /// <summary>
    /// Отметки именованных линий на одном пикете. Все в метрах.
    /// Surface зажимается не ниже TrenchBottom.
    /// </summary>
    public sealed class StationElevations
    {
        public double Station { get; }
        public double Invert { get; private set; }
        public double Diam { get; }
        public double Bedding { get; }
        public double ThkZash { get; }
        public double TrenchBottom { get; private set; }
        public double SandTop { get; private set; }
        public double PipeTop { get; private set; }
        public double ZashTop { get; private set; }
        public double Surface { get; private set; }
        public bool HasBackfill { get; private set; }

        /// <summary>
        /// Подставить отметки как на продольнике (после scatter / клипа).
        /// Bedding в каталоге не меняется — меняются сами линии.
        /// </summary>
        public void SetDrawn(double trench, double sand, double invert, double pipe, double zash, double earth)
        {
            TrenchBottom = trench;
            SandTop = sand;
            Invert = invert;
            PipeTop = pipe;
            ZashTop = zash;
            Surface = earth;
            HasBackfill = Surface > ZashTop + 1e-9;
        }

        public StationElevations(
            double station,
            double invert,
            double diam,
            double bedding,
            double thkZash,
            double surfaceRaw,
            double designRaw = double.NaN,
            double wall = 0.0)
        {
            if (diam <= 0.0) throw new ArgumentOutOfRangeException(nameof(diam));
            if (bedding < 0.0) throw new ArgumentOutOfRangeException(nameof(bedding));
            if (thkZash < 0.0) throw new ArgumentOutOfRangeException(nameof(thkZash));

            Station = station;
            Invert = invert;
            Diam = diam;
            Bedding = bedding;
            ThkZash = thkZash;

            if (double.IsNaN(designRaw)) designRaw = surfaceRaw;
            double trench, sand, pipe, zash, earth, design;
            LineClamp.Apply(invert, diam, bedding, thkZash, designRaw, surfaceRaw, wall,
                out trench, out sand, out pipe, out zash, out earth, out design);
            TrenchBottom = trench;
            SandTop = sand;
            PipeTop = pipe;
            ZashTop = zash;
            Surface = earth;
            HasBackfill = Surface > ZashTop + 1e-9;
        }

        public double this[NamedLine line]
        {
            get
            {
                switch (line)
                {
                    case NamedLine.TrenchBottom: return TrenchBottom;
                    case NamedLine.SandTop:      return SandTop;
                    case NamedLine.PipeTop:      return PipeTop;
                    case NamedLine.ZashTop:      return ZashTop;
                    case NamedLine.Surface:      return Surface;
                    default: throw new ArgumentOutOfRangeException(nameof(line));
                }
            }
        }
    }
}
