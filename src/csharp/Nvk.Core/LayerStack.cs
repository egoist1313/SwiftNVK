using System;

namespace Nvk.Core
{
    /// <summary>
    /// Пара нижней и верхней линии для профиля / уровня сечения.
    /// Нижняя линия следующего слоя — это верхняя (или та же нижняя) предыдущего:
    ///   разработка.дно → песок.дно
    ///   песок.верх     → защита.низ
    ///   защита.верх    → засыпка.низ
    /// </summary>
    public static class LayerStack
    {
        public static NamedLine Lower(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return NamedLine.TrenchBottom;
                case ProfileKind.Pesok:      return NamedLine.TrenchBottom;
                case ProfileKind.Truba:      return NamedLine.SandTop;
                case ProfileKind.Zashita:    return NamedLine.SandTop;
                case ProfileKind.Zasypka:    return NamedLine.ZashTop;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static NamedLine Upper(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return NamedLine.Surface;
                case ProfileKind.Pesok:      return NamedLine.SandTop;
                case ProfileKind.Truba:      return NamedLine.PipeTop;
                case ProfileKind.Zashita:    return NamedLine.ZashTop;
                case ProfileKind.Zasypka:    return NamedLine.Surface;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static bool IsDrawn(ProfileKind kind, StationElevations elev)
        {
            if (kind == ProfileKind.Zasypka)
                return elev.HasBackfill;
            return true;
        }

        /// <summary>
        /// Вертикальный зазор слоя в метрах. 0 если слой не рисуется (засыпка при Surface ≤ ZashTop).
        /// </summary>
        public static double Thickness(ProfileKind kind, StationElevations elev)
        {
            if (!IsDrawn(kind, elev))
                return 0.0;
            double t = elev[Upper(kind)] - elev[Lower(kind)];
            return t > 0.0 ? t : 0.0;
        }
    }
}
