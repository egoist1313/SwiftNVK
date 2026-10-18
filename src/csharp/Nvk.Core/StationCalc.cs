using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nvk.Core
{
    /// <summary>
    /// Пикеты поперечников.
    /// Вертикальные стенки (m=0): AUTO — середина каждой группы одинакового диаметра.
    /// Откос (m&gt;0): AUTO — внутренние стенки колодцев + шаг ~10 м по траншее (не внутрь шахты).
    /// Ручной ввод: "1.5,3.2,5.0" в метрах вдоль цепочки.
    /// </summary>
    public static class StationCalc
    {
        public const double VolStep = 10.0;

        public static List<double> Auto(Chain chain)
        {
            return Auto(chain, 0.0, null);
        }

        public static List<double> Auto(Chain chain, double slopeM, IList<RoadZone> zones)
        {
            if (chain == null || chain.Segs.Count == 0)
                return new List<double>();
            if (slopeM > 1e-9)
                return AutoForVolume(chain, VolStep, zones);
            return AutoByDiam(chain);
        }

        static List<double> AutoByDiam(Chain chain)
        {
            var stations = new List<double>();
            double ch = 0.0;
            int? prevMm = null;
            double groupStart = 0.0;

            foreach (var pd in chain.Segs)
            {
                int mm = pd.DiamMm;
                double len = pd.Length2d;
                if (prevMm == null)
                    groupStart = 0.0;
                else if (mm != prevMm.Value)
                {
                    stations.Add((groupStart + ch) * 0.5);
                    groupStart = ch;
                }
                prevMm = mm;
                ch += len;
            }
            if (prevMm != null)
                stations.Add((groupStart + ch) * 0.5);
            if (stations.Count == 0)
                stations.Add(chain.Total * 0.5);
            return stations;
        }

        /// <summary>
        /// Сечения для объёма при откосе: концы каждой группы диаметра + равномерный шаг,
        /// плюс границы зон покрытия.
        /// </summary>
        public static List<double> AutoForVolume(Chain chain, double step, IList<RoadZone> zones)
        {
            if (step < 1.0) step = VolStep;
            var stations = new List<double>();
            double ch = 0.0;
            int? prevMm = null;
            double groupStart = 0.0;

            foreach (var pd in chain.Segs)
            {
                int mm = pd.DiamMm;
                double len = pd.Length2d;
                if (prevMm == null)
                    groupStart = 0.0;
                else if (mm != prevMm.Value)
                {
                    AddGroupStations(stations, groupStart, ch, step, chain);
                    groupStart = ch;
                }
                prevMm = mm;
                ch += len;
            }
            if (prevMm != null)
                AddGroupStations(stations, groupStart, ch, step, chain);

            if (zones != null)
            {
                foreach (var z in zones)
                {
                    AddTrenchStation(stations, z.Ch0, chain);
                    AddTrenchStation(stations, z.Ch1, chain);
                }
            }

            stations.Sort();
            if (stations.Count == 0)
                stations.Add(chain.Total * 0.5);
            return stations;
        }

        static void AddGroupStations(List<double> stations, double g0, double g1, double step, Chain chain)
        {
            double len = g1 - g0;
            if (len < 1e-6) return;
            double total = chain.Total;
            double r0 = chain.ManholeRAt(g0, false);
            double r1 = chain.ManholeRAt(g1, false);
            double inset0 = r0 > 1e-6 ? r0 : Math.Min(0.05, len * 0.02);
            double inset1 = r1 > 1e-6 ? r1 : Math.Min(0.05, len * 0.02);
            double a0 = g0 + inset0;
            double a1 = g1 - inset1;
            if (a1 < a0 + 1e-4)
            {
                double mid = 0.5 * (g0 + g1);
                AddTrenchStation(stations, mid, chain);
                return;
            }
            AddTrenchStation(stations, a0, chain);
            AddTrenchStation(stations, a1, chain);
            int n = Math.Max(1, (int)Math.Ceiling((a1 - a0) / step));
            for (int i = 1; i < n; i++)
                AddTrenchStation(stations, a0 + (a1 - a0) * i / n, chain);
            AddInnerWalls(stations, chain, g0, g1);
        }

        static void AddInnerWalls(List<double> stations, Chain chain, double g0, double g1)
        {
            double ch = 0.0;
            foreach (var seg in chain.Segs)
            {
                double rs = seg.ShaftR(true);
                if (rs > 1e-6 && ch > g0 + 1e-6 && ch < g1 - 1e-6)
                {
                    AddTrenchStation(stations, ch - rs, chain);
                    AddTrenchStation(stations, ch + rs, chain);
                }
                ch += seg.Length2d;
                double re = seg.ShaftR(false);
                if (re > 1e-6 && ch > g0 + 1e-6 && ch < g1 - 1e-6)
                {
                    AddTrenchStation(stations, ch - re, chain);
                    AddTrenchStation(stations, ch + re, chain);
                }
            }
        }

        static void AddTrenchStation(List<double> stations, double s, Chain chain)
        {
            if (chain.InManhole(s)) return;
            AddUnique(stations, s, chain.Total);
        }

        static void AddUnique(List<double> stations, double s, double total)
        {
            if (s < 1e-9) s = 1e-9;
            if (s > total) s = total;
            if (s <= 0.0 || s > total) return;
            for (int i = 0; i < stations.Count; i++)
            {
                if (Math.Abs(stations[i] - s) < 0.05)
                    return;
            }
            stations.Add(s);
        }

        public static List<double> Parse(string stationsStr, Chain chain)
        {
            return Parse(stationsStr, chain, 0.0, null);
        }

        public static List<double> Parse(string stationsStr, Chain chain, double slopeM, IList<RoadZone> zones)
        {
            if (string.IsNullOrWhiteSpace(stationsStr)
                || stationsStr.Trim().Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                return Auto(chain, slopeM, zones);

            var stations = new List<double>();
            foreach (var tok in stationsStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double n;
                if (double.TryParse(tok.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n)
                    && n > 0.0 && n <= chain.Total)
                    stations.Add(n);
            }
            if (stations.Count == 0)
            {
                double ch = 0.0;
                foreach (var pd in chain.Segs)
                {
                    stations.Add(ch + pd.Length2d * 0.5);
                    ch += pd.Length2d;
                }
            }
            return stations;
        }
    }
}
