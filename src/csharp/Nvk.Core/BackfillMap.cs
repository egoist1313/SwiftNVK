using System;

namespace Nvk.Core
{
    /// <summary>Чем заполняют обратную засыпку (L5) под типом покрытия.</summary>
    public enum BackfillKind
    {
        Sand = 0,
        Soil = 1
    }

    public static class BackfillMap
    {
        public static BackfillKind Of(string zoneType, SectionConfig cfg)
        {
            string code;
            if (string.Equals(zoneType, "ASPH", StringComparison.OrdinalIgnoreCase))
                code = cfg != null ? cfg.FillAsph : "SAND";
            else if (string.Equals(zoneType, "TROT", StringComparison.OrdinalIgnoreCase))
                code = cfg != null ? cfg.FillTrot : "SAND";
            else if (string.Equals(zoneType, "GAZN", StringComparison.OrdinalIgnoreCase))
                code = cfg != null ? cfg.FillGazn : "SOIL";
            else if (string.Equals(zoneType, "OTHER", StringComparison.OrdinalIgnoreCase))
                code = cfg != null ? cfg.FillOther : "SOIL";
            else
                code = cfg != null ? cfg.FillOther : "SOIL";
            return Parse(code);
        }

        public static BackfillKind Parse(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return BackfillKind.Soil;
            if (code.Equals("SAND", StringComparison.OrdinalIgnoreCase)
                || code.Equals("песок", StringComparison.OrdinalIgnoreCase)
                || code == "0")
                return BackfillKind.Sand;
            return BackfillKind.Soil;
        }

        public static string Code(BackfillKind k)
        {
            return k == BackfillKind.Sand ? "SAND" : "SOIL";
        }

        public static string Ru(BackfillKind k)
        {
            return k == BackfillKind.Sand ? "песок" : "местн.грунта";
        }

        public static string Hatch(BackfillKind k)
        {
            return k == BackfillKind.Sand ? "AR-SAND" : "DOTS";
        }
    }
}
