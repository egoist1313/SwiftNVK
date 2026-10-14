using System;

namespace Nvk.Core
{
    /// <summary>
    /// Масштабы штриховки как pb-hatch-fixed-scale: не зависят от масштаба сечения.
    /// ANSI31=1, DOTS=0.2, AR-SAND=0.01.
    /// </summary>
    public static class HatchPat
    {
        public static double CsFixed(string pattern)
        {
            if (string.Equals(pattern, "DOTS", StringComparison.OrdinalIgnoreCase))
                return 0.2;
            if (string.Equals(pattern, "AR-SAND", StringComparison.OrdinalIgnoreCase))
                return 0.01;
            return 1.0;
        }

        /// <summary>L5: по карте покрытия → песок / местный грунт.</summary>
        public static string Backfill(string zoneType)
        {
            return Backfill(zoneType, null);
        }

        public static string Backfill(string zoneType, SectionConfig cfg)
        {
            return BackfillMap.Hatch(BackfillMap.Of(zoneType, cfg));
        }
    }
}
