using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Nvk.Core;

namespace Nvk.Cad
{
    /// <summary>
    /// Дублируемая палитра настройки строк таблиц L1–L5.
    /// Изменения применяются немедленно (автосохранение).
    /// </summary>
    sealed class RowGridPanel : UserControl
    {
        static readonly Color C_BG     = Color.FromArgb(22, 22, 38);
        static readonly Color C_CARD   = Color.FromArgb(30, 32, 52);
        static readonly Color C_STRIP  = Color.FromArgb(16, 17, 28);
        static readonly Color C_SEL    = Color.FromArgb(40, 52, 88);
        static readonly Color C_ACCENT = Color.FromArgb(70, 140, 215);
        static readonly Color C_TEXT   = Color.FromArgb(200, 202, 220);
        static readonly Color C_DIM    = Color.FromArgb(96, 100, 128);
        static readonly Color C_INPUT  = Color.FromArgb(14, 16, 30);
        static readonly Color C_BORDER = Color.FromArgb(50, 54, 86);
        static readonly Color C_BTN    = Color.FromArgb(38, 50, 82);
        static readonly Color C_PRIM   = Color.FromArgb(36, 82, 148);

        static readonly string[] RowTypeItems = { "Отметка", "Длина", "Уклон", "Диаметр", "Колодец" };
        static readonly string[] RowSrcItems  =
        {
            "Дно траншеи", "Верх песка", "Лоток трубы", "Верх трубы",
            "Верх защиты", "Засыпка / земля", "Покрытие (дизайн)", "Чёрная земля", "ΔH защиты"
        };
        static readonly (ProfileKind kind, string name)[] Profiles =
        {
            (ProfileKind.Razrabotka, "L1 — Разработка"),
            (ProfileKind.Pesok,      "L2 — Песок"),
            (ProfileKind.Truba,      "L3 — Труба"),
            (ProfileKind.Zashita,    "L4 — Защита"),
            (ProfileKind.Zasypka,    "L5 — Засыпка"),
        };

        readonly ComboBox     _profileSel;
        readonly DataGridView _grid;
        readonly Label        _hint;
        NvkPanel              _mainPanel;
        bool                  _loading;

        ProfileKind CurrentKind
            => _profileSel.SelectedIndex >= 0 && _profileSel.SelectedIndex < Profiles.Length
               ? Profiles[_profileSel.SelectedIndex].kind
               : ProfileKind.Truba;

        // ── Конструктор ───────────────────────────────────────────────────────

        public RowGridPanel()
        {
            Dock      = DockStyle.Fill;
            BackColor = C_BG;
            Font      = new Font("Segoe UI", 9f);

            // Профиль — выпадающий список
            var selLabel = new Label
            {
                Text = "Профиль:", AutoSize = true,
                ForeColor = C_DIM, BackColor = Color.Transparent,
                Margin = new Padding(4, 6, 4, 2)
            };
            _profileSel = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = C_INPUT, ForeColor = C_TEXT,
                Dock = DockStyle.None, Width = 200,
                Margin = new Padding(4, 0, 4, 4)
            };
            foreach (var (_, name) in Profiles) _profileSel.Items.Add(name);
            _profileSel.SelectedIndex = 2; // L3 Труба по умолчанию

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 58, BackColor = C_STRIP,
                Padding = new Padding(4, 4, 4, 2)
            };
            topFlow.Controls.Add(selLabel);
            topFlow.Controls.Add(_profileSel);

            // Тулбар — строки
            var btnAdd = MakeBtn("＋ Строку", 90);
            var btnDel = MakeBtn("✕",         36);
            var btnUp  = MakeBtn("↑",         36);
            var btnDn  = MakeBtn("↓",         36);
            var btnRst = MakeBtn("Сброс",     68);
            var btnRnd = MakeBtn("Рандом",    80);

