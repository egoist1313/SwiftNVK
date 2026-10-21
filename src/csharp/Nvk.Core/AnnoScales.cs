using System;

namespace Nvk.Core
{
    /// <summary>Масштабы аннотативных объектов по умолчанию.</summary>
    public static class AnnoScales
    {
        /// <summary>Имя коллекции ObjectARX. Не ACDB_ANNOTATION_SCALES.</summary>
        public const string CollName = "ACDB_ANNOTATIONSCALES";
        public const string CollNameAlt = "ACDB_ANNOTATION_SCALES";

        public static readonly double[] Default = { 200, 500, 1000, 5000 };

        public static string NameOf(double den)
        {
            int n = (int)Math.Round(den);
            if (n < 1) n = 1;
            return "1:" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool IsDefault(double den)
        {
            if (den < 1.0) return false;
            for (int i = 0; i < Default.Length; i++)
            {
                if (Math.Abs(den - Default[i]) < 0.6) return true;
            }
            return false;
        }
    }
}
