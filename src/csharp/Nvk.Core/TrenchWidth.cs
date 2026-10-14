using System;

namespace Nvk.Core
{
    /// <summary>
    /// Ширина дна траншеи по диаметру и типу стыка.
    /// Формулы как в pc-trench-width (PipeBuild_merged1.lsp).
    /// </summary>
    public static class TrenchWidth
    {
        public const double MinWidth = 0.7;

        public static double For(double diamMeters, JointType joint)
        {
            double add;
            if (diamMeters > 1.6)
            {
                add = 1.4;
            }
            else if (diamMeters > 0.5)
            {
                switch (joint)
                {
                    case JointType.Weld:     add = 0.8; break;
                    case JointType.Coupling: add = 1.2; break;
                    default:                 add = 1.0; break;
                }
            }
            else
            {
                switch (joint)
                {
                    case JointType.Weld:     add = 0.5; break;
                    case JointType.Coupling: add = 0.8; break;
                    default:                 add = 0.6; break;
                }
            }
            return Math.Max(MinWidth, diamMeters + add);
        }
    }
}