            btnAdd.Click += (s, e) => AddRow();
            btnDel.Click += (s, e) => DelRow();
            btnUp.Click  += (s, e) => MoveRow(-1);
            btnDn.Click  += (s, e) => MoveRow(+1);
            btnRst.Click += (s, e) => ResetToDefaults();
            btnRnd.Click += (s, e) => Recast();

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 36, BackColor = C_STRIP,
                Padding = new Padding(4, 4, 4, 0)
            };
            toolbar.Controls.AddRange(new Control[] { btnAdd, btnDel, btnUp, btnDn, btnRst, btnRnd });

            // Кнопка «Построить» снизу
            var btnBuild = new Button
            {
                Text = "Построить", Dock = DockStyle.Bottom, Height = 30,
                FlatStyle = FlatStyle.Flat, BackColor = C_PRIM, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
            };
            btnBuild.Click += (s, e) => Build();

            // Подсказка внизу
            _hint = new Label
            {
                Text = "Дно траншеи общее для L1 и L2. Песок — свой набор. «Рандом» бросает новый.",
                Dock = DockStyle.Bottom, Height = 20,
                ForeColor = C_DIM, BackColor = C_STRIP,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 8f)
            };

            // Сетка
            _grid = MakeGrid();
            _grid.Dock = DockStyle.Fill;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(topFlow);
            Controls.Add(_hint);
            Controls.Add(btnBuild);

            _profileSel.SelectedIndexChanged += (s, e) => OnProfileChanged();
            _grid.CellEndEdit               += (s, e) => AutoSave();
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += (s, e) =>
            {
                var col = _grid.Columns[e.ColumnIndex]?.Name;
                if (col == "rnd" && !_loading && e.RowIndex >= 0)
                {
                    var row = _grid.Rows[e.RowIndex];
                    bool on = row.Cells["rnd"].Value as bool? ?? false;
                    if (on && (row.Cells["type"].Value as string) == "Отметка"
                        && string.IsNullOrWhiteSpace(row.Cells["amp"].Value as string))
                        row.Cells["amp"].Value = Scatter.DefaultAmp(CurrentKind)
                            .ToString("0.###", CultureInfo.InvariantCulture);
                }
                if (col == "show" || col == "pf" || col == "rnd")
                    AutoSave();
            };
        }

        // ── Публичный API ─────────────────────────────────────────────────────

        public void SetMainPanel(NvkPanel panel) { _mainPanel = panel; }

        public void SelectProfile(ProfileKind kind)
        {
            for (int i = 0; i < Profiles.Length; i++)
                if (Profiles[i].kind == kind)
                {
                    _profileSel.SelectedIndex = i;
                    return;
                }
        }

        public void LoadFromState() => OnProfileChanged();

        // ── Внутренняя логика ─────────────────────────────────────────────────

        void OnProfileChanged()
        {
            _loading = true;
            try
            {
                var kind = CurrentKind;
                List<TableRowDef> defs = null;
                if (_mainPanel != null)
                    defs = _mainPanel.GetDefs(kind);
                if (defs == null || defs.Count == 0)
                    defs = TableRowDef.Defaults(kind);
                else
                    RowScatter.ApplyLegacy(defs, kind);
                LoadGrid(defs);
                _hint.Text = "Изменения применяются при следующем «Построить»";
            }
            finally
            {
                _loading = false;
            }
        }

        void AutoSave()
        {
            if (_loading) return;
            var defs = SaveGrid();
            if (defs == null) return;
            var kind = CurrentKind;

            _mainPanel?.SetDefs(kind, defs);

            string serialized = TableRowDef.SerializeList(defs);
            switch (kind)
            {
                case ProfileKind.Razrabotka: NvkUiState.RowsL1 = serialized; break;
                case ProfileKind.Pesok:      NvkUiState.RowsL2 = serialized; break;
                case ProfileKind.Truba:      NvkUiState.RowsL3 = serialized; break;
                case ProfileKind.Zashita:    NvkUiState.RowsL4 = serialized; break;
                case ProfileKind.Zasypka:    NvkUiState.RowsL5 = serialized; break;
            }
            RowScatter.SyncFrom(defs);
            _mainPanel?.PullDefsFromState();
            _hint.Text = "Сохранено ✓  — нажмите «Построить» для применения";
        }

        void Build()
        {
            AutoSave();
            _mainPanel?.SaveToState();
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            NvkSend.Command("НВК-ПАНЕЛЬ-ПРОГОН");
        }

        void ResetToDefaults()
        {
            _loading = true;
            try { LoadGrid(TableRowDef.Defaults(CurrentKind)); }
            finally { _loading = false; }
            AutoSave();
        }

        void Recast()
        {
            AutoSave();
            _mainPanel?.SaveToState();
            NvkSession.FreshRandom = true;
            NvkSession.FreshRandomKind = (int)CurrentKind;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            _hint.Text = "Новый набор для этого уровня — смотри командную строку";
            NvkSend.Command("НВК-ПАНЕЛЬ-ПРОГОН");
        }

        // ── Сетка ─────────────────────────────────────────────────────────────

        DataGridView MakeGrid()
        {
            var cs = new DataGridViewCellStyle
            {
                BackColor = C_INPUT, ForeColor = C_TEXT,
                SelectionBackColor = C_SEL, SelectionForeColor = Color.White
            };
            var hs = new DataGridViewCellStyle
            {
                BackColor = C_STRIP, ForeColor = C_DIM, SelectionBackColor = C_STRIP
            };
            var g = new DataGridView
            {
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible     = false,
                BackgroundColor       = C_INPUT,
                GridColor             = C_BORDER,
                BorderStyle           = BorderStyle.None,
                EnableHeadersVisualStyles  = false,
                DefaultCellStyle           = cs,
                ColumnHeadersDefaultCellStyle = hs,
                MultiSelect     = false,
                SelectionMode   = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate     = { Height = 26 }
            };

            var colShow = new DataGridViewCheckBoxColumn
                { Name = "show", HeaderText = "✓", Width = 32, Resizable = DataGridViewTriState.False };
            colShow.DefaultCellStyle.SelectionBackColor = C_SEL;

            var colType = new DataGridViewComboBoxColumn
            {
                Name = "type", HeaderText = "Тип", Width = 90,
                Resizable = DataGridViewTriState.False, FlatStyle = FlatStyle.Flat
            };
            colType.Items.AddRange(RowTypeItems);
            colType.DefaultCellStyle.BackColor = C_INPUT;
            colType.DefaultCellStyle.ForeColor = C_TEXT;

            var colSrc = new DataGridViewComboBoxColumn
            {
                Name = "src", HeaderText = "Источник", Width = 150,
                Resizable = DataGridViewTriState.False, FlatStyle = FlatStyle.Flat
            };
            colSrc.Items.AddRange(RowSrcItems);
            colSrc.DefaultCellStyle.BackColor = C_INPUT;
            colSrc.DefaultCellStyle.ForeColor = C_TEXT;

            var colLabel = new DataGridViewTextBoxColumn
                { Name = "label", HeaderText = "Подпись", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };

            var colPF = new DataGridViewCheckBoxColumn
                { Name = "pf", HeaderText = "П/Ф", Width = 36, Resizable = DataGridViewTriState.False };
            colPF.DefaultCellStyle.SelectionBackColor = C_SEL;
            colPF.HeaderCell.ToolTipText = "Разбить на строку «проект» и строку «факт»";

            var colRnd = new DataGridViewCheckBoxColumn
                { Name = "rnd", HeaderText = "Ранд", Width = 40, Resizable = DataGridViewTriState.False };
            colRnd.DefaultCellStyle.SelectionBackColor = C_SEL;
            colRnd.HeaderCell.ToolTipText = "Случайный разброс на этой отметке (свой набор чисел у уровня)";

            var colAmp = new DataGridViewTextBoxColumn
                { Name = "amp", HeaderText = "±м", Width = 44, Resizable = DataGridViewTriState.False };
            colAmp.HeaderCell.ToolTipText = "Амплитуда разброса, м. 0 = по умолчанию уровня";

            var colDec = new DataGridViewTextBoxColumn
                { Name = "dec", HeaderText = "Зн", Width = 36, Resizable = DataGridViewTriState.False };

            g.Columns.AddRange(new DataGridViewColumn[] { colShow, colType, colSrc, colLabel, colPF, colRnd, colAmp, colDec });

            g.DataError += (s, e) => { e.Cancel = true; };
            return g;
        }

        void LoadGrid(List<TableRowDef> defs)
        {
            _grid.Rows.Clear();
            if (defs == null) return;
            foreach (var d in defs)
            {
                string typeStr;
                switch (d.Type)
                {
                    case SheetRowKind.Height:  typeStr = "Отметка"; break;
                    case SheetRowKind.Length:  typeStr = "Длина";   break;
                    case SheetRowKind.Slope:   typeStr = "Уклон";   break;
                    case SheetRowKind.Diam:    typeStr = "Диаметр"; break;
                    case SheetRowKind.Manhole: typeStr = "Колодец"; break;
                    default:                   typeStr = "Отметка"; break;
                }
                bool isHeight = d.Type == SheetRowKind.Height;
                string srcStr = isHeight ? TableRowDef.SrcDisplayName(d.Src) : "";
                string decStr = d.RoundTo > 0 ? d.RoundTo.ToString() : "";
                string ampStr = "";
                if (isHeight && (d.UseScatter || d.ScatterAmp > 1e-9))
                {
                    double a = d.ScatterAmp > 1e-9 ? d.ScatterAmp : Scatter.DefaultAmp(CurrentKind);
                    ampStr = a.ToString("0.###", CultureInfo.InvariantCulture);
                }
                int idx = _grid.Rows.Add(d.Show, typeStr, srcStr, d.Label ?? "", d.IsProjFact, d.UseScatter, ampStr, decStr);
                if (!isHeight)
                {
                    _grid.Rows[idx].Cells["pf"].ReadOnly  = true;
                    _grid.Rows[idx].Cells["rnd"].ReadOnly = true;
                    _grid.Rows[idx].Cells["amp"].ReadOnly = true;
                    _grid.Rows[idx].Cells["pf"].Style.ForeColor  = C_DIM;
                    _grid.Rows[idx].Cells["rnd"].Style.ForeColor = C_DIM;
                    _grid.Rows[idx].Cells["amp"].Style.ForeColor = C_DIM;
                }
            }
        }

        List<TableRowDef> SaveGrid()
        {
            var list = new List<TableRowDef>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var d = new TableRowDef();
                d.Show = row.Cells["show"].Value as bool? ?? true;
                string typeStr = row.Cells["type"].Value as string ?? "Отметка";
                switch (typeStr)
                {
                    case "Отметка": d.Type = SheetRowKind.Height;  break;
                    case "Длина":   d.Type = SheetRowKind.Length;  break;
                    case "Уклон":   d.Type = SheetRowKind.Slope;   break;
                    case "Диаметр": d.Type = SheetRowKind.Diam;    break;
                    case "Колодец": d.Type = SheetRowKind.Manhole; break;
                    default:        d.Type = SheetRowKind.Height;  break;
                }
                if (d.Type == SheetRowKind.Height)
                {
                    string srcStr = row.Cells["src"].Value as string ?? "";
                    d.Src        = TableRowDef.SrcFromDisplayName(srcStr);
                    d.IsProjFact = row.Cells["pf"].Value  as bool? ?? false;
                    d.UseScatter = row.Cells["rnd"].Value as bool? ?? false;
                    string ampS = row.Cells["amp"].Value as string ?? "";
                    double amp;
                    if (double.TryParse(ampS.Replace(',', '.'), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out amp))
                        d.ScatterAmp = amp;
                    if (d.UseScatter && d.ScatterAmp <= 1e-9)
                        d.ScatterAmp = Scatter.DefaultAmp(CurrentKind);
                }
                d.AmpInRecord = true;
                d.Label   = row.Cells["label"].Value as string ?? "";
                string decS = row.Cells["dec"].Value as string ?? "";
                int dec;
                d.RoundTo = int.TryParse(decS, out dec) ? dec : 0;
                list.Add(d);
            }
            return list;
        }

        void MoveRow(int dir)
        {
            int i = _grid.CurrentRow?.Index ?? -1;
            if (i < 0) return;
            int j = i + dir;
            if (j < 0 || j >= _grid.Rows.Count || _grid.Rows[j].IsNewRow) return;
            var tmp = new object[_grid.Columns.Count];
            for (int c = 0; c < _grid.Columns.Count; c++) tmp[c] = _grid.Rows[i].Cells[c].Value;
            for (int c = 0; c < _grid.Columns.Count; c++)
            {
                _grid.Rows[i].Cells[c].Value = _grid.Rows[j].Cells[c].Value;
                _grid.Rows[j].Cells[c].Value = tmp[c];
            }
            _grid.CurrentCell = _grid.Rows[j].Cells[0];
            AutoSave();
        }

        void AddRow()
        {
            _grid.Rows.Add(true, "Отметка", "Дно траншеи", "", false, false, "0.05", "2");
            _grid.CurrentCell = _grid.Rows[_grid.Rows.Count - 1].Cells[0];
            AutoSave();
        }

        void DelRow()
        {
            var row = _grid.CurrentRow;
            if (row == null || row.IsNewRow) return;
            int idx = row.Index;
            _grid.Rows.Remove(row);
            if (_grid.Rows.Count > 0)
                _grid.CurrentCell = _grid.Rows[Math.Min(idx, _grid.Rows.Count - 1)].Cells[0];
            AutoSave();
        }

        Button MakeBtn(string text, int width)
            => new Button
            {
                Text = text, Width = width, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = C_BTN, ForeColor = C_TEXT,
                Margin = new Padding(2, 2, 0, 0)
            };
    }
}
