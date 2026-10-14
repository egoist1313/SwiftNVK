using System;
using System.Globalization;
using System.Reflection;

namespace Nvk.Cad
{
    static class Safe
    {
        public static double PropDouble(object obj, string name, double fallback)
        {
            if (obj == null) return fallback;
            try
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null) return fallback;
                var v = p.GetValue(obj, null);
                if (v == null) return fallback;
                return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static string PropString(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null) return null;
                var v = p.GetValue(obj, null);
                return v as string;
            }
            catch
            {
                return null;
            }
        }

        public static object Prop(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null) return null;
                return p.GetValue(obj, null);
            }
            catch
            {
                return null;
            }
        }

        public static double Meters(double v)
        {
            if (v > 10.0) return v / 1000.0;
            return v;
        }
    }
}
