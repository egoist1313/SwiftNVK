namespace Nvk.Core
{
    /// <summary>Отрисовка одного листа профиля L2…L5.</summary>
    public sealed class SheetStyle
    {
        public bool ShowManholes { get; set; } = true;
        public double TableOffsetM { get; set; } = 1.0;
        public FillStyle Hatch { get; set; }
        /// <summary>L5: штриховка песка (ASPH/TROT по умолчанию).</summary>
        public FillStyle HatchSand { get; set; }
        /// <summary>L5: штриховка местного грунта (GAZN по умолчанию).</summary>
        public FillStyle HatchSoil { get; set; }
        public LineStyle Cover { get; set; }
        public LineStyle Top { get; set; }
        public LineStyle Bot { get; set; }
        /// <summary>Вертикальные риски сечений на продольнике. По умолчанию ACI 129.</summary>
        public LineStyle CsLine { get; set; } = LineStyle.Of(129, "Continuous", 15);

        public static SheetStyle Pesok()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("ANSI31", 0, 0, 255, 1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 15),
                Top = LineStyle.Rgb(0, 0, 255, "Continuous", 15),
                Bot = LineStyle.Rgb(0, 0, 255, "Continuous", 15),
                CsLine = LineStyle.Of(129, "Continuous", 15)
            };
        }

        public static SheetStyle Truba()
        {
            return new SheetStyle
            {
                Hatch = null,
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 15),
                Top = LineStyle.Rgb(255, 0, 0, "Continuous", 15),
                Bot = LineStyle.Rgb(255, 0, 0, "Continuous", 15),
                CsLine = LineStyle.Of(129, "Continuous", 15)
            };
        }

        public static SheetStyle Zashita()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("AR-SAND", 0, 0, 255, 0.1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 15),
                Top = LineStyle.Rgb(255, 0, 0, "Continuous", 15),
                Bot = LineStyle.Rgb(0, 0, 255, "Continuous", 15),
                CsLine = LineStyle.Of(129, "Continuous", 15)
            };
        }

        public static SheetStyle Zasypka()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("DOTS", 0, 0, 255, 0.2),
                HatchSand = FillStyle.Rgb("AR-SAND", 0, 0, 255, 0.1),
                HatchSoil = FillStyle.Rgb("DOTS", 0, 0, 255, 0.2),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 15),
                Top = LineStyle.Rgb(255, 0, 0, "Continuous", 15),
                Bot = LineStyle.Rgb(255, 0, 0, "Continuous", 15),
                CsLine = LineStyle.Of(129, "Continuous", 15)
            };
        }
    }
}
