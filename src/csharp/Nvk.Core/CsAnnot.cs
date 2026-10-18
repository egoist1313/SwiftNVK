using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nvk.Core
{
    public sealed class CsTableRow
    {
        public string Label { get; set; }
        public double? C1 { get; set; }
        public double? C2 { get; set; }
        public double? C3 { get; set; }
        public double? C4 { get; set; }
        public string Center { get; set; }
        /// <summary>true = средние столбцы у бровок (±xTop), как тег 'surf в PipeBuild.</summary>
        public bool IsSurf { get; set; }
    }

    /// <summary>
    /// Подписи поперечника: заголовок, таблица отметок, расстояния.
    /// Без графики — CsDrawer только рисует.
    /// </summary>
    public static class CsAnnot
    {
        public static double TableHeight(SectionGeom g, ProfileKind kind, double hw, double mSlope, TableCfg tbl)
        {
            return TableHeight(g, kind, hw, mSlope, mSlope, tbl, null);
        }

        public static double TableHeight(SectionGeom g, ProfileKind kind, double hw, double mSlope, TableCfg tbl, SectionConfig cfg)
        {
            return TableHeight(g, kind, hw, mSlope, mSlope, tbl, cfg);
        }

        public static double TableHeight(SectionGeom g, ProfileKind kind, double hw, double mLeft, double mRight, TableCfg tbl, SectionConfig cfg)
        {
            if (tbl == null) tbl = new TableCfg();
            var rows = Rows(g, kind, hw, mLeft, mRight, tbl, cfg);
            var zs = new List<double>();
            foreach (var r in rows)
            {
                if (r.C1.HasValue) zs.Add(r.C1.Value);
                if (r.C2.HasValue) zs.Add(r.C2.Value);
                if (r.C3.HasValue) zs.Add(r.C3.Value);
                if (r.C4.HasValue) zs.Add(r.C4.Value);
            }
            var gaps = Gaps(g, kind, hw, mLeft, mRight);
            // PipeBuild: HEIGHT всегда 7 знаков, LENGTH — 4 (poper-calc-row-height).
            int maxH = TableLayout.MaxChars(zs, kind == ProfileKind.Truba ? 3 : tbl.RoundTo, 7);
            int maxL = TableLayout.MaxChars(gaps, 2, 4);
            double h = 0.0;
            foreach (var r in rows)
                h += TableLayout.RowHeight(TableRowKind.Height, tbl.TextH, maxH, maxL);
            h += TableLayout.RowHeight(TableRowKind.Length, tbl.TextH, maxH, maxL);
            return h;
        }

        public static string Header(int idx, ProfileKind kind, double station)
        {
            return SectionMark(idx);
        }

        public static string KindShort(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return "L1 Разработка";
                case ProfileKind.Pesok:      return "L2 Песок";
                case ProfileKind.Truba:      return "L3 Труба";
                case ProfileKind.Zashita:    return "L4 Защита";
                case ProfileKind.Zasypka:    return "L5 Засыпка";
                default: return kind.ToString();
            }
        }

        public static string LineTag(HeightSrc src)
        {
            switch (src)
            {
                case HeightSrc.RoadTop: return "L0";
                case HeightSrc.TrenchBottom: return "L1";
                case HeightSrc.SandTop: return "L2";
                case HeightSrc.Invert: return "L3лот";
                case HeightSrc.PipeTop: return "L3верх";
                case HeightSrc.ZashTop: return "L4";
                case HeightSrc.Surface:
                case HeightSrc.BlackEarth: return "L6";
                default: return "";
            }
        }

        public static string DiamText(double diam)
        {
            return TableLayout.DiamMark(diam);
        }

        public static string FmtZ(double z)
        {
            return z.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string FmtM(double m)
        {
            return m.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string FmtSlope(double slope)
        {
            return (slope * 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "‰";
        }

        public static string SectionMark(int idx)
        {
            int n = idx + 1;
            return "Сечение " + n.ToString(CultureInfo.InvariantCulture)
                 + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Полуширина внутренних колонок: верх текущего слоя.</summary>
        public static double XInner(SectionGeom g, ProfileKind kind, double mSlope)
        {
            return XInnerLeft(g, kind, mSlope);
        }

        public static double XInnerLeft(SectionGeom g, ProfileKind kind, double mSlope)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return TrenchGeom.BottomLeft(g);
                case ProfileKind.Pesok:      return TrenchGeom.XAtLeft(g, mSlope, g.Elev.SandTop);
                case ProfileKind.Truba:      return TrenchGeom.XAtLeft(g, mSlope, g.Elev.PipeTop);
                case ProfileKind.Zashita:    return TrenchGeom.XAtLeft(g, mSlope, g.Elev.ZashTop);
                case ProfileKind.Zasypka:    return TrenchGeom.XTopLeft(g, mSlope);
                default: return TrenchGeom.BottomLeft(g);
            }
        }

        public static double XInnerRight(SectionGeom g, ProfileKind kind, double mSlope)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return TrenchGeom.BottomRight(g);
                case ProfileKind.Pesok:      return TrenchGeom.XAtRight(g, mSlope, g.Elev.SandTop);
                case ProfileKind.Truba:      return TrenchGeom.XAtRight(g, mSlope, g.Elev.PipeTop);
                case ProfileKind.Zashita:    return TrenchGeom.XAtRight(g, mSlope, g.Elev.ZashTop);
                case ProfileKind.Zasypka:    return TrenchGeom.XTopRight(g, mSlope);
                default: return TrenchGeom.BottomRight(g);
            }
        }

        public static List<CsTableRow> Rows(SectionGeom g, ProfileKind kind, double hw, double mSlope)
        {
            return Rows(g, kind, hw, mSlope, mSlope, null, null);
        }

        public static List<CsTableRow> Rows(SectionGeom g, ProfileKind kind, double hw, double mSlope, TableCfg tbl)
        {
            return Rows(g, kind, hw, mSlope, mSlope, tbl, null);
        }

        public static List<CsTableRow> Rows(SectionGeom g, ProfileKind kind, double hw, double mSlope, TableCfg tbl, SectionConfig cfg)
        {
            return Rows(g, kind, hw, mSlope, mSlope, tbl, cfg);
        }

        public static List<CsTableRow> Rows(SectionGeom g, ProfileKind kind, double hw, double mLeft, double mRight, TableCfg tbl, SectionConfig cfg)
        {
            var e = g.Elev;
            // L6 у края стенки траншеи (бровка), не у дна.
            double xSurfL = Math.Min(hw, TrenchGeom.XTopLeft(g, mLeft));
            double xSurfR = Math.Min(hw, TrenchGeom.XTopRight(g, mRight));

            var rows = new List<CsTableRow>();
            if (tbl == null) tbl = new TableCfg();
            bool pf = cfg != null && cfg.ProjFact && !cfg.SingleScatter;
            rows.Add(new CsTableRow
            {
                Label = "Отметка земли, м",
                C1 = TrenchGeom.SurfZAt(g, -hw),
                C2 = TrenchGeom.SurfZAt(g, -xSurfL),
                C3 = TrenchGeom.SurfZAt(g, xSurfR),
                C4 = TrenchGeom.SurfZAt(g, hw),
                IsSurf = true
            });

            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    rows.Add(Mid(pf ? "Отметка дна\\Pтраншеи, проект, м" : "Отметка дна\\Pтраншеи, м", e.TrenchBottom));
                    if (pf)
                        rows.Add(Mid("Отметка дна\\Pтраншеи, факт, м", g.FactBottom ?? e.TrenchBottom));
                    break;
                case ProfileKind.Pesok:
                    rows.Add(Mid(pf ? "Отметка верха\\Pпостели из песка, проект, м" : "Отметка верха\\Pпостели из песка, м", e.SandTop));
                    if (pf)
                        rows.Add(Mid("Отметка верха\\Pпостели из песка, факт, м", g.FactSand ?? e.SandTop));
                    rows.Add(Mid(pf ? "Отметка дна\\Pтраншеи, проект, м" : "Отметка дна\\Pтраншеи, м", e.TrenchBottom));
                    if (pf)
                        rows.Add(Mid("Отметка дна\\Pтраншеи, факт, м", g.FactPesokBot ?? g.FactBottom ?? e.TrenchBottom));
                    break;
                case ProfileKind.Truba:
                    rows.Add(Mid(pf ? "Отметка низа\\Pили лотка трубы, проект, м" : "Отметка низа\\Pили лотка трубы, м", e.Invert));
                    if (pf)
                        rows.Add(Mid("Отметка низа\\Pили лотка трубы, факт, м", g.FactInvert ?? e.Invert));
                    break;
                case ProfileKind.Zashita:
                    rows.Add(Mid(pf ? "Отметка верха\\Pзащитного слоя, проект, м" : "Отметка верха\\Pзащитного слоя, м", e.ZashTop));
                    if (pf)
                        rows.Add(Mid("Отметка верха\\Pзащитного слоя, факт, м", g.FactZash ?? e.ZashTop));
                    if (string.Equals(g.ZoneType, "ASPH", StringComparison.OrdinalIgnoreCase))
                        rows.Add(Mid("Подошва дорожной\\Pодежды (низ песка), м", e.Surface));
                    rows.Add(Mid(pf ? "Отметка верха\\Pпостели из песка, проект, м" : "Отметка верха\\Pпостели из песка, м", e.SandTop));
                    if (pf)
                        rows.Add(Mid("Отметка верха\\Pпостели из песка, факт, м", g.FactSand ?? e.SandTop));
                    break;
                case ProfileKind.Zasypka:
                    if (e.HasBackfill)
                    {
                        rows.Add(Mid(pf ? "Отметка обратной\\Pзасыпки, проект, м" : "Отметка обратной\\Pзасыпки, м", e.Surface));
                        if (pf)
                            rows.Add(Mid("Отметка обратной\\Pзасыпки, факт, м", g.FactSurf ?? e.Surface));
                        rows.Add(Mid(pf ? "Отметка верха\\Pзащитного слоя, проект, м" : "Отметка верха\\Pзащитного слоя, м", e.ZashTop));
                        if (pf)
                            rows.Add(Mid("Отметка верха\\Pзащитного слоя, факт, м", g.FactZash ?? e.ZashTop));
                    }
                    break;
            }
            return rows;
        }

        /// <summary>Ширины трёх пролётов таблицы, м: край→откос, дно, откос→край.</summary>
        public static double[] Gaps(SectionGeom g, ProfileKind kind, double hw, double mSlope)
        {
            return Gaps(g, kind, hw, mSlope, mSlope);
        }

        public static double[] Gaps(SectionGeom g, ProfileKind kind, double hw, double mLeft, double mRight)
        {
            double xL = XInnerLeft(g, kind, mLeft);
            double xR = XInnerRight(g, kind, mRight);
            if (xL > hw) xL = hw;
            if (xR > hw) xR = hw;
            return new[] { hw - xL, xL + xR, hw - xR };
        }

        /// <summary>Метка площади: S с ∅ снизу, тот же MTEXT что на продольнике.</summary>
        public static string AreaLabel(ProfileKind kind, double diam)
        {
            return TableLayout.SDiam(diam);
        }

        public static string AreaLabel(ProfileKind kind, double diam, SectionGeom g, SectionConfig cfg)
        {
            if (kind == ProfileKind.Pesok)
                return "Sпеска";
            if (kind != ProfileKind.Zasypka)
                return AreaLabel(kind, diam);
            var fill = BackfillMap.Of(g != null ? g.ZoneType : null, cfg);
            return fill == BackfillKind.Sand ? "Sпеска" : "Sместн.грунта";
        }

        /// <summary>Самые длинные подписи столбца, чтобы шаг сетки не был уже таблицы.</summary>
        public static List<string> LayoutLabels()
        {
            return new List<string>
            {
                "Отметка земли, м",
                "Отметка дна\\Pтраншеи, проект, м",
                "Отметка верха\\Pпостели из песка, проект, м",
                "Отметка низа\\Pили лотка трубы, проект, м",
                "Отметка верха\\Pзащитного слоя, проект, м",
                "Отметка обратной\\Pзасыпки, проект, м",
                "Подошва дорожной\\Pодежды (низ песка), м",
                "Расстояние, м"
            };
        }

        /// <summary>
        /// Шаг по X между центрами сечений: таблица слева + рамка ±hw·sc + размеры/L-теги справа + зазор.
        /// </summary>
        public static double ColGap(double hw, double scale, double th, double labW)
        {
            if (th < 0.05) th = 0.05;
            if (hw < 0.0) hw = 0.0;
            if (scale < 0.0) scale = 0.0;
            if (labW < 0.0) labW = 0.0;
            double left = hw * scale + th + labW;
            double right = hw * scale + 5.0 * th;
            double gap = 6.0 * th;
            if (gap < 2.0) gap = 2.0;
            return left + right + gap;
        }

        public static int MaxPerRow(double rowWidth, double slot)
        {
            if (slot < 1e-6) return 1;
            int n = (int)Math.Floor(rowWidth / slot);
            return n < 1 ? 1 : n;
        }

        /// <summary>
        /// Сечений в ряду. specified&gt;0 — как в NOD/панели (43-44-46 = 2).
        /// Иначе по длине продольника: короткий участок даёт 2, длинный — один ряд.
        /// </summary>
        public static int PerRow(int specified, double rowWidth, double slot)
        {
            if (specified > 0) return specified;
            return MaxPerRow(rowWidth, slot);
        }

        public static void Grid(int index, int maxPerRow, out int col, out int row)
        {
            if (maxPerRow < 1) maxPerRow = 1;
            if (index < 0) index = 0;
            col = index % maxPerRow;
            row = index / maxPerRow;
        }

        /// <summary>
        /// Зазор по X между вертикальными «Сечение N-N»: повёрнутый на 90° текст
        /// занимает по горизонтали высоту шрифта.
        /// </summary>
        public static double MarkGap(double th)
        {
            if (th < 0.05) th = 0.05;
            return th;
        }

        /// <summary>
        /// X подписей: линия остаётся на станции, текст уезжает вправо если ближе MarkGap.
        /// </summary>
        public static List<double> PlaceMarks(IList<double> stationX, double th)
        {
            var xs = new List<double>();
            if (stationX == null || stationX.Count == 0) return xs;
            double gap = MarkGap(th);
            double prev = double.NegativeInfinity;
            for (int i = 0; i < stationX.Count; i++)
            {
                double x = stationX[i];
                if (x < prev + gap)
                    x = prev + gap;
                xs.Add(x);
                prev = x;
            }
            return xs;
        }

        static CsTableRow Mid(string label, double z)
        {
            return new CsTableRow { Label = label, C2 = z, C3 = z };
        }
    }
}
