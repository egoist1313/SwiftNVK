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
        public LineStyle CsLine { get; set; } = LineStyle.Of(129, "Continuous", 30);

        public static SheetStyle Pesok()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Of("ANSI31", 5, 1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Of(5, "Continuous", 30),
                Bot = LineStyle.Of(5, "Continuous", 30),
                CsLine = LineStyle.Of(129, "Continuous", 30)
            };
        }

        public static SheetStyle Truba()
        {
            return new SheetStyle
            {
                Hatch = null,
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Rgb(255, 0, 0, "Continuous", 30),
                Bot = LineStyle.Rgb(255, 0, 0, "Continuous", 30),
                CsLine = LineStyle.Of(129, "Continuous", 30)
            };
        }

        public static SheetStyle Zashita()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Of("AR-SAND", 5, 0.1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Of(1, "Continuous", 30),
                Bot = LineStyle.Of(5, "Continuous", 30),
                CsLine = LineStyle.Of(129, "Continuous", 30)
            };
        }

        public static SheetStyle Zasypka()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Of("DOTS", 5, 0.2),
                HatchSand = FillStyle.Of("AR-SAND", 5, 0.1),
                HatchSoil = FillStyle.Of("DOTS", 5, 0.2),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Of(1, "Continuous", 30),
                Bot = LineStyle.Of(5, "Continuous", 30),
                CsLine = LineStyle.Of(129, "Continuous", 30)
            };
        }
    }
}
