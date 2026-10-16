using System;

namespace Nvk.Core
{
    /// <summary>
    /// Штриховка сечения и продольника — один FillStyle с вкладки уровня.
    /// Базовый масштаб как pb-hatch-fixed-scale: ANSI31=1, DOTS=0.2, AR-SAND=0.01.
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

        /// <summary>Базовый масштаб паттерна × масштаб из стиля. Один и тот же для CS и EP.</summary>
        public static double Scale(FillStyle fill)
        {
            if (fill == null) return 1.0;
            double s = CsFixed(fill.Pattern);
            if (fill.Scale > 1e-9) s *= fill.Scale;
            if (s < 0.001) s = 0.001;
            return s;
        }

        /// <summary>
        /// Стиль штриховки уровня. L1 — HatchExcavation, L2/L4/L5 — SheetStyle.Hatch.
        /// Труба без заливки.
        /// </summary>
        public static FillStyle ForKind(ProfileKind kind, L1Style l1, SheetStyle sheet)
        {
            if (kind == ProfileKind.Truba) return null;
            if (kind == ProfileKind.Razrabotka)
            {
                if (l1 != null && l1.HatchExcavation != null
                    && !string.IsNullOrEmpty(l1.HatchExcavation.Pattern))
                    return l1.HatchExcavation;
                return FillStyle.Rgb("ANSI31", 160, 160, 160, 1.0);
            }
            if (sheet != null && sheet.Hatch != null
                && !string.IsNullOrEmpty(sheet.Hatch.Pattern))
                return sheet.Hatch;
            switch (kind)
            {
                case ProfileKind.Pesok:
                    return FillStyle.Rgb("ANSI31", 220, 200, 80, 1);
                case ProfileKind.Zashita:
                    return FillStyle.Rgb("AR-SAND", 210, 180, 80, 1);
                case ProfileKind.Zasypka:
                    return FillStyle.Rgb("DOTS", 140, 140, 140, 1);
                default:
                    return FillStyle.Rgb("ANSI31", 160, 160, 160, 1);
            }
        }

        /// <summary>L5: по карте покрытия → песок / местный грунт (подписи объёмов).</summary>
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
