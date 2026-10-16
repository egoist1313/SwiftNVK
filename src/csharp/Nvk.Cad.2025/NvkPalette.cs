using System;
using System.Drawing;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Nvk.Cad.NvkPalette))]
[assembly: CommandClass(typeof(Nvk.Cad.ZonePicker))]
[assembly: CommandClass(typeof(Nvk.Cad.PlanRotPicker))]

namespace Nvk.Cad
{
    public sealed class NvkPalette
    {
        static PaletteSet _ps;
        static NvkPanel _panel;
        static RowGridPanel _rowPanel;
        static readonly Guid Id = new Guid("b7e2c4a1-9d3f-4e8a-a12b-6f0c91e4d8a7");

        [CommandMethod("NVKPANEL")]
        public void ShowEn()
        {
            Show();
        }

        [CommandMethod("НВК-ПАНЕЛЬ")]
        public void ShowRu()
        {
            Show();
        }

        public static void SetStatus(string text)
        {
            try
            {
                if (_panel != null) _panel.ShowStatus(text);
            }
            catch { }
        }

        public static ResultBuffer GetLisp(ResultBuffer args)
        {
            if (_panel != null)
            {
                try { _panel.SaveToState(); } catch { }
            }
            NvkProfile.Save();
            return NvkUiState.ToRb();
        }

        public static ResultBuffer PutLisp(ResultBuffer args)
        {
            NvkUiState.FromRb(args);
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                NvkUiState.MarkCurrent(NvkUiState.MakeDocKey(doc.Database, doc.Name));
            if (_panel != null)
            {
                try { _panel.LoadFromState(); } catch { }
            }
            if (_rowPanel != null)
            {
                try { _rowPanel.LoadFromState(); } catch { }
            }
            return new ResultBuffer(new TypedValue((int)LispDataType.Text, "ok"));
        }

        public static void SetZones(string zones)
        {
            NvkUiState.Zones = zones ?? "";
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                NvkUiState.MarkCurrent(NvkUiState.MakeDocKey(doc.Database, doc.Name));
            if (_panel != null)
            {
                try { _panel.LoadFromState(); } catch { }
            }
        }

        public static void SetPlanRot(double deg)
        {
            NvkUiState.PlanRotDeg = deg;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                NvkUiState.MarkCurrent(NvkUiState.MakeDocKey(doc.Database, doc.Name));
            if (_panel != null)
            {
                try { _panel.LoadFromState(); } catch { }
            }
        }

        public static void SyncFromDoc(Autodesk.AutoCAD.ApplicationServices.Document doc, bool force)
        {
            if (doc == null) return;
            try
            {
                string key = NvkUiState.MakeDocKey(doc.Database, doc.Name);
                bool fromDwg = NvkUiState.LoadFromDatabase(doc.Database, doc.Name, force);
                if (!fromDwg && (force || !NvkUiState.IsCurrent(key)))
                {
                    if (!NvkProfile.Load())
                        NvkUiState.ResetToDefaults();
                    NvkSession.ClearDrawingCaches();
                    NvkUiState.HasNod = false;
                    NvkUiState.MarkCurrent(key);
                    try { NvkLog.Write("[НВК] STATE из профиля (в чертеже пусто)"); } catch { }
                }
                if (_panel != null)
                    _panel.LoadFromState();
                if (_rowPanel != null)
                    _rowPanel.LoadFromState();
            }
            catch { }
        }

        public static void Show()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
                SyncFromDoc(doc, false);
            if (_ps == null)
            {
                _ps = new PaletteSet("Поперечник НВК", "NVKPANEL", Id);
                _ps.Style = PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.Snappable;
                _ps.MinimumSize = new Size(360, 420);
                _ps.DockEnabled = DockSides.Left | DockSides.Right;
                _ps.Size = new Size(400, 760);
            }
            if (_panel == null)
            {
                try
                {
                    _panel = new NvkPanel();
                    _ps.Add("НВК", _panel);
                }
                catch (System.Exception ex)
                {
                    if (doc != null)
                        doc.Editor.WriteMessage("\n[НВК] ошибка панели: " + ex.Message + "\n" + ex.StackTrace);
                    _panel = null;
                }
            }
            else
            {
                try { _panel.LoadFromState(); } catch { }
            }
            if (_rowPanel == null && _panel != null)
            {
                try
                {
                    _rowPanel = new RowGridPanel();
                    _rowPanel.SetMainPanel(_panel);
                    _ps.Add("Строки", _rowPanel);
                    _rowPanel.LoadFromState();
                }
                catch (System.Exception ex)
                {
                    if (doc != null)
                        doc.Editor.WriteMessage("\n[НВК] ошибка панели строк: " + ex.Message);
                    _rowPanel = null;
                }
            }
            else if (_rowPanel != null)
            {
                try { _rowPanel.LoadFromState(); } catch { }
            }
            _ps.Visible = true;
            if (_panel != null)
            {
                try { _panel.RefreshInfo(); } catch { }
            }
            if (doc != null)
                doc.Editor.WriteMessage("\n[НВК] панель. Построить / Обновить / Очистить / Дамп.");
        }

        public static void ShowRows(Nvk.Core.ProfileKind kind = Nvk.Core.ProfileKind.Truba)
        {
            Show();
            if (_ps != null && _rowPanel != null)
            {
                try
                {
                    for (int i = 0; i < _ps.Count; i++)
                    {
                        if (_ps[i].Name == "Строки")
                        {
                            _ps.Activate(i);
                            break;
                        }
                    }
                    _rowPanel.SelectProfile(kind);
                }
                catch { }
            }
        }
    }
}
