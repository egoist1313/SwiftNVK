using Autodesk.AutoCAD.Colors;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace Nvk.Cad
{
    static class CadColor
    {
        public static AcadColor Of(LineStyle st)
        {
            if (st == null) return Aci(7);
            if (st.Color > 0 && st.R == 0 && st.G == 0 && st.B == 0)
                return Aci(st.Color);
            return AcadColor.FromRgb(st.R, st.G, st.B);
        }

        public static AcadColor Of(FillStyle st)
        {
            if (st == null) return Aci(5);
            if (st.Color > 0 && st.R == 0 && st.G == 0 && st.B == 0)
                return Aci(st.Color);
            return AcadColor.FromRgb(st.R, st.G, st.B);
        }

        public static AcadColor Aci(short i)
        {
            return AcadColor.FromColorIndex(ColorMethod.ByAci, i);
        }

        /// <summary>Цвет вертикальных рисок сечения на продольнике этого уровня.</summary>
        public static AcadColor CsMark(ProfileKind kind)
        {
            LineStyle st = null;
            if (kind == ProfileKind.Razrabotka)
                st = NvkSession.L1 != null ? NvkSession.L1.CsLine : null;
            else
            {
                var sh = NvkSession.Sheet(kind);
                st = sh != null ? sh.CsLine : null;
            }
            return st != null ? Of(st) : Aci(129);
        }
    }
}
