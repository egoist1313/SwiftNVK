namespace Nvk.Core
{
    /// <summary>
    /// Именованные линии участка. Считаются один раз из трубы + TIN
    /// и переезжают из профиля в профиль / из уровня сечения в уровень.
    /// </summary>
    public enum NamedLine
    {
        /// <summary>invert − wall − bedding. Дно траншеи. Низ разработки и низ песка.</summary>
        TrenchBottom = 0,

        /// <summary>invert − wall. Верх песка = низ оболочки трубы. Низ защиты.</summary>
        SandTop = 1,

        /// <summary>invert + diam + wall. Верх оболочки трубы.</summary>
        PipeTop = 2,

        /// <summary>invert + diam + wall + thk_zash. Верх защиты. Низ засыпки.</summary>
        ZashTop = 3,

        /// <summary>TIN минус одежда. Верх разработки и верх засыпки.</summary>
        Surface = 4
    }
}
