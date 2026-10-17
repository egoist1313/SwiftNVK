using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    public enum HeightSrc
    {
        TrenchBottom,
        SandTop,
        Invert,
        PipeTop,
        ZashTop,
        Surface,
        RoadTop,
        BlackEarth,
        DeltaZash
    }

    public enum SheetRowKind
    {
        Height,
        Length,
        Slope,
        Diam,
        Manhole
    }

    public sealed class SheetRow
    {
        public SheetRowKind Kind    { get; set; }
        public HeightSrc    Src     { get; set; }
        public string       Label   { get; set; }
        public bool         Show    { get; set; }
        public bool         IsFact  { get; set; }
        public int          RoundTo { get; set; }   // 0 = использовать TableCfg.RoundTo

        public static SheetRow H(HeightSrc src, string label)
        {
            return new SheetRow { Kind = SheetRowKind.Height, Src = src, Label = label, Show = true };
        }

        public static SheetRow Len(string label)
        {
            return new SheetRow { Kind = SheetRowKind.Length, Label = label, Show = true };
        }

        public static SheetRow Slp(string label)
        {
            return new SheetRow { Kind = SheetRowKind.Slope, Label = label, Show = true };
        }

        public static SheetRow Diam()
        {
            return new SheetRow { Kind = SheetRowKind.Diam, Label = "%%c трубопровода", Show = true };
        }

        public static SheetRow Mh()
        {
            return new SheetRow { Kind = SheetRowKind.Manhole, Label = "Номер колодца, точки,\\Pуглы поворота", Show = true };
        }
    }

    public static class ProfileRows
    {
        public const string EarthLabel = "Отметка земли, м";

        public static string MigrateEarthLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return EarthLabel;
            string t = s.Replace("\\P", " ").Replace("\n", " ");
            if (t.IndexOf("черн", StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthLabel;
            return s;
        }

        public static List<SheetRow> ForL1(L1Style st, TableCfg tbl)
        {
            var rows = For(ProfileKind.Razrabotka, tbl);
            if (st == null) return rows;
            foreach (var r in rows)
            {
                if (r.Src == HeightSrc.RoadTop && !string.IsNullOrEmpty(st.LabelRoad))
                    r.Label = st.LabelRoad;
                if (r.Src == HeightSrc.BlackEarth)
                    r.Label = MigrateEarthLabel(st.LabelEarth);
                if (r.Src == HeightSrc.TrenchBottom && !string.IsNullOrEmpty(st.LabelBottom))
                    r.Label = st.LabelBottom;
                if (r.Kind == SheetRowKind.Length && !string.IsNullOrEmpty(st.LabelLength))
                    r.Label = st.LabelLength;
                if (r.Kind == SheetRowKind.Manhole)
                {
                    r.Show = st.ShowManholes;
                    if (!string.IsNullOrEmpty(st.LabelMh)) r.Label = st.LabelMh;
                }
            }
            return rows;
        }

        public static List<SheetRow> For(ProfileKind kind, TableCfg tbl)
        {
            var rows = For(kind);
            if (tbl == null) return rows;
            foreach (var r in rows)
            {
                if (r.Kind == SheetRowKind.Height && !string.IsNullOrEmpty(tbl.LabelHeight)
                    && r.Src != HeightSrc.DeltaZash && r.Src != HeightSrc.RoadTop
                    && r.Src != HeightSrc.BlackEarth)
                    r.Label = tbl.LabelHeight;
                if (r.Kind == SheetRowKind.Length && !string.IsNullOrEmpty(tbl.LabelLength))
                    r.Label = tbl.LabelLength;
                if (r.Kind == SheetRowKind.Slope && !string.IsNullOrEmpty(tbl.LabelSlope))
                    r.Label = tbl.LabelSlope;
                if (r.Kind == SheetRowKind.Length) r.Show = tbl.ShowLengths;
                if (r.Kind == SheetRowKind.Slope) r.Show = tbl.ShowSlopes;
                if (r.Src == HeightSrc.DeltaZash) r.Show = tbl.DeltaH;
            }
            return rows;
        }

        public static List<SheetRow> For(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka:
                    {
                        var mh = SheetRow.Mh();
                        mh.Show = true;
                        return new List<SheetRow>
                        {
                            SheetRow.H(HeightSrc.RoadTop, "Проектно-планировочное\\Pпокрытие, м"),
                            SheetRow.H(HeightSrc.BlackEarth, EarthLabel),
                            SheetRow.H(HeightSrc.TrenchBottom, "Отметка дна\\Pтраншеи, м"),
                            SheetRow.Len("Длина, м"),
                            SheetRow.Diam(),
                            mh
                        };
                    }
                case ProfileKind.Pesok:
                    return new List<SheetRow>
                    {
                        SheetRow.H(HeightSrc.RoadTop, "Проектно-планировочное\\Pпокрытие, м"),
                        SheetRow.H(HeightSrc.SandTop, "Отметка верха\\Pпостели из песка, м"),
                        SheetRow.H(HeightSrc.TrenchBottom, "Отметка дна\\Pтраншеи, м"),
                        SheetRow.Len("Длина, м"),
                        SheetRow.Diam(),
                        SheetRow.Mh()
                    };
                case ProfileKind.Truba:
                    return new List<SheetRow>
                    {
                        SheetRow.H(HeightSrc.RoadTop, "Проектно-планировочное\\Pпокрытие, м"),
                        SheetRow.Slp("Уклон, ‰"),
                        SheetRow.H(HeightSrc.Invert, "Отметка низа\\Pили лотка трубы, м"),
                        SheetRow.Len("Длина, м"),
                        SheetRow.Diam(),
                        SheetRow.Mh()
                    };
                case ProfileKind.Zashita:
                    return new List<SheetRow>
                    {
                        SheetRow.H(HeightSrc.DeltaZash, "Толщина защитного слоя, м"),
                        SheetRow.H(HeightSrc.ZashTop, "Отметка верха\\Pзащитного слоя, м"),
                        SheetRow.H(HeightSrc.SandTop, "Отметка верха\\Pпостели из песка, м"),
                        SheetRow.Len("Длина, м"),
                        SheetRow.Diam(),
                        SheetRow.Mh()
                    };
                case ProfileKind.Zasypka:
                    return new List<SheetRow>
                    {
                        SheetRow.H(HeightSrc.Surface, "Отметка обратной\\Pзасыпки, м"),
                        SheetRow.H(HeightSrc.ZashTop, "Отметка верха\\Pзащитного слоя, м"),
                        SheetRow.Len("Длина, м"),
                        SheetRow.Diam(),
                        SheetRow.Mh()
                    };
                default:
                    return new List<SheetRow> { SheetRow.Len("Длина, м"), SheetRow.Mh() };
            }
        }

        /// <summary>Собрать список SheetRow из явной конфигурации строк таблицы.</summary>
        public static List<SheetRow> FromDefs(List<TableRowDef> defs)
        {
            var rows = new List<SheetRow>();
            if (defs == null) return rows;
            foreach (var d in defs)
            {
                if (d.Type == SheetRowKind.Height && d.IsProjFact)
                {
                    // Пара: «, проект» читает чистую линию; «, факт» — линию с рандомом, если он включён на вкладке.
                    string lab = d.Src == HeightSrc.BlackEarth ? MigrateEarthLabel(d.Label) : (d.Label ?? "");
                    var rProj = SheetRow.H(d.Src, lab + ", проект");
                    rProj.Show = d.Show; rProj.RoundTo = d.RoundTo; rProj.IsFact = false;
                    rows.Add(rProj);
                    var rFact = SheetRow.H(d.Src, lab + ", факт");
                    rFact.Show = d.Show; rFact.RoundTo = d.RoundTo; rFact.IsFact = true;
                    rows.Add(rFact);
                    continue;
                }

                SheetRow r;
                switch (d.Type)
                {
                    case SheetRowKind.Height:
                        r = SheetRow.H(d.Src, d.Src == HeightSrc.BlackEarth
                            ? MigrateEarthLabel(d.Label) : (d.Label ?? ""));
                        r.IsFact = d.IsFact || d.UseScatter;
                        break;
                    case SheetRowKind.Length:
                        r = SheetRow.Len(d.Label ?? "Длина, м");
                        break;
                    case SheetRowKind.Slope:
                        r = SheetRow.Slp(d.Label ?? "Уклон, ‰");
                        break;
                    case SheetRowKind.Diam:
                        r = SheetRow.Diam();
                        if (!string.IsNullOrEmpty(d.Label)) r.Label = d.Label;
                        break;
                    case SheetRowKind.Manhole:
                        r = SheetRow.Mh();
                        if (!string.IsNullOrEmpty(d.Label)) r.Label = d.Label;
                        break;
                    default:
                        continue;
                }
                r.Show    = d.Show;
                r.RoundTo = d.RoundTo;
                rows.Add(r);
            }
            return rows;
        }

        public static double ZAtCh(IList<AxisPt> axis, HeightSrc src, SectionConfig cfg, double ch)
        {
            if (axis == null || axis.Count == 0) return 0.0;
            if (ch <= axis[0].Ch) return ZAt(src, axis[0], cfg);
            int last = axis.Count - 1;
            if (ch >= axis[last].Ch) return ZAt(src, axis[last], cfg);
            for (int i = 0; i < last; i++)
            {
                double a = axis[i].Ch, b = axis[i + 1].Ch;
                if (ch < a - 1e-9 || ch > b + 1e-9) continue;
                double d = b - a;
                double u = d < 1e-12 ? 0.0 : (ch - a) / d;
                return ZAt(src, axis[i], cfg) * (1.0 - u) + ZAt(src, axis[i + 1], cfg) * u;
            }
            return ZAt(src, axis[last], cfg);
        }

        public static double ZAt(HeightSrc src, AxisPt p, SectionConfig cfg)
        {
            if (p == null) return 0.0;
            if (cfg == null) cfg = new SectionConfig();
            switch (src)
            {
                case HeightSrc.TrenchBottom: return p.NamedZ(NamedLine.TrenchBottom, cfg);
                case HeightSrc.SandTop: return p.NamedZ(NamedLine.SandTop, cfg);
                case HeightSrc.Invert: return p.Invert;
                case HeightSrc.PipeTop: return p.NamedZ(NamedLine.PipeTop, cfg);
                case HeightSrc.ZashTop: return p.NamedZ(NamedLine.ZashTop, cfg);
                case HeightSrc.Surface: return p.NamedZ(NamedLine.Surface, cfg);
                case HeightSrc.BlackEarth:
                    return p.NamedZ(NamedLine.Surface, cfg);
                case HeightSrc.RoadTop:
                    {
                        // L0 = TIN как есть. Не поднимать к L1/L6 — иначе одежда съезжает с заданной толщины.
                        double earth = p.NamedZ(NamedLine.Surface, cfg);
                        if (p.DesignZ.HasValue) return p.DesignZ.Value;
                        return earth + cfg.PavementAt(p.Ch);
                    }
                case HeightSrc.DeltaZash:
                    return DeltaZashThickness(
                        p.NamedZ(NamedLine.ZashTop, cfg),
                        p.NamedZ(NamedLine.SandTop, cfg),
                        p.Diam);
                default: return p.Invert;
            }
        }

        /// <summary>Толщина защитного слоя: L4 − L2 − диаметр трубы. Отрицательное → 0.</summary>
        public static double DeltaZashThickness(double zashTop, double sandTop, double diam)
        {
            double t = zashTop - sandTop - diam;
            return t < 0.0 ? 0.0 : t;
        }
    }
}
