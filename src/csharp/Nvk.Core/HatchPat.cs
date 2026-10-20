using System;

namespace Nvk.Core
{
    /// <summary>
    /// Штриховка сечения и продольника — один FillStyle с вкладки уровня.
    /// FillStyle.Scale = PatternScale AutoCAD. CsFixed — только если Scale не задан.
    /// </summary>
    public static class HatchPat
    {
        public const double SandScale = 0.1;
        public const double SoilScale = 0.2;

        public static double CsFixed(string pattern)
        {
            if (string.Equals(pattern, "DOTS", StringComparison.OrdinalIgnoreCase))
                return SoilScale;
            if (string.Equals(pattern, "AR-SAND", StringComparison.OrdinalIgnoreCase))
                return SandScale;
            return 1.0;
        }

        /// <summary>Масштаб паттерна как в диалоге штриховки. Один и тот же для CS и EP.</summary>
        public static double Scale(FillStyle fill)
        {
            if (fill == null) return 1.0;
            double s = fill.Scale > 1e-9 ? fill.Scale : CsFixed(fill.Pattern);
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
                return FillStyle.Of("ANSI31", 5, 1.0);
            }
            if (sheet != null && sheet.Hatch != null
                && !string.IsNullOrEmpty(sheet.Hatch.Pattern))
                return sheet.Hatch;
            switch (kind)
            {
                case ProfileKind.Pesok:
                    return FillStyle.Of("ANSI31", 5, 1);
                case ProfileKind.Zashita:
                    return FillStyle.Of("AR-SAND", 5, SandScale);
                case ProfileKind.Zasypka:
                    return FillStyle.Of("DOTS", 5, SoilScale);
                default:
                    return FillStyle.Of("ANSI31", 5, 1);
            }
        }

        /// <summary>L5: свотч песка или грунта с вкладки засыпки.</summary>
        public static FillStyle ForBackfill(SheetStyle sheet, string zoneType, SectionConfig cfg)
        {
            var k = BackfillMap.Of(zoneType, cfg);
            FillStyle src = PickBackfill(sheet, k);
            if (src != null && !string.IsNullOrEmpty(src.Pattern))
                return src.Clone();
            return k == BackfillKind.Sand
                ? FillStyle.Of("AR-SAND", 5, SandScale)
                : FillStyle.Of("DOTS", 5, SoilScale);
        }

        static FillStyle PickBackfill(SheetStyle sheet, BackfillKind k)
        {
            if (sheet == null) return null;
            if (k == BackfillKind.Sand)
                return sheet.HatchSand;
            return sheet.HatchSoil ?? sheet.Hatch;
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
