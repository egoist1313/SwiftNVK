using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nvk.Core
{
    public enum TableRowKind
    {
        Height,
        Length,
        Slope,
        DeltaH
    }

    /// <summary>Настройки таблицы как в PoperSwift (DCL / XData).</summary>
    public sealed class TableCfg
    {
        public double TextH { get; set; } = 2.5;
        public double OffsetPct { get; set; } = 10.0;
        public int RoundTo { get; set; } = 2;
        public int SlopeRoundTo { get; set; } = 1;
        public string SlopeUnit { get; set; } = "permille";
        public bool ShowPermille { get; set; } = true;
        public bool ShowLengths { get; set; } = true;
        public bool ShowSlopes { get; set; } = true;
        public double LabelColW { get; set; } = 0.0;
        public double CsTextH { get; set; } = 0.5;
        public bool Annotative { get; set; }
        public double AnnGap { get; set; } = 10.0;
        public bool DrawProjections { get; set; } = true;
        public short ProjectionColor { get; set; } = 256;
        public bool UseTextHeight { get; set; } = true;
        public bool DeltaH { get; set; } = true;
        public string LabelHeight { get; set; }
        public string LabelLength { get; set; }
        public string LabelSlope { get; set; }

        /// <summary>pb-scale-text-height: th × (CANNOSCALE / 1000).</summary>
        public double Scaled(double th, double cannoscale)
        {
            if (!Annotative) return th;
            if (cannoscale <= 0.0) cannoscale = 1000.0;
            return th * (cannoscale / 1000.0);
        }

        public double Offset
        {
            get { return TextH * OffsetPct / 100.0; }
        }

        public TableCfg Clone()
        {
            return new TableCfg
            {
                TextH = TextH,
                OffsetPct = OffsetPct,
                RoundTo = RoundTo,
                SlopeRoundTo = SlopeRoundTo,
                SlopeUnit = SlopeUnit,
                ShowPermille = ShowPermille,
                ShowLengths = ShowLengths,
                ShowSlopes = ShowSlopes,
                LabelColW = LabelColW,
                CsTextH = CsTextH,
                Annotative = Annotative,
                AnnGap = AnnGap,
                DrawProjections = DrawProjections,
                ProjectionColor = ProjectionColor,
                UseTextHeight = UseTextHeight,
                DeltaH = DeltaH,
                LabelHeight = LabelHeight,
                LabelLength = LabelLength,
                LabelSlope = LabelSlope
            };
        }

        public TableCfg ForCs()
        {
            return new TableCfg
            {
                TextH = CsTextH > 0.0 ? CsTextH : 0.5,
                OffsetPct = OffsetPct,
                RoundTo = RoundTo,
                SlopeRoundTo = SlopeRoundTo,
                SlopeUnit = SlopeUnit,
                ShowPermille = ShowPermille,
                ShowLengths = ShowLengths,
                ShowSlopes = ShowSlopes,
                LabelColW = LabelColW,
                CsTextH = CsTextH,
                Annotative = Annotative,
                AnnGap = AnnGap,
                DrawProjections = DrawProjections,
                ProjectionColor = ProjectionColor,
                UseTextHeight = UseTextHeight,
                DeltaH = DeltaH,
                LabelHeight = LabelHeight,
                LabelLength = LabelLength,
                LabelSlope = LabelSlope
            };
        }
    }

    /// <summary>
    /// Геометрия таблицы PoperSwift: высота строки, столбец подписей, rtos с нулями.
    /// </summary>
    public static class TableLayout
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Зазор таблицы под низом профиля, м натуры (× h_scale).
        /// Поле «Таблица ниже низа»; отрицательное = 0.
        /// </summary>
        public static double TableBelowM(double tableOffsetM)
        {
            return tableOffsetM < 0.0 ? 0.0 : tableOffsetM;
        }

        public static string Rtos(double val, int precision)
        {
            if (precision <= 0)
                return Math.Round(val, MidpointRounding.AwayFromZero).ToString("0", Inv);
            string fmt = "0." + new string('0', precision);
            return val.ToString(fmt, Inv);
        }

        public static int MaxChars(IList<double> vals, int roundTo, int floor)
        {
            int max = floor;
            if (vals == null) return max;
            for (int i = 0; i < vals.Count; i++)
            {
                int n = Rtos(vals[i], roundTo).Length;
                if (n > max) max = n;
            }
            return max;
        }

        /// <summary>HEIGHT = th*(0.7*chars+0.3); LENGTH то же; SLOPE = th*3; DELTA_H = HEIGHT*1.2.</summary>
        public static double RowHeight(TableRowKind kind, double textH, int maxHeightChars, int maxLengthChars)
        {
            switch (kind)
            {
                case TableRowKind.Height:
                    return textH * (0.7 * maxHeightChars + 0.3);
                case TableRowKind.DeltaH:
                    return textH * (0.7 * maxHeightChars + 0.3) * 1.2;
                case TableRowKind.Length:
                    return textH * (0.7 * maxLengthChars + 0.3);
                case TableRowKind.Slope:
                    return textH * 3.0;
                default:
                    return textH * 1.5;
            }
        }

        public static int MaxLineLen(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int best = 0, tok = 0, i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length && (s[i + 1] == 'P' || s[i + 1] == 'p'))
                {
                    if (tok > best) best = tok;
                    tok = 0;
                    i += 2;
                    continue;
                }
                if (c == '{' || c == '}')
                {
                    i++;
                    continue;
                }
                tok++;
                i++;
            }
            if (tok > best) best = tok;
            return best;
        }

        public static double AutoLabelColW(IList<string> labels, double textH)
        {
            int maxLen = 8;
            if (labels != null)
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    int n = MaxLineLen(labels[i]);
                    if (n > maxLen) maxLen = n;
                }
            }
            return Math.Max(textH * 10.0, textH * maxLen * 0.9);
        }

        public const string CoordNo = "№";

        /// <summary>X/Y в таблице координат — три знака.</summary>
        public static string XyText(double v)
        {
            return v.ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Геодезическая X (север) = AutoCAD Y.
        /// В таблице колонка «X» — как на приборе, не как ось AutoCAD.
        /// </summary>
        public static string CoordX(double acadX, double acadY)
        {
            return XyText(acadY);
        }

        /// <summary>Геодезическая Y (восток) = AutoCAD X.</summary>
        public static string CoordY(double acadX, double acadY)
        {
            return XyText(acadX);
        }

        /// <summary>
        /// Строка № / X / Y. Колонка X — север (AutoCAD Y), колонка Y — восток (AutoCAD X).
        /// </summary>
        public static string[] CoordRow(string name, double acadX, double acadY)
        {
            return new[]
            {
                string.IsNullOrEmpty(name) ? "—" : name,
                CoordX(acadX, acadY),
                CoordY(acadX, acadY)
            };
        }

        /// <summary>Ширина колонки координатной таблицы: высота текста × (знаки + поле).</summary>
        public static double TextColW(double textH, IList<string> cells)
        {
            double th = textH > 1e-9 ? textH : 2.5;
            int maxLen = 1;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    int n = MaxLineLen(cells[i]);
                    if (n > maxLen) maxLen = n;
                }
            }
            return th * (maxLen * 0.7 + 1.6);
        }

        public static double LabelColW(TableCfg cfg, IList<string> labels)
        {
            if (cfg != null && cfg.LabelColW > 0.0) return cfg.LabelColW;
            double th = cfg != null ? cfg.TextH : 2.5;
            return AutoLabelColW(labels, th);
        }

        public static bool RotateLength(double segWidthDraw, double textH)
        {
            return segWidthDraw < 2.0 * textH;
        }

        /// <summary>
        /// Выноска длины, если текст не влазит: только первый интервал влево,
        /// со второго — вправо.
        /// </summary>
        public static bool LengthLeaderToLeft(int spanIndex)
        {
            return spanIndex == 0;
        }

        /// <summary>X полки выноски длины. Первый интервал — xa−th, остальные — xb+th.</summary>
        public static double LengthLeaderTextX(int spanIndex, double xa, double xb, double th)
        {
            if (LengthLeaderToLeft(spanIndex)) return xa - th;
            return xb + th;
        }

        /// <summary>0 в rowRound = брать fallback (глобальное округление таблицы).</summary>
        public static int Prec(int rowRound, int fallback)
        {
            return rowRound > 0 ? rowRound : fallback;
        }

        /// <summary>Уклон как poper-make-slope-text. permille — |ΔH|/L*1000, знак в диагонали.</summary>
        public static string SlopeText(double dz, double len, TableCfg cfg)
        {
            return SlopeText(dz, len, cfg, cfg != null ? cfg.SlopeRoundTo : 1);
        }

        public static string SlopeText(double dz, double len, TableCfg cfg, int precision)
        {
            if (cfg == null) cfg = new TableCfg();
            if (len < 1e-9) return "—";
            double permille = Math.Abs(dz) / len * 1000.0;
            int pr = precision < 0 ? 0 : precision;
            string unit = cfg.SlopeUnit ?? "permille";
            if (unit == "degrees")
            {
                double deg = Math.Atan(Math.Abs(dz) / len) * 180.0 / Math.PI;
                return Rtos(deg, pr) + "°";
            }
            if (unit == "ratio")
            {
                if (permille < 1e-9) return "0";
                return "1:" + Rtos(1000.0 / permille, pr);
            }
            if (unit == "auto" && permille > 150.0)
            {
                if (permille < 1e-9) return "0";
                return "1:" + Rtos(1000.0 / permille, pr);
            }
            string s = Rtos(permille, pr);
            if (cfg.ShowPermille) s += "‰";
            return s;
        }

        public static string SlopeSigned(double dzPerLen, TableCfg cfg)
        {
            if (cfg == null) cfg = new TableCfg();
            double p = dzPerLen * 1000.0;
            string s = Rtos(p, cfg.SlopeRoundTo);
            if (cfg.ShowPermille) s += "‰";
            return s;
        }

        /// <summary>Знаменатель легенды: масштаб листа / коэффициент профиля.</summary>
        public static double LegendDenom(double paperScale, double factor)
        {
            if (paperScale <= 0.0) paperScale = 1000.0;
            if (factor <= 0.0) factor = 1.0;
            return paperScale / factor;
        }

        /// <summary>
        /// Легенда: знаменатели уже из масштаба листа / l_scale и / h_scale.
        /// </summary>
        public static string ScaleLegend(double horizDenom, double vertDenom, double osh)
        {
            return ScaleLegend(horizDenom, vertDenom, osh, null);
        }

        public static string ScaleLegend(double horizDenom, double vertDenom, double osh, double? areaM2)
        {
            return ScaleLegend(horizDenom, vertDenom, osh, areaM2, null);
        }

        public static string ScaleLegend(double horizDenom, double vertDenom, double osh, double? areaM2, string areaLabel)
        {
            string s = "";
            if (areaM2.HasValue && areaM2.Value > 0.0)
            {
                string lbl = string.IsNullOrEmpty(areaLabel) ? "S" : areaLabel;
                s = lbl + " = " + Rtos(areaM2.Value, 2) + " м²\\P";
            }
            return s
                 + "М 1:" + Rtos(horizDenom, 0) + " по горизонтали\\P"
                 + "М 1:" + Rtos(vertDenom, 0) + " по вертикали\\P"
                 + "Условный горизонт " + Rtos(osh, 2) + " м";
        }

        public static int DiamMm(double diam)
        {
            return (int)Math.Floor(diam * 1000.0 + 0.5);
        }

        public static string DiamMark(double diam)
        {
            return "∅" + DiamMm(diam).ToString(Inv);
        }

        /// <summary>MTEXT: ∅ как подстрочный индекс (пустой верх, диаметр снизу).</summary>
        public static string DiamSub(int mm)
        {
            return "{\\H0.7x;\\S^∅" + mm.ToString(Inv) + ";}";
        }

        public static string DiamSub(int mm, string extra)
        {
            if (string.IsNullOrEmpty(extra))
                return DiamSub(mm);
            return "{\\H0.7x;\\S^∅" + mm.ToString(Inv) + " " + extra + ";}";
        }

        public static string SDiam(int mm)
        {
            return "S" + DiamSub(mm);
        }

        public static string SDiam(double diam)
        {
            return SDiam(DiamMm(diam));
        }

        /// <summary>
        /// Интервалы строки «Длина»: нетто трубы и пролёты колодцев
        /// (0→первая стенка, стенка→стенка между трубами, последняя стенка→конец).
        /// </summary>
        public static List<double[]> LengthIntervals(IList<double> netStarts, IList<double> netEnds, double total)
        {
            var result = new List<double[]>();
            if (netStarts == null || netEnds == null) return result;
            int n = Math.Min(netStarts.Count, netEnds.Count);
            double prev = 0.0;
            for (int i = 0; i < n; i++)
            {
                double a = netStarts[i];
                double b = netEnds[i];
                if (a > prev + 1e-6)
                    result.Add(new[] { prev, a });
                if (b > a + 1e-6)
                    result.Add(new[] { a, b });
                if (b > prev) prev = b;
            }
            if (total > prev + 1e-6)
                result.Add(new[] { prev, total });
            return result;
        }
    }
}
