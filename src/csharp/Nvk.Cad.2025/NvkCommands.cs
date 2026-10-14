using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Nvk.Cad.NvkCommands))]

namespace Nvk.Cad
{
    /// <summary>Команды НВК-*. Бывший nvk-commands.lsp. Палитра шлёт эти имена через SendStringToExecute.</summary>
    public sealed class NvkCommands
    {
        [CommandMethod("НВК-ПОСТРОИТЬ", CommandFlags.UsePickSet)]
        public void Build() { NvkPipeline.Run(); }

        [CommandMethod("НВК-ПРОГОН", CommandFlags.UsePickSet)]
        public void Progon() { NvkPipeline.Run(); }

        [CommandMethod("НВК-ОБНОВИТЬ", CommandFlags.UsePickSet)]
        public void Update() { NvkPipeline.Run(); }

        [CommandMethod("НВК-ПАНЕЛЬ-ПРОГОН", CommandFlags.UsePickSet)]
        public void PanelBuild() { NvkPipeline.Run(); }

        [CommandMethod("НВК-ИНФО")]
        public void Info() { NvkPipeline.Info(); }

        [CommandMethod("NVKINFO")]
        public void InfoEn() { NvkPipeline.Info(); }

        [CommandMethod("НВК-ВВОД", CommandFlags.UsePickSet)]
        public void Input() { NvkPipeline.StageInput(true); }

        [CommandMethod("НВК-ДАМП", CommandFlags.UsePickSet)]
        public void Dump() { NvkPipeline.Dump(); }

        [CommandMethod("НВК-ПАНЕЛЬ-ДАМП", CommandFlags.UsePickSet)]
        public void PanelDump() { NvkPipeline.Dump(); }

        [CommandMethod("НВК-СЕЧЕНИЯ", CommandFlags.UsePickSet)]
        public void Cs()
        {
            NvkPipeline.StageConfig();
            NvkPipeline.StageInput(false);
            NvkPipeline.StageDomain();
            NvkPipeline.StageCs();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ПРОФИЛЬ", CommandFlags.UsePickSet)]
        public void Ep()
        {
            NvkPipeline.StageConfig();
            NvkPipeline.StageInput(false);
            NvkPipeline.StageDomain();
            NvkPipeline.StageEp();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ПОДПИСИ")]
        public void Poper()
        {
            NvkPipeline.StagePoper();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ОБЪЁМЫ", CommandFlags.UsePickSet)]
        public void Vol()
        {
            NvkPipeline.StageConfig();
            NvkPipeline.StageInput(false);
            NvkPipeline.StageDomain();
            NvkPipeline.StageVol();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ПЛАН", CommandFlags.UsePickSet)]
        public void Plan()
        {
            NvkPipeline.StageConfig();
            NvkPipeline.StageInput(false);
            NvkPipeline.StageDomain();
            NvkPipeline.StagePlan();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ЛИСТЫ")]
        public void Sheets()
        {
            NvkPipeline.StageSheets();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-ОЧИСТИТЬ")]
        public void Clean() { NvkPipeline.Clean(); }

        [CommandMethod("НВК-ПАНЕЛЬ-ОЧИСТИТЬ")]
        public void PanelClean() { NvkPipeline.Clean(); }

        [CommandMethod("НВК-ДВИГАТЬ")]
        public void Move()
        {
            NvkMover.MoveLisp(null);
            NvkPipeline.SaveNod();
        }

        [CommandMethod("NVKMOVE")]
        public void MoveEn() { Move(); }

        [CommandMethod("НВК-СБРОС")]
        public void Reset()
        {
            NvkPipeline.ResetInserts();
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                doc.Editor.WriteMessage("\n[НВК] точки вставки сброшены. Трубы и TIN на месте.");
        }

        [CommandMethod("НВК-СЛОИ")]
        public void Layers()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            NvkLayers.EnsureAll(doc.Database);
            doc.Editor.WriteMessage("\n[НВК] слои NVK-* и AppID готовы.");
        }

        [CommandMethod("НВК-СТРОКИ")]
        public void Rows() { NvkPalette.ShowRows(); }

        [CommandMethod("НВК-РАНДОМ")]
        public void RandomAll()
        {
            NvkSession.FreshRandom = true;
            NvkSession.FreshRandomKind = 0;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                doc.Editor.WriteMessage("\n[НВК] новый набор случайных отклонений по всем уровням.");
            NvkPipeline.Run();
        }

        [CommandMethod("НВК-ПАНЕЛЬ-РАНДОМ")]
        public void PanelRandom() { RandomAll(); }

        [CommandMethod("NVK-UI-PRIMENIT")]
        public void ApplyUi()
        {
            NvkPipeline.ApplyTable();
            NvkPipeline.SaveNod();
        }

        [CommandMethod("НВК-НАСТРОЙКИ")]
        public void Settings()
        {
            NvkPalette.Show();
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                doc.Editor.WriteMessage("\n[НВК] настройки — на панели слева.");
        }
    }
}
