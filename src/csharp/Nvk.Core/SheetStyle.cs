namespace Nvk.Core
{
    /// <summary>Отрисовка одного листа профиля L2…L5.</summary>
    public sealed class SheetStyle
    {
        public bool ShowManholes { get; set; } = true;
        public double TableOffsetM { get; set; } = 1.0;
        public FillStyle Hatch { get; set; }
        public LineStyle Cover { get; set; }
        public LineStyle Top { get; set; }
        public LineStyle Bot { get; set; }

        public static SheetStyle Pesok()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("ANSI31", 220, 200, 80, 1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Rgb(255, 200, 0, "Continuous", 30),
                Bot = LineStyle.Rgb(255, 0, 0, "Continuous", 30)
            };
        }

        public static SheetStyle Truba()
        {
            return new SheetStyle
            {
                Hatch = null,
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Rgb(255, 0, 0, "Continuous", 30),
                Bot = LineStyle.Rgb(255, 0, 0, "Continuous", 30)
            };
        }

        public static SheetStyle Zashita()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("AR-SAND", 210, 180, 80, 1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Rgb(0, 160, 80, "Continuous", 30),
                Bot = LineStyle.Rgb(255, 200, 0, "Continuous", 30)
            };
        }

        public static SheetStyle Zasypka()
        {
            return new SheetStyle
            {
                Hatch = FillStyle.Rgb("DOTS", 140, 140, 140, 1),
                Cover = LineStyle.Rgb(255, 0, 0, "DASHED", 18),
                Top = LineStyle.Rgb(0, 180, 80, "Continuous", 30),
                Bot = LineStyle.Rgb(0, 160, 80, "Continuous", 30)
            };
        }
    }
}
