using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    public sealed class PlanRotPicker
    {
        [CommandMethod("НВК-ПОВОРОТ-ПЛАНА")]
        public void PickRu() { Pick(); }

        [CommandMethod("NVKPLANROT")]
        public void PickEn() { Pick(); }

        public static void Pick()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                object tm = AcadApp.GetSystemVariable("TILEMODE");
                if (Convert.ToInt16(tm) == 0)
                    AcadApp.SetSystemVariable("TILEMODE", 1);
            }
            catch (Exc) { }
            ed.WriteMessage("\n[НВК] поворот плана: две точки — направление оси X видового.");
            var p1 = ed.GetPoint("\n  первая точка: ");
            if (p1.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[НВК] поворот не задан.");
                return;
            }
            var opt = new PromptPointOptions("\n  вторая точка (ось X): ");
            opt.UseBasePoint = true;
            opt.BasePoint = p1.Value;
            var p2 = ed.GetPoint(opt);
            if (p2.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[НВК] поворот не задан.");
                return;
            }
            double deg = PlanView.AngleDeg(p1.Value.X, p1.Value.Y, p2.Value.X, p2.Value.Y);
            double dx = p2.Value.X - p1.Value.X;
            double dy = p2.Value.Y - p1.Value.Y;
            if (dx * dx + dy * dy < 1e-18)
            {
                ed.WriteMessage("\n[НВК] точки совпали, поворот не изменён.");
                return;
            }
            NvkUiState.PlanRotDeg = deg;
            NvkPalette.SetPlanRot(deg);
            try { NvkPipeline.SaveNod(); } catch (Exc) { }
            ed.WriteMessage("\n[НВК] поворот ПСК " + deg.ToString("0.###") + "°  (Обновить — применить на листах)");
        }
    }
}
