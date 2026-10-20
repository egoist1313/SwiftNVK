namespace Nvk.Core
{
    public sealed class LineStyle
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        /// <summary>Старый ACI; если &gt;0 и RGB=0 — брать его.</summary>
        public short Color { get; set; }
        public string Linetype { get; set; } = "Continuous";
        public int Weight { get; set; } = 15;

        public static LineStyle Of(short aci, string ltype, int weight)
        {
            return new LineStyle { Color = aci, Linetype = ltype ?? "Continuous", Weight = weight };
        }

        public static LineStyle Rgb(byte r, byte g, byte b, string ltype, int weight)
        {
            return new LineStyle { R = r, G = g, B = b, Color = 0, Linetype = ltype ?? "Continuous", Weight = weight };
        }
    }

    public sealed class FillStyle
    {
        public byte R { get; set; } = 128;
        public byte G { get; set; } = 128;
        public byte B { get; set; } = 128;
        public short Color { get; set; }
        public string Pattern { get; set; } = "ANSI31";
        public double Scale { get; set; } = 1.0;

        public static FillStyle Of(string pat, short aci, double scale)
        {
            return new FillStyle { Pattern = pat ?? "ANSI31", Color = aci, Scale = scale };
        }

        public static FillStyle Rgb(string pat, byte r, byte g, byte b, double scale)
        {
            return new FillStyle { Pattern = pat ?? "ANSI31", R = r, G = g, B = b, Color = 0, Scale = scale };
        }

        public FillStyle Clone()
        {
            return new FillStyle
            {
                Pattern = Pattern ?? "ANSI31",
                Color = Color,
                R = R,
                G = G,
                B = B,
                Scale = Scale
            };
        }
    }

    /// <summary>Настройки уровня L1 (разработка). Файл пресета — JSON в presets/l1.</summary>
    public sealed class L1Style
    {
        public string Name { get; set; } = "default";

        public double ThkAsph { get; set; } = 1.02;
        public double ThkTrot { get; set; } = 0.31;
        public double ThkGazn { get; set; } = 0.15;
        public double ThkOther { get; set; } = 0.35;
        public string Zones { get; set; } = "";
        public bool UseBlackEarth { get; set; }
        public string BlackEarthHandle { get; set; } = "";
        public string BlackEarthName { get; set; } = "";
        public string DesignHandle { get; set; } = "";
        public string DesignName { get; set; } = "";

        public bool Scatter { get; set; } = true;
        public double ScatterAmp { get; set; } = 0.05;

        public bool ShowManholes { get; set; } = true;

        /// <summary>Таблица ниже низа профиля, метров натуры (× h_scale на чертеже).</summary>
        public double TableOffsetM { get; set; } = 1.0;

        public string LabelRoad { get; set; } = "Проектно-планировочное\\Pпокрытие, м";
        public string LabelEarth { get; set; } = ProfileRows.EarthLabel;
        public string LabelBottom { get; set; } = "Отметка дна\\Pтраншеи, м";
        public string LabelLength { get; set; } = "Длина, м";
        public string LabelMh { get; set; } = "Колодец Ø / дно, м";

        public LineStyle Road { get; set; } = LineStyle.Rgb(255, 0, 0, "DASHED", 15);
        public LineStyle Earth { get; set; } = LineStyle.Rgb(0, 0, 255, "Continuous", 15);
        public LineStyle Bottom { get; set; } = LineStyle.Rgb(255, 0, 0, "Continuous", 15);
        /// <summary>Вертикальные риски сечений на продольнике L1. По умолчанию ACI 129.</summary>
        public LineStyle CsLine { get; set; } = LineStyle.Of(129, "Continuous", 15);

        public FillStyle HatchExcavation { get; set; } = FillStyle.Rgb("ANSI31", 0, 0, 255, 1.0);
        public FillStyle HatchPavement { get; set; } = FillStyle.Rgb("ANSI31", 128, 128, 128, 1.0);

        public static L1Style Default()
        {
            return new L1Style();
        }
    }
}
