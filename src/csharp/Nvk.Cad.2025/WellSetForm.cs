using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Свой набор колец или шахт у каждого колодца. Пустой набор — авто по высоте.</summary>
    sealed class WellSetForm : Form
    {
        public string Packed = "";

        sealed class Row
        {
            public string Handle;
            public string Name;
            public double Diam;
            public double Height;
            public bool Own;
            public string Parts = "";

            public override string ToString()
            {
                var ru = CultureInfo.GetCultureInfo("ru-RU");
                string n = string.IsNullOrWhiteSpace(Name) ? Handle : Name;
                return n
                    + "   Ø" + Diam.ToString("0.00", ru)
                    + "  H=" + Height.ToString("0.00", ru)
                    + (Own ? "  свой" : "");
            }
        }

        readonly bool _plastic;
        readonly string _shaft;
        readonly string _base;
        readonly string _neck;
        readonly List<Row> _rows = new List<Row>();
        readonly ListBox _list;
        readonly CheckBox _own;
        readonly DataGridView _grid;
        readonly Label _hint;
        DataGridViewColumn _nameCol;
        int _cur = -1;
        bool _loading;

        public WellSetForm()
        {
            _plastic = NvkUiState.WellBodyPlastic;
            _shaft = string.IsNullOrEmpty(NvkUiState.WellShaftCode) ? "Авто" : NvkUiState.WellShaftCode;
            _base = string.IsNullOrEmpty(NvkUiState.WellBaseCode) ? "Прямопроходная" : NvkUiState.WellBaseCode;
            _neck = string.IsNullOrEmpty(NvkUiState.WellNeckCode) ? "Конус" : NvkUiState.WellNeckCode;

            Text = _plastic ? "Шахты по колодцам" : "Кольца по колодцам";
            Font = new System.Drawing.Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(760, 460);
            ShowInTaskbar = false;

            _list = new ListBox { Left = 8, Top = 8, Width = 280, Height = 400 };
            _list.SelectedIndexChanged += OnSelect;
            _own = new CheckBox
            {
                Left = 300,
                Top = 8,
                AutoSize = true,
                Text = "Свой набор"
            };
            _own.CheckedChanged += OwnChanged;
            _hint = new Label
            {
                Left = 300,
                Top = 32,
                Width = 448,
                Height = 48,
                Text = _plastic
                    ? "Автоматом — шахты 1,00 / 0,50 / 0,25 м по высоте этого колодца. Свой набор заменяет авто. 0 — такого кольца нет."
                    : "Автоматом — КС / ПН / ПП по диаметру и высоте этого колодца. Свой набор заменяет авто."
            };
            _grid = new DataGridView
            {
                Left = 300,
                Top = 84,
                Width = 448,
                Height = 280,
                AllowUserToAddRows = !_plastic,
                AllowUserToDeleteRows = !_plastic,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            if (_plastic) BuildShaftGrid();
            else BuildConcreteGrid();

            var pick = new Button { Left = 300, Top = 372, Width = 140, Height = 28, Text = "Подобрать" };
            pick.Click += (s, e) => Pick();
            var ok = new Button
            {
                Left = 548,
                Top = 420,
                Width = 96,
                Height = 28,
                Text = "ОК",
                DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Left = 652,
                Top = 420,
                Width = 96,
                Height = 28,
                Text = "Отмена",
                DialogResult = DialogResult.Cancel
            };
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(_list);
            Controls.Add(_own);
            Controls.Add(_hint);
            Controls.Add(_grid);
            Controls.Add(pick);
            Controls.Add(ok);
            Controls.Add(cancel);
            FormClosing += OnClose;
            LoadWells();
        }

        void BuildShaftGrid()
        {
            _grid.Columns.Add("h", "Шахта");
            _grid.Columns.Add("qty", "шт");
            _nameCol = _grid.Columns[0];
            _nameCol.ReadOnly = true;
            _grid.Columns[1].FillWeight = 40;
            _grid.Rows.Add("1,00 м", "0");
            _grid.Rows.Add("0,50 м", "0");
            _grid.Rows.Add("0,25 м", "0");
        }

        void BuildConcreteGrid()
        {
            var code = new DataGridViewComboBoxColumn
            {
                Name = "code",
                HeaderText = "Элемент",
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            string[] codes = WellCatalog.ConcreteCodes();
            for (int i = 0; i < codes.Length; i++) code.Items.Add(codes[i]);
            _grid.Columns.Add(code);
            _grid.Columns.Add("qty", "шт");
            _grid.Columns[1].FillWeight = 40;
            _nameCol = code;
        }

        void LoadWells()
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    _hint.Text = "Нет открытого чертежа.";
                    return;
                }
                List<WellPitInput> wells;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    wells = WellReader.AllIn(tr, doc.Database);
                    tr.Commit();
                }
                string packed = NvkUiState.WellEach ?? "";
                for (int i = 0; i < wells.Count; i++)
                {
                    var w = wells[i];
                    if (w == null || string.IsNullOrEmpty(w.Handle)) continue;
                    var row = new Row
                    {
                        Handle = w.Handle,
                        Name = w.Name,
                        Diam = w.Diam,
                        Height = w.Height
                    };
                    var own = WellCatalog.OwnOf(packed, w.Handle, _plastic);
                    if (own != null && !string.IsNullOrEmpty(own.Parts))
                    {
                        row.Own = true;
                        row.Parts = own.Parts;
                    }
                    _rows.Add(row);
                    _list.Items.Add(row);
                }
                if (_rows.Count == 0)
                    _hint.Text = "В чертеже нет колодцев. Набор по колодцам появится, когда в сети будут структуры.";
                else
                    _list.SelectedIndex = 0;
            }
            catch (Exc ex)
            {
                _hint.Text = ex.Message;
            }
        }

        void OnSelect(object sender, EventArgs e)
        {
            if (_loading) return;
            int next = _list.SelectedIndex;
            if (next == _cur) return;
            SaveGrid(_cur);
            _cur = next;
            ShowRow();
        }

        void ShowRow()
        {
            var row = RowAt(_cur);
            _loading = true;
            if (row == null)
            {
                _own.Checked = false;
                _grid.ReadOnly = true;
                _loading = false;
                return;
            }
            _own.Checked = row.Own;
            _grid.ReadOnly = !row.Own;
            if (_nameCol != null) _nameCol.ReadOnly = true;
            string show = row.Own && !string.IsNullOrEmpty(row.Parts) ? row.Parts : AutoText(row);
            FillFrom(show, row);
            _loading = false;
        }

        void OwnChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            var row = RowAt(_cur);
            if (row == null) return;
            row.Own = _own.Checked;
            if (row.Own && string.IsNullOrEmpty(row.Parts))
                row.Parts = AutoText(row);
            _grid.ReadOnly = !row.Own;
            if (_nameCol != null) _nameCol.ReadOnly = true;
            if (row.Own)
                FillFrom(row.Parts, row);
            TouchList();
        }

        void Pick()
        {
            var row = RowAt(_cur);
            if (row == null) return;
            row.Own = true;
            row.Parts = AutoText(row);
            _loading = true;
            _own.Checked = true;
            _loading = false;
            _grid.ReadOnly = false;
            if (_nameCol != null) _nameCol.ReadOnly = true;
            FillFrom(row.Parts, row);
            TouchList();
        }

        void FillFrom(string parts, Row row)
        {
            if (_plastic) FillShaft(parts, row);
            else FillConcrete(parts);
        }

        void FillShaft(string parts, Row row)
        {
            if (_grid.Rows.Count < 3) return;
            var list = WellCatalog.ParseParts(parts);
            double[] hs = WellCatalog.PlasticShaftHeights();
            for (int i = 0; i < 3; i++)
            {
                int q = 0;
                for (int k = 0; k < list.Count; k++)
                {
                    if (list[k] == null) continue;
                    if (Math.Abs(list[k].Height - hs[i]) < 0.02) q += list[k].Qty;
                }
                int ser = SeriesOf(row);
                _grid.Rows[i].Cells[0].Value = hs[i].ToString("0.00", CultureInfo.GetCultureInfo("ru-RU"))
                    + " м  DN" + ser.ToString(CultureInfo.InvariantCulture);
                _grid.Rows[i].Cells[1].Value = q.ToString(CultureInfo.InvariantCulture);
            }
        }

        void FillConcrete(string parts)
        {
            _grid.Rows.Clear();
            var list = WellCatalog.ParseParts(parts);
            var col = _grid.Columns.Count > 0 ? _grid.Columns[0] as DataGridViewComboBoxColumn : null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || list[i].Qty < 1) continue;
                if (col != null && !col.Items.Contains(list[i].Code)) continue;
                _grid.Rows.Add(list[i].Code, list[i].Qty.ToString(CultureInfo.InvariantCulture));
            }
        }

        void SaveGrid(int index)
        {
            var row = RowAt(index);
            if (row == null || !row.Own) return;
            if (_grid != null) _grid.EndEdit();
            row.Parts = _plastic ? ReadShaft(row) : ReadConcrete();
        }

        string ReadShaft(Row row)
        {
            int ser = SeriesOf(row);
            double[] hs = WellCatalog.PlasticShaftHeights();
            var list = new List<WellPart>();
            for (int i = 0; i < 3 && i < _grid.Rows.Count; i++)
            {
                int q = 0;
                var cell = _grid.Rows[i].Cells[1].Value;
                if (cell != null)
                    int.TryParse(cell.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out q);
                if (q < 0) q = 0;
                var p = WellCatalog.MakePlasticShaft(ser, hs[i]);
                p.Qty = q;
                list.Add(p);
            }
            return WellCatalog.FormatParts(list);
        }

        string ReadConcrete()
        {
            var list = new List<WellPart>();
            foreach (DataGridViewRow grow in _grid.Rows)
            {
                if (grow.IsNewRow) continue;
                string code = grow.Cells[0].Value != null ? grow.Cells[0].Value.ToString().Trim() : "";
                if (code.Length == 0) continue;
                int q = 1;
                if (grow.Cells.Count > 1 && grow.Cells[1].Value != null)
                    int.TryParse(grow.Cells[1].Value.ToString(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out q);
                if (q < 1) continue;
                var p = WellCatalog.Find(code);
                if (p == null || p.Kind == WellPartKind.Shaft) continue;
                p.Qty = q;
                list.Add(p);
            }
            return WellCatalog.FormatParts(list);
        }

        string AutoText(Row row)
        {
            double h = row.Height < 0.4 ? 0.4 : row.Height;
            double d = row.Diam < 0.3 ? 1.0 : row.Diam;
            if (_plastic)
            {
                int ser = WellCatalog.PlasticSeriesMm(_shaft, d);
                return WellCatalog.FormatParts(WellCatalog.PlasticShaftParts(ser, h, _base, _neck));
            }
            return WellCatalog.FormatParts(WellCatalog.AutoParts(d, h));
        }

        int SeriesOf(Row row)
        {
            double d = row != null && row.Diam >= 0.3 ? row.Diam : 1.0;
            return WellCatalog.PlasticSeriesMm(_shaft, d);
        }

        Row RowAt(int i)
        {
            if (i < 0 || i >= _rows.Count) return null;
            return _rows[i];
        }

        void TouchList()
        {
            if (_cur < 0 || _cur >= _list.Items.Count) return;
            _loading = true;
            int i = _cur;
            _list.Items[i] = _rows[i];
            _list.SelectedIndex = i;
            _loading = false;
        }

        void OnClose(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK) return;
            SaveGrid(_cur);
            var handles = new List<string>();
            var own = new List<WellOwnSet>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null || string.IsNullOrEmpty(row.Handle)) continue;
                handles.Add(row.Handle);
                if (!row.Own || string.IsNullOrEmpty(row.Parts)) continue;
                own.Add(new WellOwnSet
                {
                    Handle = row.Handle,
                    Plastic = _plastic,
                    Parts = row.Parts
                });
            }
            Packed = WellCatalog.ReplaceOwn(NvkUiState.WellEach, _plastic, handles, own);
        }
    }
}
