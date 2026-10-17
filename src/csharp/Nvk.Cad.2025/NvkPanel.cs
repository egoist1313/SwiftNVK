using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Nvk.Core;

namespace Nvk.Cad
{
    sealed class NvkPanel : UserControl
    {
        // ── Design tokens ─────────────────────────────────────────────────────
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
        static readonly Color C_WARN   = Color.FromArgb(160, 90, 24);
        // ──────────────────────────────────────────────────────────────────────

        readonly TextBox _bedding, _zash, _asph, _gazn, _trot, _slopeL, _slopeR, _trenchL, _trenchR, _hwidth, _ygap;
        readonly ComboBox _joint, _slopeU;
        readonly TextBox _hSc, _lSc, _csSc, _csTh, _textH, _textOff, _stations, _labW, _labH, _vpEp, _vpCs, _vpPlan;
        readonly CheckBox _blockCs, _showTags, _doCs, _doEp, _doPoper, _doVol, _doPlan, _doSheets;
        readonly CheckBox _showPerm;
        readonly CheckBox _projFact, _singleSc;
        readonly TextBox _annGap, _zones, _planInt, _planMin;
        readonly Label _planRotLbl;
        readonly CheckBox _annot, _pavAll, _pickZones, _planEn, _drawProj;
        readonly TextBox _other, _tblOff, _labRoad, _labEarth, _labBot;
        readonly TextBox _labTblL2, _labTblL3, _labTblL4, _labTblL5;
        readonly ComboBox _fillA, _fillT, _fillG, _fillP;
        readonly ComboBox _l1rl, _l1el, _l1bl;
        readonly TextBox _l1preset;
        readonly CheckBox _useBe, _l1mh;
        readonly Button _btnRoad, _btnEarth, _btnBot, _btnHat, _btnPav, _btnCs;
        readonly ComboBox _cL0, _cL6;
        readonly SheetUi _s2, _s3, _s4, _s5;
        readonly CheckBox _useWall;
        readonly DataGridView _wallGrid;
        System.Collections.Generic.List<TableRowDef> _defsL1, _defsL2, _defsL3, _defsL4, _defsL5;
        readonly Label _status, _info;

        public NvkPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9f);

            var tabs = new FlatTabControl
            {
                Dock = DockStyle.Fill,
                Alignment = TabAlignment.Left,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(38, 128),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                BackColor = C_CARD
            };
            tabs.DrawItem += DrawTab;
            Controls.Add(tabs);

            var pRun  = Page(tabs, "Прогон");
            var pPar  = Page(tabs, "Параметры");
            var pTbl  = Page(tabs, "Таблица");
            var pFact = Page(tabs, "Факт / зоны");
            var pCov  = Page(tabs, "Покрытия");
            var pL1   = Page(tabs, "L1 Разработка");
            var pL2   = Page(tabs, "L2 Песок");
            var pL3   = Page(tabs, "L3 Труба");
            var pL4   = Page(tabs, "L4 Защита");
            var pL5   = Page(tabs, "L5 Засыпка");

            _info = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(240, 0),
                ForeColor = C_ACCENT,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 4, 0, 8)
            };
            pRun.Controls.Add(_info);

            pRun.Controls.Add(Head("Этапы"));
            _doCs    = Chk(pRun, "Поперечники", true);
            _doEp    = Chk(pRun, "Продольники", true);
            _doPoper = Chk(pRun, "Таблица POPER", true);
            _doVol   = Chk(pRun, "Объёмы + колодцы", true);
            _doPlan  = Chk(pRun, "План", true);
            _doSheets = Chk(pRun, "Листы + видовые экраны", true);
            _stations = Num(pRun, "Станции (AUTO или пикеты)", "AUTO");

            pRun.Controls.Add(Head("Действия"));
            var btns = new FlowLayoutPanel { Width = 244, Height = 164, WrapContents = true, BackColor = Color.Transparent };
            btns.Controls.Add(Btn("Построить",    Build,                                   118, true));
            btns.Controls.Add(Btn("Обновить",     Build,                                   118));
            btns.Controls.Add(Btn("Трубы",        () => Send("НВК-ВВОД "),                 118));
            btns.Controls.Add(Btn("Дамп",         () => Send("НВК-ПАНЕЛЬ-ДАМП "),         118));
            btns.Controls.Add(Btn("Очистить",     () => Send("НВК-ПАНЕЛЬ-ОЧИСТИТЬ "),     118));
            btns.Controls.Add(Btn("Сброс точек",  () => Send("НВК-СБРОС "),                118));
            btns.Controls.Add(Btn("Двигать",      () => Send("НВК-ДВИГАТЬ "),              118));
            btns.Controls.Add(Btn("Листы",        () => Send("НВК-ЛИСТЫ "),                118));
            pRun.Controls.Add(btns);

            _status = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(240, 0),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Text = "Построить: трубы и точка CS — в командной строке."
            };
            pRun.Controls.Add(_status);

            pPar.Controls.Add(Head("Траншея / сечение"));
            _trenchL = Num(pPar, "Ширина дна лево, м (0=каталог)", "0");
            _trenchR = Num(pPar, "Ширина дна право, м (0=каталог)", "0");
            _slopeL  = Num(pPar, "Откос лево m (0=вертикаль)", "0.00");
            _slopeR  = Num(pPar, "Откос право m (0=вертикаль)", "0.00");
            _hwidth  = Num(pPar, "Полуширина TIN, м", "3.0");
            _ygap   = Num(pPar, "Шаг уровней CS, ед.", "100");
            _joint  = Combo(pPar, "Стык", new[] { "SOCKET", "WELD", "COUPLING" });

            pPar.Controls.Add(Head("Масштаб"));
            _hSc  = Num(pPar, "Профиль H", "10");
            _lSc  = Num(pPar, "Профиль L", "1");
            _csSc = Num(pPar, "Сечение (как L профиля)", "1");
            _csTh = Num(pPar, "Текст сечения", "0.5");
            _vpEp = Num(pPar, "Лист профиль 1:", "500");
            _vpCs = Num(pPar, "Лист сечения 1:", "200");
            _vpPlan = Num(pPar, "Лист план 1:", "500");

            pPar.Controls.Add(Head("Графика"));
            _blockCs  = Chk(pPar, "Сечения блоками", true);
            _showTags = Chk(pPar, "Подписи L0–L6", true);

            pTbl.Controls.Add(Head("PoperSwift"));
            _textH   = Num(pTbl, "Текст профиля", "2.5");
            _textOff = Num(pTbl, "Отступ текста, %", "10");
            _slopeU  = Combo(pTbl, "Единица уклона", new[] { "permille", "degrees", "ratio", "auto" });
            _labW    = Num(pTbl, "Ширина столбца (0=авто)", "0");
            _labH    = Num(pTbl, "Подпись отметки (L2-L5)", "");
            _showPerm = Chk(pTbl, "Знак промилле", true);
            _annot    = Chk(pTbl, "Аннотативный текст (от 1:1000)", false);
            _annGap   = Num(pTbl, "Отступ таблицы, м", "0");
            _drawProj = Chk(pTbl, "Линии проекции", true);

            pFact.Controls.Add(Head("Проект / факт"));
            _projFact = Chk(pFact, "Проект / факт", false);
            _singleSc = Chk(pFact, "Однострочный scatter", true);

            pFact.Controls.Add(Head("План"));
            _planEn  = Chk(pFact, "Ось и контур траншеи", true);
            _planInt = Num(pFact, "Интервал выносок, м", "50");
            _planMin = Num(pFact, "Мин. расстояние, м", "15");
            _planRotLbl = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(240, 0),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 4),
                Text = "Поворот ПСК: 0°"
            };
            pFact.Controls.Add(_planRotLbl);
            pFact.Controls.Add(Btn("Задать поворот точками…", PickPlanRot, 240));
            pFact.Controls.Add(Head("Слои плана по листам"));
            pFact.Controls.Add(Btn("Разработка…", () => PickPlanLayers(ProfileKind.Razrabotka), 240));
            pFact.Controls.Add(Btn("Песок…", () => PickPlanLayers(ProfileKind.Pesok), 240));
            pFact.Controls.Add(Btn("Труба…", () => PickPlanLayers(ProfileKind.Truba), 240));
            pFact.Controls.Add(Btn("Защитный слой…", () => PickPlanLayers(ProfileKind.Zashita), 240));
            pFact.Controls.Add(Btn("Обратная засыпка…", () => PickPlanLayers(ProfileKind.Zasypka), 240));

            pCov.Controls.Add(Head("Толщины покрытий, м"));
            _asph = Num(pCov, "Асфальт, м", "1.02");
            _trot = Num(pCov, "Тротуар, м", "0.31");
            _gazn = Num(pCov, "Газон, м", "0.15");
            pCov.Controls.Add(Head("L5 засыпка под покрытием"));
            var fillItems = new[] { "Песок", "Местный грунт" };
            _fillA = Combo(pCov, "Асфальт →", fillItems);
            _fillT = Combo(pCov, "Тротуар →", fillItems);
            _fillG = Combo(pCov, "Газон →", fillItems);
            _fillP = Combo(pCov, "Прочее / без зоны →", fillItems);
            _fillG.SelectedIndex = 1;
            _fillP.SelectedIndex = 1;
            _other = Num(pCov, "Прочее, м", "0.35");
            _zones = Num(pCov, "Зоны: участок1 | участок2", "");
            pCov.Controls.Add(Btn("Задать зоны на плане", PickZones, 240));
            _pavAll    = Chk(pCov, "Если зон нет — асфальт на всю трассу", true);
            _pickZones = Chk(pCov, "Скрыто", false);
            _pickZones.Visible = false;
            pCov.Controls.Add(Head("Поверхности (TIN)"));
            _cL0  = SurfCombo(pCov, "L0 проектное покрытие");
            _cL6  = SurfCombo(pCov, "L6 черная земля (нет = L0 − зоны)");
            _useBe = Chk(pCov, "Использовать L6 как черную землю", false);
            pCov.Controls.Add(Btn("Обновить список TIN", RefreshSurfaces, 240));

            pL1.Controls.Add(Head("Разработка L1"));
            _l1mh     = Chk(pL1, "Показывать колодцы", true);
            _tblOff   = Num(pL1, "Таблица ниже низа, м", "1.0");
            _labRoad  = Num(pL1, "Подпись L0 покрытия", "Проектно-планировочное покрытие, м");
            _labEarth = Num(pL1, "Подпись L6 черной земли", "Отметка земли, м");
            _labBot   = Num(pL1, "Подпись L1 дна траншеи", "Отметка дна траншеи, м");
            _btnCs    = ColorRow(pL1, "Линии сечения", AciToRgb(129));
            _btnRoad  = ColorRow(pL1, "L0 покрытие", Color.Red);
            _l1rl     = LtRow(pL1, "Тип линии L0", "Continuous");
            _btnEarth = ColorRow(pL1, "L6 черная земля", Color.Blue);
            _l1el     = LtRow(pL1, "Тип линии L6", "Continuous");
            _btnBot   = ColorRow(pL1, "L1 дно траншеи", Color.Red);
            _l1bl     = LtRow(pL1, "Тип линии L1", "Continuous");
            _btnHat   = HatchRow(pL1, "Штриховка разработки", Color.Blue);
            _btnPav   = HatchRow(pL1, "Штриховка одежды", Color.IndianRed);
            _l1preset = Num(pL1, "Пресет L1", "default");
            var pre = new FlowLayoutPanel { Width = 244, Height = 38, WrapContents = true, BackColor = Color.Transparent };
            pre.Controls.Add(Btn("Сохранить", SaveL1,  118));
            pre.Controls.Add(Btn("Загрузить", LoadL1, 118));
            pL1.Controls.Add(pre);
            pL1.Controls.Add(Head("Строки таблицы"));
            MakeRowConfigBtn(pL1, ProfileKind.Razrabotka, () => _defsL1, v => _defsL1 = v);

            _bedding = Num(pL2, "Толщина постели (песок), м", "0.10");
            _labTblL2 = Num(pL2, "Подпись строки в таблице профиля", "");
            _s2 = MakeSheet(pL2, NvkSession.L2, "L0 покрытие", "L2 верх песка", "L1 дно траншеи");
            pL2.Controls.Add(Head("Строки таблицы"));
            MakeRowConfigBtn(pL2, ProfileKind.Pesok, () => _defsL2, v => _defsL2 = v);
            _labTblL3 = Num(pL3, "Подпись строки в таблице профиля", "");
            _s3 = MakeSheet(pL3, NvkSession.L3, "L0 покрытие", "L3 лоток трубы", "L3 лоток трубы", false);
            _useWall = Chk(pL3, "Учитывать толщину стенки", true);
            pL3.Controls.Add(new Label
            {
                Text = "Каталог Корсис SN8: стенка = (Øнар − Øвн)/2. L2 = лоток − стенка.",
                AutoSize = true,
                MaximumSize = new Size(240, 0),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 4, 0, 4)
            });
            _wallGrid = MakeWallGrid(pL3);
            var wallBtns = new FlowLayoutPanel { Width = 244, Height = 38, WrapContents = true, BackColor = Color.Transparent };
            wallBtns.Controls.Add(Btn("Корсис SN8", ResetWalls,  118));
            wallBtns.Controls.Add(Btn("+ строка",   AddWallRow, 118));
            pL3.Controls.Add(wallBtns);
            pL3.Controls.Add(Head("Строки таблицы"));
            MakeRowConfigBtn(pL3, ProfileKind.Truba, () => _defsL3, v => _defsL3 = v);
            _zash = Num(pL4, "Толщина защитного слоя, м", "0.30");
            _labTblL4 = Num(pL4, "Подпись строки в таблице профиля", "");
            _s4 = MakeSheet(pL4, NvkSession.L4, "L0 покрытие", "L4 верх защиты", "L2 верх песка");
            pL4.Controls.Add(Head("Строки таблицы"));
            MakeRowConfigBtn(pL4, ProfileKind.Zashita, () => _defsL4, v => _defsL4 = v);
            _labTblL5 = Num(pL5, "Подпись строки в таблице профиля", "");
            _s5 = MakeSheet(pL5, NvkSession.L5, "L0 покрытие", "L5 засыпка / земля", "L4 верх защиты", true, true);
            pL5.Controls.Add(Head("Строки таблицы"));
            MakeRowConfigBtn(pL5, ProfileKind.Zasypka, () => _defsL5, v => _defsL5 = v);

            LoadFromState();
        }

        public void LoadFromState()
        {
            _bedding.Text = NvkUiState.Bedding.ToString("0.###");
            _zash.Text    = NvkUiState.ThkZash.ToString("0.###");
            _asph.Text    = NvkUiState.ThkAsph.ToString("0.###");
            _gazn.Text    = NvkUiState.ThkGazn.ToString("0.###");
            _trot.Text    = NvkUiState.ThkTrot.ToString("0.###");
            _trenchL.Text = NvkUiState.TrenchLeft.ToString("0.###");
            _trenchR.Text = NvkUiState.TrenchRight.ToString("0.###");
            _slopeL.Text  = NvkUiState.SlopeL.ToString("0.###");
            _slopeR.Text  = NvkUiState.SlopeR.ToString("0.###");
            _hwidth.Text  = NvkUiState.HalfWidth.ToString("0.#");
            _ygap.Text    = NvkUiState.CsYgap.ToString("0.#");
            Pick(_joint, NvkUiState.Joint);
            _hSc.Text     = NvkUiState.HScale.ToString("0.#");
            _lSc.Text     = NvkUiState.LScale.ToString("0.#");
            _csSc.Text    = NvkUiState.CsScale.ToString("0.#");
            _csTh.Text    = NvkUiState.CsTextH.ToString("0.##");
            _vpEp.Text    = NvkUiState.VpScaleEp.ToString("0");
            _vpCs.Text    = NvkUiState.VpScaleCs.ToString("0");
            _vpPlan.Text  = NvkUiState.VpScalePlan.ToString("0");
            _textH.Text   = NvkUiState.TextH.ToString("0.##");
            _textOff.Text = NvkUiState.TextOff.ToString("0.#");
            Pick(_slopeU, NvkUiState.SlopeUnit);
            _labW.Text   = NvkUiState.LabelColW.ToString("0.#");
            _labH.Text   = NvkUiState.LabelHeight ?? "";
            _showPerm.Checked  = NvkUiState.ShowPermille;
            _blockCs.Checked   = NvkUiState.BlockCs;
            _showTags.Checked  = NvkUiState.ShowLineTags;
            _stations.Text     = NvkUiState.Stations ?? "AUTO";
            _doCs.Checked      = NvkUiState.DoCs;
            _doEp.Checked      = NvkUiState.DoEp;
            _doPoper.Checked   = NvkUiState.DoPoper;
            _doVol.Checked     = NvkUiState.DoVol;
            _doPlan.Checked    = NvkUiState.DoPlan;
            _doSheets.Checked  = NvkUiState.DoSheets;
            _projFact.Checked  = NvkUiState.ProjFact;
            _singleSc.Checked  = NvkUiState.SingleScatter;
            _annot.Checked     = NvkUiState.Annotative;
            _annGap.Text       = NvkUiState.AnnGap.ToString("0.#");
            _pavAll.Checked    = NvkUiState.PavAll;
            _pickZones.Checked = NvkUiState.PickZones;
            _zones.Text        = NvkUiState.Zones ?? "";
            _planEn.Checked    = NvkUiState.PlanEn;
            _planInt.Text      = NvkUiState.PlanInt.ToString("0.#");
            _planMin.Text      = NvkUiState.PlanMin.ToString("0.#");
            _planRotLbl.Text   = "Поворот ПСК: " + NvkUiState.PlanRotDeg.ToString("0.###") + "°";
            _other.Text        = NvkUiState.ThkOther.ToString("0.###");
            SetFill(_fillA, NvkUiState.FillAsph);
            SetFill(_fillT, NvkUiState.FillTrot);
            SetFill(_fillG, NvkUiState.FillGazn);
            SetFill(_fillP, NvkUiState.FillOther);
            _useBe.Checked = NvkUiState.UseBlackEarth;
            RefreshSurfaces();
            PickSurf(_cL0, NvkUiState.DesignHandle);
            PickSurf(_cL6, NvkUiState.BlackEarthHandle);
            _l1mh.Checked    = NvkUiState.L1ShowMh;
            _tblOff.Text     = NvkUiState.TableOffsetM.ToString("0.##");
            _labRoad.Text    = string.IsNullOrEmpty(NvkUiState.LabelRoad)   ? "Проектно-планировочное покрытие, м"   : NvkUiState.LabelRoad;
            _labEarth.Text   = string.IsNullOrEmpty(NvkUiState.LabelEarth)  ? "Отметка земли, м"     : NvkUiState.LabelEarth;
            _labBot.Text     = string.IsNullOrEmpty(NvkUiState.LabelBottom) ? "Отметка дна траншеи, м"               : NvkUiState.LabelBottom;
            var st = NvkSession.L1 ?? L1Style.Default();
            PaintLine(_btnCs,    st.CsLine, AciToRgb(129));
            PaintLine(_btnRoad,  st.Road,   Color.Red);
            PaintLine(_btnEarth, st.Earth,  Color.Blue);
            PaintLine(_btnBot,   st.Bottom, Color.Red);
            SetLt(_l1rl, st.Road   != null && !string.IsNullOrEmpty(st.Road.Linetype)   ? st.Road.Linetype   : "DASHED");
            SetLt(_l1el, st.Earth  != null && !string.IsNullOrEmpty(st.Earth.Linetype)  ? st.Earth.Linetype  : "Continuous");
            SetLt(_l1bl, st.Bottom != null && !string.IsNullOrEmpty(st.Bottom.Linetype) ? st.Bottom.Linetype : "Continuous");
            PaintFill(_btnHat, st.HatchExcavation);
            PaintFill(_btnPav, st.HatchPavement);
            _l1preset.Text = NvkUiState.L1Preset ?? "default";
            PushSheet(_s2, NvkSession.L2);
            PushSheet(_s3, NvkSession.L3);
            PushSheet(_s4, NvkSession.L4);
            PushSheet(_s5, NvkSession.L5);
            _useWall.Checked  = NvkUiState.UsePipeWall;
            FillWallGrid(NvkUiState.Walls);
            _drawProj.Checked = NvkUiState.DrawProjections;
            _labTblL2.Text    = NvkUiState.TblL2Label ?? "";
            _labTblL3.Text    = NvkUiState.TblL3Label ?? "";
            _labTblL4.Text    = NvkUiState.TblL4Label ?? "";
            _labTblL5.Text    = NvkUiState.TblL5Label ?? "";
            _defsL1 = string.IsNullOrEmpty(NvkUiState.RowsL1) ? null : TableRowDef.DeserializeList(NvkUiState.RowsL1);
            _defsL2 = string.IsNullOrEmpty(NvkUiState.RowsL2) ? null : TableRowDef.EnsureCoverFirst(ProfileKind.Pesok, TableRowDef.DeserializeList(NvkUiState.RowsL2));
            _defsL3 = string.IsNullOrEmpty(NvkUiState.RowsL3) ? null : TableRowDef.EnsureCoverFirst(ProfileKind.Truba, TableRowDef.DeserializeList(NvkUiState.RowsL3));
            _defsL4 = string.IsNullOrEmpty(NvkUiState.RowsL4) ? null : TableRowDef.DeserializeList(NvkUiState.RowsL4);
            _defsL5 = string.IsNullOrEmpty(NvkUiState.RowsL5) ? null : TableRowDef.DeserializeList(NvkUiState.RowsL5);
            if (_defsL1 != null) RowScatter.ApplyLegacy(_defsL1, ProfileKind.Razrabotka);
            if (_defsL2 != null) RowScatter.ApplyLegacy(_defsL2, ProfileKind.Pesok);
            if (_defsL3 != null) RowScatter.ApplyLegacy(_defsL3, ProfileKind.Truba);
            if (_defsL4 != null) RowScatter.ApplyLegacy(_defsL4, ProfileKind.Zashita);
            if (_defsL5 != null) RowScatter.ApplyLegacy(_defsL5, ProfileKind.Zasypka);
            RefreshInfo();
        }

        public void SaveToState()
        {
            NvkUiState.Bedding   = Parse(_bedding, 0.10);
            NvkUiState.ThkZash   = Parse(_zash, 0.30);
            NvkUiState.ThkAsph   = Parse(_asph, 0.0);
            NvkUiState.ThkGazn   = Parse(_gazn, 0.0);
            NvkUiState.ThkTrot   = Parse(_trot, 0.0);
            NvkUiState.FillAsph  = FillCode(_fillA);
            NvkUiState.FillTrot  = FillCode(_fillT);
            NvkUiState.FillGazn  = FillCode(_fillG);
            NvkUiState.FillOther = FillCode(_fillP);
            NvkUiState.TrenchLeft  = Parse(_trenchL, 0.0);
            NvkUiState.TrenchRight = Parse(_trenchR, 0.0);
            if (NvkUiState.TrenchLeft < 0.0) NvkUiState.TrenchLeft = 0.0;
            if (NvkUiState.TrenchRight < 0.0) NvkUiState.TrenchRight = 0.0;
            NvkUiState.SlopeL    = Parse(_slopeL, 0.0);
            NvkUiState.SlopeR    = Parse(_slopeR, 0.0);
            if (NvkUiState.SlopeL < 0.0) NvkUiState.SlopeL = 0.0;
            if (NvkUiState.SlopeR < 0.0) NvkUiState.SlopeR = 0.0;
            NvkUiState.SlopeM    = NvkUiState.SlopeL > NvkUiState.SlopeR ? NvkUiState.SlopeL : NvkUiState.SlopeR;
            NvkUiState.HalfWidth = Parse(_hwidth, 3.0);
            NvkUiState.CsYgap    = Parse(_ygap, 100.0);
            NvkUiState.Joint     = _joint.SelectedItem as string ?? "SOCKET";
            NvkUiState.HScale    = Parse(_hSc, 10);
            NvkUiState.LScale    = Parse(_lSc, 1);
            NvkUiState.CsScale   = Parse(_csSc, 1);
            NvkUiState.CsTextH   = Parse(_csTh, 0.5);
            NvkUiState.VpScaleEp = Parse(_vpEp, 500);
            if (NvkUiState.VpScaleEp < 1) NvkUiState.VpScaleEp = 500;
            NvkUiState.VpScaleCs = Parse(_vpCs, 200);
            if (NvkUiState.VpScaleCs < 1) NvkUiState.VpScaleCs = 200;
            NvkUiState.VpScalePlan = Parse(_vpPlan, 500);
            if (NvkUiState.VpScalePlan < 1) NvkUiState.VpScalePlan = 500;
            NvkUiState.TextH     = Parse(_textH, 2.5);
            NvkUiState.TextOff   = Parse(_textOff, 10);
            NvkUiState.RoundTo = 2;
            NvkUiState.SlopeRound = 1;
            NvkUiState.SlopeUnit  = _slopeU.SelectedItem as string ?? "permille";
            NvkUiState.LabelColW  = Parse(_labW, 0);
            NvkUiState.LabelHeight = _labH.Text ?? "";
            NvkUiState.ShowPermille = _showPerm.Checked;
            NvkUiState.ShowLen      = true;
            NvkUiState.ShowSlope    = true;
            NvkUiState.BlockCs      = _blockCs.Checked;
            NvkUiState.BlockEp      = false;
            NvkUiState.ShowLineTags = _showTags.Checked;
            NvkUiState.Stations     = string.IsNullOrWhiteSpace(_stations.Text) ? "AUTO" : _stations.Text.Trim();
            NvkUiState.DoCs         = _doCs.Checked;
            NvkUiState.DoEp         = _doEp.Checked;
            NvkUiState.DoPoper      = _doPoper.Checked;
            NvkUiState.DoVol        = _doVol.Checked;
            NvkUiState.DoPlan       = _doPlan.Checked;
            NvkUiState.DoSheets     = _doSheets.Checked;
            NvkUiState.ProjFact     = _projFact.Checked;
            NvkUiState.SingleScatter     = _singleSc.Checked;
            NvkUiState.Annotative     = _annot.Checked;
            NvkUiState.AnnGap         = Parse(_annGap, 0);
            NvkUiState.PavAll         = _pavAll.Checked;
            NvkUiState.PickZones      = _pickZones.Checked;
            NvkUiState.Zones          = _zones.Text ?? "";
            NvkUiState.PlanEn         = _planEn.Checked;
            NvkUiState.PlanInt        = Parse(_planInt, 50);
            NvkUiState.PlanMin        = Parse(_planMin, 15);
            NvkUiState.DrawProjections = _drawProj.Checked;
            NvkUiState.UseTextHeight   = true;
            NvkUiState.DeltaH          = true;
            NvkUiState.TblL2Label      = _labTblL2.Text ?? "";
            NvkUiState.TblL3Label      = _labTblL3.Text ?? "";
            NvkUiState.TblL4Label      = _labTblL4.Text ?? "";
            NvkUiState.TblL5Label      = _labTblL5.Text ?? "";
            NvkUiState.RowsL1 = _defsL1 != null && _defsL1.Count > 0 ? TableRowDef.SerializeList(_defsL1) : "";
            NvkUiState.RowsL2 = _defsL2 != null && _defsL2.Count > 0 ? TableRowDef.SerializeList(_defsL2) : "";
            NvkUiState.RowsL3 = _defsL3 != null && _defsL3.Count > 0 ? TableRowDef.SerializeList(_defsL3) : "";
            NvkUiState.RowsL4 = _defsL4 != null && _defsL4.Count > 0 ? TableRowDef.SerializeList(_defsL4) : "";
            NvkUiState.RowsL5 = _defsL5 != null && _defsL5.Count > 0 ? TableRowDef.SerializeList(_defsL5) : "";
            RowScatter.SyncOwners();
            PullDefsFromState();
            NvkUiState.SyncScatterFromRows();
            NvkUiState.ThkOther        = Parse(_other, 0.35);
            NvkUiState.UseBlackEarth   = _useBe.Checked;
            if (_cL0 != null && _cL0.Items.Count > 0)
            {
                NvkUiState.DesignHandle = SurfHandle(_cL0);
                NvkUiState.DesignName   = SurfName(_cL0);
            }
            if (_cL6 != null && _cL6.Items.Count > 0)
            {
                NvkUiState.BlackEarthHandle = SurfHandle(_cL6);
                NvkUiState.BlackEarthName   = SurfName(_cL6);
            }
            if (string.IsNullOrEmpty(NvkUiState.BlackEarthHandle))
                NvkUiState.UseBlackEarth = false;
            NvkUiState.L1ShowMh      = _l1mh.Checked;
            NvkUiState.TableOffsetM  = Parse(_tblOff, 1.0);
            NvkUiState.LabelRoad     = _labRoad.Text  ?? "";
            NvkUiState.LabelEarth    = _labEarth.Text ?? "";
            NvkUiState.LabelBottom   = _labBot.Text   ?? "";
            NvkUiState.L1RoadLt      = LtName(_l1rl, "DASHED");
            NvkUiState.L1EarthLt     = LtName(_l1el, "Continuous");
            NvkUiState.L1BotLt       = LtName(_l1bl, "Continuous");
            NvkUiState.L1Preset      = _l1preset.Text ?? "default";
            if (NvkSession.L1 == null) NvkSession.L1 = L1Style.Default();
            if (NvkSession.L1.Road   == null) NvkSession.L1.Road   = LineStyle.Rgb(255,   0, 0, "DASHED",     30);
            NvkSession.L1.Road.Linetype = LtName(_l1rl, "DASHED");
            if (NvkSession.L1.Earth  == null) NvkSession.L1.Earth  = LineStyle.Rgb(  0,   0, 255, "Continuous", 30);
            NvkSession.L1.Earth.Linetype = LtName(_l1el, "Continuous");
            if (NvkSession.L1.Bottom == null) NvkSession.L1.Bottom = LineStyle.Rgb(255,   0, 0, "Continuous", 30);
            NvkSession.L1.Bottom.Linetype = LtName(_l1bl, "Continuous");
            if (NvkSession.L1.CsLine == null) NvkSession.L1.CsLine = LineStyle.Of(129, "Continuous", 30);
            CopyRgb(_btnCs,    NvkSession.L1.CsLine);
            CopyRgb(_btnRoad,  NvkSession.L1.Road);
            CopyRgb(_btnEarth, NvkSession.L1.Earth  ?? (NvkSession.L1.Earth  = LineStyle.Rgb(  0,   0, 255, "Continuous", 30)));
            CopyRgb(_btnBot,   NvkSession.L1.Bottom ?? (NvkSession.L1.Bottom = LineStyle.Rgb(255,   0,   0, "Continuous", 30)));
            CopyFill(_btnHat, NvkSession.L1.HatchExcavation ?? (NvkSession.L1.HatchExcavation = FillStyle.Of("ANSI31", 5, 1)));
            CopyFill(_btnPav, NvkSession.L1.HatchPavement   ?? (NvkSession.L1.HatchPavement   = FillStyle.Rgb("ANSI31", 200,  80,  80, 1)));
            NvkUiState.L1HatPat  = NvkSession.L1.HatchExcavation.Pattern;
            NvkUiState.L1PavPat  = NvkSession.L1.HatchPavement.Pattern;
            NvkUiState.HasPush   = true;
            NvkUiState.SyncL1();
            PullSheet(_s2, NvkSession.L2);
            PullSheet(_s3, NvkSession.L3, false);
            PullSheet(_s4, NvkSession.L4);
            PullSheet(_s5, NvkSession.L5);
            NvkUiState.UsePipeWall = _useWall.Checked;
            NvkUiState.Walls       = ReadWallGrid();
            if (NvkSession.L3 != null) NvkSession.L3.Hatch = null;
        }

        void SaveL1()
        {
            SaveToState();
            if (NvkSession.L1 == null) NvkSession.L1 = L1Style.Default();
            NvkSession.L1.Name = string.IsNullOrWhiteSpace(_l1preset.Text) ? "default" : _l1preset.Text.Trim();
            L1PresetStore.Save(NvkSession.L1);
            _status.Text = "Пресет L1 сохранён: " + NvkSession.L1.Name;
        }

        public System.Collections.Generic.List<TableRowDef> GetDefs(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: return _defsL1;
                case ProfileKind.Pesok:      return _defsL2;
                case ProfileKind.Truba:      return _defsL3;
                case ProfileKind.Zashita:    return _defsL4;
                case ProfileKind.Zasypka:    return _defsL5;
                default:                     return null;
            }
        }

        public void SetDefs(ProfileKind kind, System.Collections.Generic.List<TableRowDef> defs)
        {
            switch (kind)
            {
                case ProfileKind.Razrabotka: _defsL1 = defs; break;
                case ProfileKind.Pesok:      _defsL2 = defs; break;
                case ProfileKind.Truba:      _defsL3 = defs; break;
                case ProfileKind.Zashita:    _defsL4 = defs; break;
                case ProfileKind.Zasypka:    _defsL5 = defs; break;
            }
        }

        public void PullDefsFromState()
        {
            if (!string.IsNullOrEmpty(NvkUiState.RowsL1))
                _defsL1 = TableRowDef.DeserializeList(NvkUiState.RowsL1);
            if (!string.IsNullOrEmpty(NvkUiState.RowsL2))
                _defsL2 = TableRowDef.EnsureCoverFirst(ProfileKind.Pesok, TableRowDef.DeserializeList(NvkUiState.RowsL2));
            if (!string.IsNullOrEmpty(NvkUiState.RowsL3))
                _defsL3 = TableRowDef.EnsureCoverFirst(ProfileKind.Truba, TableRowDef.DeserializeList(NvkUiState.RowsL3));
            if (!string.IsNullOrEmpty(NvkUiState.RowsL4))
                _defsL4 = TableRowDef.DeserializeList(NvkUiState.RowsL4);
            if (!string.IsNullOrEmpty(NvkUiState.RowsL5))
                _defsL5 = TableRowDef.DeserializeList(NvkUiState.RowsL5);
        }

        void LoadL1()
        {
            string n = string.IsNullOrWhiteSpace(_l1preset.Text) ? "default" : _l1preset.Text.Trim();
            NvkUiState.FromL1(L1PresetStore.Load(n));
            LoadFromState();
            _status.Text = "Пресет L1: " + n;
        }

        public void RefreshInfo()
        {
            var last = NvkSession.Last;
            if (last == null || last.Chain == null)
            {
                _info.Text = "Домена нет. Построить спросит трубы и TIN.";
                return;
            }
            _info.Text = "Труб " + last.Chain.Segs.Count
                + "   L=" + last.Chain.Total.ToString("0.0") + " м"
                + "\nTIN " + (last.SurfName ?? "-")
                + "   сечений " + (last.Geoms == null ? 0 : last.Geoms.Count);
        }

        public void ShowStatus(string text)
        {
            if (_status == null) return;
            try
            {
                if (_status.InvokeRequired)
                    _status.BeginInvoke(new Action(() => _status.Text = text ?? ""));
                else
                    _status.Text = text ?? "";
            }
            catch { }
        }

        void Build()
        {
            try
            {
                SaveToState();
                ShowStatus("Прогон… смотри командную строку.");
                NvkSend.Command("НВК-ПАНЕЛЬ-ПРОГОН");
            }
            catch (Exception ex)
            {
                ShowStatus("Сбой: " + ex.Message);
                NvkLog.Write("[НВК] панель Обновить: " + ex.Message);
            }
        }

        static void Send(string lisp)
        {
            NvkSend.Command(lisp);
        }

        static void Pick(ComboBox cb, string v)
        {
            if (string.IsNullOrEmpty(v)) { if (cb.Items.Count > 0) cb.SelectedIndex = 0; return; }
            int i = cb.FindStringExact(v);
            cb.SelectedIndex = i >= 0 ? i : 0;
        }

        static double Parse(TextBox tb, double def)
        {
            double x;
            string s = tb.Text.Trim().Replace(',', '.');
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out x))
                return x;
            return def;
        }

        // ── Visual helpers ────────────────────────────────────────────────────

        static FlowLayoutPanel Page(TabControl tabs, string title)
        {
            var page = new TabPage(title) { BackColor = C_CARD, Padding = new Padding(0) };
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                Dock          = DockStyle.Fill,
                AutoScroll    = true,
                Padding       = new Padding(10, 10, 6, 10),
                BackColor     = C_CARD
            };
            page.Controls.Add(flow);
            tabs.TabPages.Add(page);
            return flow;
        }

        static Control Head(string t)
        {
            var p = new Panel
            {
                Width     = 242,
                Height    = 22,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 14, 0, 4)
            };
            p.Controls.Add(new Panel
            {
                Bounds    = new Rectangle(0, 3, 3, 16),
                BackColor = C_ACCENT
            });
            p.Controls.Add(new Label
            {
                Text      = t.ToUpperInvariant(),
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = C_ACCENT,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Bounds    = new Rectangle(10, 0, 228, 22),
                TextAlign = ContentAlignment.MiddleLeft
            });
            return p;
        }

        static TextBox Num(Control parent, string cap, string val)
        {
            parent.Controls.Add(new Label
            {
                Text      = cap,
                AutoSize  = true,
                Margin    = new Padding(0, 5, 0, 1),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8f)
            });
            var tb = new TextBox
            {
                Text        = val,
                Width       = 240,
                BackColor   = C_INPUT,
                ForeColor   = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 9f),
                Margin      = new Padding(0, 0, 0, 2)
            };
            parent.Controls.Add(tb);
            return tb;
        }

        static CheckBox Chk(Control parent, string cap, bool on)
        {
            var c = new ToggleSwitch
            {
                Text      = cap,
                Checked   = on,
                Width     = 242,
                Height    = 26,
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 3, 0, 3)
            };
            parent.Controls.Add(c);
            return c;
        }

        static ComboBox Combo(Control parent, string cap, string[] items)
        {
            parent.Controls.Add(new Label
            {
                Text      = cap,
                AutoSize  = true,
                Margin    = new Padding(0, 5, 0, 1),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8f)
            });
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 240,
                BackColor     = C_INPUT,
                ForeColor     = C_TEXT,
                Font          = new Font("Segoe UI", 9f),
                Margin        = new Padding(0, 0, 0, 2),
                DrawMode      = DrawMode.OwnerDrawFixed,
                ItemHeight    = 22
            };
            cb.Items.AddRange(items);
            cb.SelectedIndex = 0;
            cb.DrawItem += DrawComboItem;
            parent.Controls.Add(cb);
            return cb;
        }

        static void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var cb = (ComboBox)sender;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool face = (e.State & DrawItemState.ComboBoxEdit) != 0;
            var bg = face ? C_INPUT : (sel ? C_SEL : C_INPUT);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);
            var r = e.Bounds;
            r.X += 4;
            TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font, r,
                C_TEXT, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        static Button Btn(string cap, Action act, int w, bool primary = false)
        {
            var bg = primary ? C_PRIM : C_BTN;
            var b = new Button
            {
                Text      = cap,
                Width     = w,
                Height    = 30,
                BackColor = bg,
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f),
                Cursor    = Cursors.Hand,
                Margin    = new Padding(2, 2, 2, 2)
            };
            b.FlatAppearance.BorderColor     = primary ? C_ACCENT : C_BORDER;
            b.FlatAppearance.BorderSize      = 1;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.12f);
            b.Click += (s, e) => act();
            return b;
        }

        static void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            string txt = tabs.TabPages[e.Index].Text;
            bool sel   = (e.State & DrawItemState.Selected) != 0;

            // Background
            var bgColor = sel ? C_SEL : C_STRIP;
            using (var br = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(br, e.Bounds);

            // Left accent bar on selected tab
            if (sel)
            {
                Color bar = C_ACCENT;
                if      (txt.StartsWith("L1")) bar = Color.FromArgb(200,  70,  70);
                else if (txt.StartsWith("L2")) bar = Color.FromArgb(200, 170,  50);
                else if (txt.StartsWith("L3")) bar = Color.FromArgb( 50, 160, 200);
                else if (txt.StartsWith("L4")) bar = Color.FromArgb( 60, 180,  90);
                else if (txt.StartsWith("L5")) bar = Color.FromArgb(200, 120,  50);
                using (var br = new SolidBrush(bar))
                    e.Graphics.FillRectangle(br, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }

            // Text
            Color fg = sel ? C_TEXT : C_DIM;
            TextRenderer.DrawText(e.Graphics, txt, tabs.Font, e.Bounds, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        // ─────────────────────────────────────────────────────────────────────

        void PickZones()
        {
            SaveToState();
            _status.Text = "Зоны: кликай пары точек на плане. Enter — следующий тип.";
            Send("НВК-ЗОНЫ ");
        }

        void PickPlanRot()
        {
            SaveToState();
            _status.Text = "План: две точки — направление оси X.";
            Send("НВК-ПОВОРОТ-ПЛАНА ");
        }

        void PickPlanLayers(ProfileKind kind)
        {
            SaveToState();
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                _status.Text = "Нет чертежа";
                return;
            }
            var names = new List<string>();
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var lt = (Autodesk.AutoCAD.DatabaseServices.LayerTable)tr.GetObject(doc.Database.LayerTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                    foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in lt)
                    {
                        var rec = (Autodesk.AutoCAD.DatabaseServices.LayerTableRecord)tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                        if (rec.IsDependent) continue;
                        names.Add(rec.Name);
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                _status.Text = "Слои: " + ex.Message;
                return;
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            var on = PlanView.SplitLayers(NvkUiState.PlanVpOnFor(kind));
            bool freezeSamp = kind == ProfileKind.Truba;
            using (var f = new Form())
            {
                f.Text = "Слои плана — " + NvkSheets.NameOf(kind);
                f.Width = 380;
                f.Height = 560;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = C_BG;
                f.ForeColor = C_TEXT;
                var list = new CheckedListBox
                {
                    Dock = DockStyle.Fill,
                    CheckOnClick = true,
                    BackColor = C_INPUT,
                    ForeColor = C_TEXT,
                    BorderStyle = BorderStyle.FixedSingle
                };
                foreach (var n in names)
                    list.Items.Add(n, PlanView.IsOn(n, on, freezeSamp));
                var bar = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 68,
                    BackColor = C_BG
                };
                var ours = new Button
                {
                    Text = "Только наши",
                    Dock = DockStyle.Top,
                    Height = 32,
                    BackColor = C_BTN,
                    ForeColor = C_TEXT,
                    FlatStyle = FlatStyle.Flat
                };
                ours.Click += (s, e) =>
                {
                    for (int i = 0; i < list.Items.Count; i++)
                        list.SetItemChecked(i, PlanView.IsOn(list.Items[i].ToString(), null, freezeSamp));
                };
                var ok = new Button
                {
                    Text = "OK",
                    Dock = DockStyle.Bottom,
                    Height = 32,
                    DialogResult = DialogResult.OK,
                    BackColor = C_PRIM,
                    ForeColor = C_TEXT,
                    FlatStyle = FlatStyle.Flat
                };
                bar.Controls.Add(ours);
                bar.Controls.Add(ok);
                f.Controls.Add(list);
                f.Controls.Add(bar);
                f.AcceptButton = ok;
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var sel = new List<string>();
                for (int i = 0; i < list.Items.Count; i++)
                    if (list.GetItemChecked(i)) sel.Add(list.Items[i].ToString());
                bool onlyOurs = true;
                for (int i = 0; i < sel.Count; i++)
                {
                    if (!PlanView.IsOurs(sel[i])) { onlyOurs = false; break; }
                }
                if (onlyOurs)
                {
                    for (int i = 0; i < names.Count; i++)
                    {
                        if (freezeSamp && PlanView.IsSampLeader(names[i])) continue;
                        if (PlanView.IsOurs(names[i]) && !PlanView.IsOn(names[i], sel, freezeSamp))
                        {
                            onlyOurs = false;
                            break;
                        }
                    }
                }
                NvkUiState.SetPlanVpOn(kind, onlyOurs ? "" : PlanView.JoinLayers(sel));
                _status.Text = NvkSheets.NameOf(kind) + ": слои "
                    + (onlyOurs ? "только наши" : sel.Count.ToString());
            }
        }

        static string FillCode(ComboBox cb)
        {
            if (cb == null || cb.SelectedIndex <= 0) return "SAND";
            return "SOIL";
        }

        static void SetFill(ComboBox cb, string code)
        {
            if (cb == null) return;
            bool sand = string.IsNullOrEmpty(code)
                || code.Equals("SAND", StringComparison.OrdinalIgnoreCase)
                || code.Equals("Песок", StringComparison.OrdinalIgnoreCase);
            cb.SelectedIndex = sand ? 0 : 1;
        }

        static Button ColorRow(Control parent, string cap, Color c)
        {
            var row = new Panel
            {
                Width     = 242,
                Height    = 26,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 4, 0, 2)
            };
            var swatch = new Button
            {
                Bounds    = new Rectangle(0, 2, 22, 22),
                BackColor = c,
                FlatStyle = FlatStyle.Flat,
                Text      = "",
                Cursor    = Cursors.Hand
            };
            swatch.FlatAppearance.BorderColor = C_BORDER;
            swatch.FlatAppearance.BorderSize  = 1;
            var lbl = new Label
            {
                Text      = cap,
                Bounds    = new Rectangle(30, 0, 212, 26),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 9f),
                Cursor    = Cursors.Hand
            };
            swatch.Click += (s, ev) => OpenColorPicker(swatch);
            lbl.Click    += (s, ev) => OpenColorPicker(swatch);
            row.Controls.Add(swatch);
            row.Controls.Add(lbl);
            parent.Controls.Add(row);
            return swatch;
        }

        static void OpenColorPicker(Button swatch)
        {
            using (var d = new ColorDialog { Color = swatch.BackColor, FullOpen = true, AnyColor = true })
                if (d.ShowDialog() == DialogResult.OK)
                    swatch.BackColor = d.Color;
        }

        Button HatchRow(Control parent, string cap, Color c)
        {
            var fill = FillStyle.Rgb("ANSI31", c.R, c.G, c.B, 1.0);
            var row = new Panel
            {
                Width     = 242,
                Height    = 26,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 4, 0, 2)
            };
            var swatch = new Button
            {
                Bounds    = new Rectangle(0, 2, 22, 22),
                BackColor = c,
                FlatStyle = FlatStyle.Flat,
                Text      = "",
                Tag       = fill,
                Cursor    = Cursors.Hand
            };
            swatch.FlatAppearance.BorderColor = C_BORDER;
            swatch.FlatAppearance.BorderSize  = 1;
            var lbl = new Label
            {
                Text      = cap + " — " + fill.Pattern,
                Bounds    = new Rectangle(30, 0, 212, 26),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 9f),
                Tag       = fill,
                Cursor    = Cursors.Hand
            };
            swatch.Click += (s, ev) => { EditHatch(swatch); lbl.Text = cap + " — " + (swatch.Tag as FillStyle)?.Pattern; };
            lbl.Click    += (s, ev) => { EditHatch(swatch); lbl.Text = cap + " — " + (swatch.Tag as FillStyle)?.Pattern; };
            row.Controls.Add(swatch);
            row.Controls.Add(lbl);
            parent.Controls.Add(row);
            return swatch;
        }

        static void EditHatch(Button b)
        {
            var fill = b.Tag as FillStyle ?? FillStyle.Rgb("ANSI31", b.BackColor.R, b.BackColor.G, b.BackColor.B, 1);
            using (var f = new Form
            {
                Text            = "Штриховка",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(316, 220),
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = C_CARD
            })
            {
                var cb = new ComboBox
                {
                    Left          = 12,
                    Top           = 14,
                    Width         = 288,
                    Height        = 28,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    DrawMode      = DrawMode.OwnerDrawFixed,
                    ItemHeight    = 22,
                    BackColor     = C_INPUT,
                    ForeColor     = C_TEXT
                };
                var hats = DrawAssets.Hatches();
                foreach (var n in hats) cb.Items.Add(n);
                if (cb.Items.Count == 0) cb.Items.Add("ANSI31");
                int hi = cb.FindStringExact(fill.Pattern ?? "ANSI31");
                cb.SelectedIndex = hi >= 0 ? hi : 0;
                cb.DrawItem += (s, ev) =>
                {
                    ev.DrawBackground();
                    if (ev.Index < 0) return;
                    TextRenderer.DrawText(ev.Graphics, cb.Items[ev.Index].ToString(), cb.Font,
                        ev.Bounds, ev.ForeColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                };
                Color fillRgb = (fill.R == 0 && fill.G == 0 && fill.B == 0 && fill.Color > 0)
                    ? AciToRgb(fill.Color)
                    : Color.FromArgb(fill.R, fill.G, fill.B);
                var col = new Button
                {
                    Left      = 12,
                    Top       = 52,
                    Width     = 288,
                    Height    = 28,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = fillRgb,
                    Text      = "цвет штриховки…"
                };
                col.FlatAppearance.BorderColor = C_BORDER;
                col.Click += (s, ev) =>
                {
                    using (var d = new ColorDialog { Color = col.BackColor, FullOpen = true, AnyColor = true })
                        if (d.ShowDialog() == DialogResult.OK) col.BackColor = d.Color;
                };
                var scLbl = new Label { Left = 12, Top = 96, Text = "Масштаб", AutoSize = true,
                    ForeColor = C_DIM, BackColor = Color.Transparent };
                var sc = new TextBox
                {
                    Left        = 100,
                    Top         = 93,
                    Width       = 80,
                    Text        = fill.Scale.ToString("0.##"),
                    BackColor   = C_INPUT,
                    ForeColor   = C_TEXT,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var ok = new Button
                {
                    Text          = "OK",
                    Left          = 110,
                    Top           = 138,
                    Width         = 90,
                    Height        = 32,
                    DialogResult  = DialogResult.OK,
                    FlatStyle     = FlatStyle.Flat,
                    BackColor     = C_PRIM,
                    ForeColor     = C_TEXT
                };
                ok.FlatAppearance.BorderColor = C_ACCENT;
                f.Controls.Add(cb);
                f.Controls.Add(col);
                f.Controls.Add(scLbl);
                f.Controls.Add(sc);
                f.Controls.Add(ok);
                f.AcceptButton = ok;
                if (f.ShowDialog() != DialogResult.OK) return;
                double scv;
                if (!double.TryParse(sc.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out scv) || scv <= 0)
                    scv = 1.0;
                fill.Pattern = cb.SelectedItem != null ? cb.SelectedItem.ToString() : "ANSI31";
                fill.R       = col.BackColor.R;
                fill.G       = col.BackColor.G;
                fill.B       = col.BackColor.B;
                fill.Color   = 0;
                fill.Scale   = scv;
                b.Tag        = fill;
                b.BackColor  = col.BackColor;
                b.ForeColor  = col.BackColor.GetBrightness() > 0.55f
                    ? Color.FromArgb(20, 20, 20) : Color.White;
                b.Text       = fill.Pattern;
            }
        }

        static ComboBox SurfCombo(Control parent, string cap)
        {
            parent.Controls.Add(new Label
            {
                Text      = cap,
                AutoSize  = true,
                Margin    = new Padding(0, 5, 0, 1),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8f)
            });
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 240,
                BackColor     = C_INPUT,
                ForeColor     = C_TEXT,
                Font          = new Font("Segoe UI", 9f),
                Margin        = new Padding(0, 0, 0, 2),
                DrawMode      = DrawMode.OwnerDrawFixed,
                ItemHeight    = 22
            };
            cb.DrawItem += DrawComboItem;
            parent.Controls.Add(cb);
            return cb;
        }

        void RefreshSurfaces()
        {
            var items = new System.Collections.Generic.List<SurfItem>();
            items.Add(new SurfItem { Handle = "", Name = "(нет)" });
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        foreach (var s in SurfaceSampler.ListAll(doc.Database, tr))
                            items.Add(new SurfItem { Handle = s.Handle, Name = s.Name });
                        tr.Commit();
                    }
                }
            }
            catch { }
            FillSurf(_cL0, items, NvkUiState.DesignHandle);
            FillSurf(_cL6, items, NvkUiState.BlackEarthHandle);
        }

        static void FillSurf(ComboBox cb, System.Collections.Generic.List<SurfItem> items, string handle)
        {
            if (cb == null) return;
            cb.Items.Clear();
            foreach (var it in items) cb.Items.Add(it);
            PickSurf(cb, handle);
        }

        static void PickSurf(ComboBox cb, string handle)
        {
            if (cb == null || cb.Items.Count == 0) return;
            if (string.IsNullOrEmpty(handle)) { cb.SelectedIndex = 0; return; }
            for (int i = 0; i < cb.Items.Count; i++)
            {
                var it = cb.Items[i] as SurfItem;
                if (it != null && string.Equals(it.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            cb.SelectedIndex = 0;
        }

        static string SurfHandle(ComboBox cb)
        {
            var it = cb != null ? cb.SelectedItem as SurfItem : null;
            return it == null ? "" : (it.Handle ?? "");
        }

        static string SurfName(ComboBox cb)
        {
            var it = cb != null ? cb.SelectedItem as SurfItem : null;
            if (it == null || string.IsNullOrEmpty(it.Handle)) return "";
            return it.Name ?? "";
        }

        static void PaintLine(Button b, LineStyle st, Color fallback)
        {
            if (b == null) return;
            Color c;
            if (st == null) c = fallback;
            else if (st.R == 0 && st.G == 0 && st.B == 0 && st.Color > 0) c = AciToRgb(st.Color);
            else c = Color.FromArgb(st.R, st.G, st.B);
            b.BackColor = c;
            b.ForeColor = c.GetBrightness() > 0.55f ? Color.FromArgb(20, 20, 20) : Color.White;
        }

        static void PaintFill(Button b, FillStyle st)
        {
            if (b == null) return;
            if (st == null)
            {
                b.BackColor = Color.Silver;
                b.Text = "ANSI31";
                b.Tag  = FillStyle.Rgb("ANSI31", 160, 160, 160, 1);
                b.ForeColor = Color.FromArgb(20, 20, 20);
                return;
            }
            b.Tag  = st;
            b.Text = string.IsNullOrEmpty(st.Pattern) ? "ANSI31" : st.Pattern;
            Color c = (st.R == 0 && st.G == 0 && st.B == 0 && st.Color > 0)
                ? AciToRgb(st.Color)
                : Color.FromArgb(st.R, st.G, st.B);
            b.BackColor = c;
            b.ForeColor = c.GetBrightness() > 0.55f ? Color.FromArgb(20, 20, 20) : Color.White;
        }

        static void CopyRgb(Button b, LineStyle st)
        {
            if (b == null || st == null) return;
            st.R = b.BackColor.R;
            st.G = b.BackColor.G;
            st.B = b.BackColor.B;
            st.Color = 0;
        }

        static void CopyFill(Button b, FillStyle st)
        {
            if (b == null || st == null) return;
            var t = b.Tag as FillStyle;
            if (t != null)
            {
                st.Pattern = t.Pattern;
                st.R       = t.R;
                st.G       = t.G;
                st.B       = t.B;
                st.Scale   = t.Scale;
                st.Color   = 0;
            }
            else
            {
                st.R     = b.BackColor.R;
                st.G     = b.BackColor.G;
                st.B     = b.BackColor.B;
                st.Color = 0;
            }
        }

        static Color AciToRgb(short aci)
        {
            try
            {
                var c = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, aci);
                var sys = c.ColorValue;
                return Color.FromArgb(sys.R, sys.G, sys.B);
            }
            catch
            {
                switch (aci)
                {
                    case 1: return Color.Red;
                    case 2: return Color.Yellow;
                    case 3: return Color.Lime;
                    case 4: return Color.Cyan;
                    case 5: return Color.Blue;
                    case 6: return Color.Magenta;
                    case 7: return Color.White;
                    case 8: return Color.Gray;
                    case 129: return Color.FromArgb(0, 255, 191);
                    default: return Color.Gray;
                }
            }
        }

        static ComboBox LtRow(Control parent, string cap, string current)
        {
            if (string.IsNullOrEmpty(current)) current = "Continuous";
            parent.Controls.Add(new Label
            {
                Text      = cap,
                AutoSize  = true,
                Margin    = new Padding(0, 4, 0, 1),
                ForeColor = C_DIM,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8f)
            });
            var cb = new ComboBox
            {
                Width         = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode      = DrawMode.OwnerDrawFixed,
                ItemHeight    = 24,
                BackColor     = C_INPUT,
                ForeColor     = C_TEXT,
                Margin        = new Padding(0, 0, 0, 2),
                Tag           = current
            };
            cb.Items.Add(current);
            cb.SelectedIndex = 0;
            cb.DropDown += (s, ev) =>
            {
                string cur = cb.Tag as string ?? "Continuous";
                var names = DrawAssets.Linetypes();
                if (names.Count == 0) names.Add("Continuous");
                if (!names.Exists(x => string.Equals(x, cur, StringComparison.OrdinalIgnoreCase)))
                    names.Insert(0, cur);
                cb.Items.Clear();
                foreach (var n in names) cb.Items.Add(n);
                int i = cb.FindStringExact(cur);
                if (i >= 0) cb.SelectedIndex = i;
            };
            cb.SelectedIndexChanged += (s, ev) =>
            {
                if (cb.SelectedItem != null) cb.Tag = cb.SelectedItem.ToString();
            };
            cb.DrawItem += DrawLtItem;
            parent.Controls.Add(cb);
            return cb;
        }

        static void DrawLtItem(object sender, DrawItemEventArgs ev)
        {
            if (ev.Index < 0) return;
            var cb = (ComboBox)sender;
            bool sel  = (ev.State & DrawItemState.Selected)    != 0;
            bool face = (ev.State & DrawItemState.ComboBoxEdit) != 0;
            using (var br = new SolidBrush(sel && !face ? C_SEL : C_INPUT))
                ev.Graphics.FillRectangle(br, ev.Bounds);
            string nm = cb.Items[ev.Index].ToString();
            var r = ev.Bounds;
            var lineR = new Rectangle(r.X + 4, r.Y + r.Height / 2, 72, 1);
            using (var pen = new Pen(C_TEXT, 1.5f))
            {
                string u = nm.ToUpperInvariant();
                if (u.Contains("DASH") || u.Contains("HIDDEN") || u.Contains("ISO02") || u.Contains("ISO03"))
                    pen.DashPattern = new[] { 5f, 3f };
                else if (u.Contains("CENTER") || u.Contains("ISO04") || u.Contains("ISO05"))
                    pen.DashPattern = new[] { 9f, 2f, 2f, 2f };
                else if (u.Contains("DOT") || u.Contains("ISO07"))
                    pen.DashPattern = new[] { 1f, 3f };
                else if (u.Contains("PHANTOM") || u.Contains("DIVIDE"))
                    pen.DashPattern = new[] { 8f, 2f, 2f, 2f, 2f, 2f };
                ev.Graphics.DrawLine(pen, lineR.Left, lineR.Top, lineR.Right, lineR.Top);
            }
            var tr2 = new Rectangle(r.X + 82, r.Y, r.Width - 86, r.Height);
            TextRenderer.DrawText(ev.Graphics, nm, cb.Font, tr2, C_TEXT,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        static void SetLt(ComboBox cb, string n)
        {
            if (cb == null) return;
            if (string.IsNullOrEmpty(n)) n = "Continuous";
            cb.Tag = n;
            int i = cb.FindStringExact(n);
            if (i >= 0) { cb.SelectedIndex = i; return; }
            // not in list yet — update placeholder
            if (cb.Items.Count == 0) cb.Items.Add(n);
            else cb.Items[0] = n;
            cb.SelectedIndex = 0;
        }

        static string LtName(ComboBox cb, string def)
        {
            if (cb == null) return def;
            string t = cb.Tag as string;
            if (!string.IsNullOrEmpty(t)) return t;
            return cb.SelectedItem != null ? cb.SelectedItem.ToString() : def;
        }

        // ── Row-grid helpers ──────────────────────────────────────────────────

        void MakeRowConfigBtn(Control parent, ProfileKind kind,
            Func<System.Collections.Generic.List<TableRowDef>> getter,
            Action<System.Collections.Generic.List<TableRowDef>> setter)
        {
            var btn = Btn("⚙ Строки таблицы...", () =>
            {
                NvkPalette.ShowRows(kind);
            }, 244);
            parent.Controls.Add(btn);
        }

        // ─────────────────────────────────────────────────────────────────────

        DataGridView MakeWallGrid(Control parent)
        {
            var g = new DataGridView
            {
                Width                   = 244,
                Height                  = 148,
                AllowUserToAddRows      = true,
                AllowUserToDeleteRows   = true,
                RowHeadersVisible       = false,
                AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor         = C_INPUT,
                GridColor               = C_BORDER,
                BorderStyle             = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor          = C_INPUT,
                    ForeColor          = C_TEXT,
                    SelectionBackColor = C_SEL,
                    SelectionForeColor = Color.White
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor          = C_STRIP,
                    ForeColor          = C_DIM,
                    SelectionBackColor = C_STRIP
                }
            };
            g.Columns.Add("od", "Ø нар.");
            g.Columns.Add("id", "Ø вн.");
            g.Columns.Add("w",  "стенка");
            parent.Controls.Add(g);
            return g;
        }

        void FillWallGrid(string s)
        {
            if (_wallGrid == null) return;
            var rows = PipeWalls.Parse(s);
            if (rows.Count == 0) rows = PipeWalls.KorsisSn8();
            _wallGrid.Rows.Clear();
            foreach (var r in rows)
                _wallGrid.Rows.Add(r.OdMm.ToString("0.#"), r.IdMm.ToString("0.#"), r.WallMm.ToString("0.#"));
        }

        string ReadWallGrid()
        {
            var list = new System.Collections.Generic.List<PipeWallRow>();
            if (_wallGrid == null) return PipeWalls.Format(PipeWalls.KorsisSn8());
            foreach (DataGridViewRow row in _wallGrid.Rows)
            {
                if (row.IsNewRow) continue;
                double od, id, w;
                if (!TryCell(row, 0, out od) || !TryCell(row, 1, out id) || !TryCell(row, 2, out w))
                    continue;
                if (od <= 0 || w < 0) continue;
                list.Add(new PipeWallRow { OdMm = od, IdMm = id, WallMm = w });
            }
            return PipeWalls.Format(list);
        }

        static bool TryCell(DataGridViewRow row, int i, out double v)
        {
            v = 0;
            if (i >= row.Cells.Count || row.Cells[i].Value == null) return false;
            string s = row.Cells[i].Value.ToString().Trim().Replace(',', '.');
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        void ResetWalls()  { FillWallGrid(PipeWalls.Format(PipeWalls.KorsisSn8())); }
        void AddWallRow()  { if (_wallGrid != null) _wallGrid.Rows.Add("", "", ""); }

        sealed class SheetUi
        {
            public CheckBox Mh;
            public Button Hatch, HatchSand, HatchSoil, Cover, Top, Bot, Cs;
            public ComboBox CoverLt, TopLt, BotLt;
        }

        SheetUi MakeSheet(Control p, SheetStyle st, string capCover, string capTop, string capBot,
            bool hatch = true, bool sandSoil = false)
        {
            var ui = new SheetUi();
            ui.Mh = Chk(p, "Показывать колодцы", st != null && st.ShowManholes);
            ui.Cs = ColorRow(p, "Линии сечения", AciToRgb(129));
            if (st != null) PaintLine(ui.Cs, st.CsLine, AciToRgb(129));
            if (hatch && sandSoil)
            {
                ui.HatchSand = HatchRow(p, "Штриховка песка", FillSwatch(st != null ? st.HatchSand : null, Color.Blue));
                ui.HatchSoil = HatchRow(p, "Штриховка местного грунта", FillSwatch(st != null ? st.HatchSoil ?? st.Hatch : null, Color.Blue));
                if (st != null && st.HatchSand != null) PaintFill(ui.HatchSand, st.HatchSand);
                if (st != null && (st.HatchSoil != null || st.Hatch != null))
                    PaintFill(ui.HatchSoil, st.HatchSoil ?? st.Hatch);
            }
            else if (hatch)
            {
                ui.Hatch = HatchRow(p, "Штриховка", FillSwatch(st != null ? st.Hatch : null, Color.Blue));
                if (st != null && st.Hatch != null) PaintFill(ui.Hatch, st.Hatch);
            }
            ui.Cover = ColorRow(p, capCover, Color.Red);
            if (st != null) PaintLine(ui.Cover, st.Cover, Color.Red);
            ui.CoverLt = LtRow(p, "Тип линии " + capCover,
                st != null && st.Cover != null && !string.IsNullOrEmpty(st.Cover.Linetype) ? st.Cover.Linetype : "DASHED");
            ui.Top = ColorRow(p, capTop, Color.Blue);
            if (st != null) PaintLine(ui.Top, st.Top, Color.Blue);
            ui.TopLt = LtRow(p, "Тип линии " + capTop,
                st != null && st.Top != null && !string.IsNullOrEmpty(st.Top.Linetype) ? st.Top.Linetype : "Continuous");
            ui.Bot = ColorRow(p, capBot, Color.Blue);
            if (st != null) PaintLine(ui.Bot, st.Bot, Color.Blue);
            ui.BotLt = LtRow(p, "Тип линии " + capBot,
                st != null && st.Bot != null && !string.IsNullOrEmpty(st.Bot.Linetype) ? st.Bot.Linetype : "Continuous");
            return ui;
        }

        static Color FillSwatch(FillStyle st, Color fallback)
        {
            if (st == null) return fallback;
            if (st.R == 0 && st.G == 0 && st.B == 0 && st.Color > 0) return AciToRgb(st.Color);
            return Color.FromArgb(st.R, st.G, st.B);
        }

        static void PushSheet(SheetUi ui, SheetStyle st)
        {
            if (ui == null || st == null) return;
            ui.Mh.Checked = st.ShowManholes;
            if (ui.HatchSand != null) PaintFill(ui.HatchSand, st.HatchSand);
            if (ui.HatchSoil != null) PaintFill(ui.HatchSoil, st.HatchSoil ?? st.Hatch);
            if (ui.Hatch != null) PaintFill(ui.Hatch, st.Hatch);
            PaintLine(ui.Cs,    st.CsLine, AciToRgb(129));
            PaintLine(ui.Cover, st.Cover, Color.Red);
            PaintLine(ui.Top,   st.Top,   Color.Blue);
            PaintLine(ui.Bot,   st.Bot,   Color.Blue);
            SetLt(ui.CoverLt, st.Cover != null ? st.Cover.Linetype : "DASHED");
            SetLt(ui.TopLt,   st.Top   != null ? st.Top.Linetype   : "Continuous");
            SetLt(ui.BotLt,   st.Bot   != null ? st.Bot.Linetype   : "Continuous");
        }

        static void PullSheet(SheetUi ui, SheetStyle st, bool allowHatch = true)
        {
            if (ui == null || st == null) return;
            st.ShowManholes = ui.Mh.Checked;
            if (ui.HatchSand != null)
            {
                if (st.HatchSand == null) st.HatchSand = FillStyle.Of("AR-SAND", 5, 0.1);
                CopyFill(ui.HatchSand, st.HatchSand);
            }
            if (ui.HatchSoil != null)
            {
                if (st.HatchSoil == null) st.HatchSoil = FillStyle.Of("DOTS", 5, 0.2);
                CopyFill(ui.HatchSoil, st.HatchSoil);
                st.Hatch = st.HatchSoil;
            }
            else if (!allowHatch || ui.Hatch == null)
                st.Hatch = null;
            else
            {
                if (st.Hatch == null) st.Hatch = FillStyle.Of("ANSI31", 5, 1);
                CopyFill(ui.Hatch, st.Hatch);
            }
            if (st.Cover == null) st.Cover = LineStyle.Rgb(255,   0,   0, "DASHED",     18);
            if (st.Top   == null) st.Top   = LineStyle.Of(5, "Continuous", 30);
            if (st.Bot   == null) st.Bot   = LineStyle.Of(5, "Continuous", 30);
            if (st.CsLine == null) st.CsLine = LineStyle.Of(129, "Continuous", 30);
            CopyRgb(ui.Cover, st.Cover);
            CopyRgb(ui.Top,   st.Top);
            CopyRgb(ui.Bot,   st.Bot);
            CopyRgb(ui.Cs,    st.CsLine);
            st.Cover.Linetype = LtName(ui.CoverLt, "DASHED");
            st.Top.Linetype   = LtName(ui.TopLt,   "Continuous");
            st.Bot.Linetype   = LtName(ui.BotLt,   "Continuous");
        }

        sealed class SurfItem
        {
            public string Handle;
            public string Name;
            public override string ToString() { return Name ?? "(нет)"; }
        }

        // ── TabControl without white content-area border ─────────────────────
        sealed class FlatTabControl : TabControl
        {
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F) return; // WM_PAINT
                using (var g = Graphics.FromHwnd(Handle))
                {
                    if (TabCount == 0 || SelectedIndex < 0) return;
                    // cover the 1-2 px system border around the content page
                    var page = TabPages[SelectedIndex].Bounds;
                    var border = new Rectangle(page.X - 2, page.Y - 2, page.Width + 4, page.Height + 4);
                    using (var pen = new Pen(C_CARD, 4))
                        g.DrawRectangle(pen, border);
                    // cover outer control border
                    using (var pen = new Pen(C_BG, 2))
                        g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────

        // ── Toggle switch (replaces CheckBox visually) ────────────────────────
        sealed class ToggleSwitch : CheckBox
        {
            const int PW = 36, PH = 18;

            public ToggleSwitch()
            {
                SetStyle(ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.Selectable, true);
                AutoSize = false;
                Cursor   = Cursors.Hand;
            }

            protected override bool ShowFocusCues => false;

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // fill control background from parent
                var bg = Parent != null ? Parent.BackColor : C_CARD;
                using (var br = new SolidBrush(bg))
                    g.FillRectangle(br, ClientRectangle);

                // pill background
                int py = (Height - PH) / 2;
                var pillColor = Checked ? C_ACCENT : C_BTN;
                using (var br = new SolidBrush(pillColor))
                    FillRoundRect(g, br, 0, py, PW, PH, PH / 2);

                // knob
                int ks = PH - 4;
                int kx = Checked ? PW - ks - 2 : 2;
                using (var br = new SolidBrush(Color.White))
                    g.FillEllipse(br, kx, py + 2, ks, ks);

                // label text
                var tr = new Rectangle(PW + 8, 0, Width - PW - 8, Height);
                TextRenderer.DrawText(g, Text, Font, tr, ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            protected override void OnCheckedChanged(EventArgs e)
            {
                base.OnCheckedChanged(e);
                Invalidate();
            }

            static void FillRoundRect(Graphics g, Brush br, int x, int y, int w, int h, int r)
            {
                if (r < 1) { g.FillRectangle(br, x, y, w, h); return; }
                int d = r * 2;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(x,         y,         d, d, 180, 90);
                    path.AddArc(x + w - d, y,         d, d, 270, 90);
                    path.AddArc(x + w - d, y + h - d, d, d,   0, 90);
                    path.AddArc(x,         y + h - d, d, d,  90, 90);
                    path.CloseFigure();
                    g.FillPath(br, path);
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────
    }

    // ── Popup form for per-level table row configuration ──────────────────
    sealed class RowConfigForm : Form
    {
        static readonly Color C_BG     = Color.FromArgb(22, 22, 38);
        static readonly Color C_INPUT  = Color.FromArgb(14, 16, 30);
        static readonly Color C_TEXT   = Color.FromArgb(200, 202, 220);
        static readonly Color C_SEL    = Color.FromArgb(40, 52, 88);
        static readonly Color C_DIM    = Color.FromArgb(96, 100, 128);
        static readonly Color C_STRIP  = Color.FromArgb(16, 17, 28);
        static readonly Color C_BTN    = Color.FromArgb(38, 50, 82);
        static readonly Color C_BORDER = Color.FromArgb(50, 54, 86);
        static readonly Color C_PRIM   = Color.FromArgb(36, 82, 148);

        static readonly string[] RowTypeItems = { "Отметка", "Длина", "Уклон", "Диаметр", "Колодец" };
        static readonly string[] RowSrcItems  =
        {
            "Дно траншеи", "Верх песка", "Лоток трубы", "Верх трубы",
            "Верх защиты", "Засыпка / земля", "Покрытие (дизайн)", "Чёрная земля", "ΔH защиты"
        };

        readonly DataGridView _grid;
        readonly ProfileKind _kind;
        public System.Collections.Generic.List<TableRowDef> Result { get; private set; }

        public RowConfigForm(System.Collections.Generic.List<TableRowDef> initialDefs, ProfileKind kind)
        {
            _kind = kind;
            Text = "Строки таблицы — " + KindName(kind);
            ClientSize = new Size(820, 480);
            MinimumSize = new Size(600, 400);
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;

            _grid = MakeGrid();

            var btnUp  = MakeBtn("↑",         40);
            var btnDn  = MakeBtn("↓",         40);
            var btnAdd = MakeBtn("+ строку",   90);
            var btnDel = MakeBtn("×",          40);
            var btnRst = MakeBtn("Сброс",      70);
            var btnOk  = MakeBtn("OK",         80);
            var btnCcl = MakeBtn("Отмена",     80);
            btnOk.BackColor = C_PRIM;

            btnUp.Click  += (s, e) => MoveRow(-1);
            btnDn.Click  += (s, e) => MoveRow(+1);
            btnAdd.Click += (s, e) => AddRow();
            btnDel.Click += (s, e) => DelRow();
            btnRst.Click += (s, e) => LoadGrid(TableRowDef.Defaults(kind));
            btnOk.Click  += (s, e) => { Result = SaveGrid(); DialogResult = DialogResult.OK; Close(); };
            btnCcl.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 36, BackColor = C_STRIP,
                Padding = new Padding(4, 4, 4, 0)
            };
            topBar.Controls.AddRange(new Control[] { btnUp, btnDn, btnAdd, btnDel, btnRst });

            var botBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 40, BackColor = C_STRIP,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4)
            };
            botBar.Controls.AddRange(new Control[] { btnCcl, btnOk });

            _grid.Dock = DockStyle.Fill;
            Controls.Add(_grid);
            Controls.Add(topBar);
            Controls.Add(botBar);

            LoadGrid(initialDefs);
        }

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
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = C_INPUT, GridColor = C_BORDER, BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                DefaultCellStyle = cs, ColumnHeadersDefaultCellStyle = hs,
                MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            var colShow = new DataGridViewCheckBoxColumn { Name = "show", HeaderText = "✓", Width = 32, Resizable = DataGridViewTriState.False };
            colShow.DefaultCellStyle.SelectionBackColor = C_SEL;

            var colType = new DataGridViewComboBoxColumn
                { Name = "type", HeaderText = "Тип", Width = 90, Resizable = DataGridViewTriState.False, FlatStyle = FlatStyle.Flat };
            colType.Items.AddRange(RowTypeItems);
            colType.DefaultCellStyle.BackColor = C_INPUT;
            colType.DefaultCellStyle.ForeColor = C_TEXT;

            var colSrc = new DataGridViewComboBoxColumn
                { Name = "src", HeaderText = "Источник", Width = 150, Resizable = DataGridViewTriState.False, FlatStyle = FlatStyle.Flat };
            colSrc.Items.AddRange(RowSrcItems);
            colSrc.DefaultCellStyle.BackColor = C_INPUT;
            colSrc.DefaultCellStyle.ForeColor = C_TEXT;

            var colFact = new DataGridViewCheckBoxColumn { Name = "fact", HeaderText = "Ф", Width = 32, Resizable = DataGridViewTriState.False };
            colFact.DefaultCellStyle.SelectionBackColor = C_SEL;

            var colLabel = new DataGridViewTextBoxColumn
                { Name = "label", HeaderText = "Подпись", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };

            var colDec = new DataGridViewTextBoxColumn { Name = "dec", HeaderText = "Зн", Width = 40, Resizable = DataGridViewTriState.False };

            g.Columns.AddRange(new DataGridViewColumn[] { colShow, colType, colSrc, colFact, colLabel, colDec });
            return g;
        }

        Button MakeBtn(string text, int width)
        {
            return new Button
            {
                Text = text, Width = width, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = C_BTN, ForeColor = C_TEXT,
                Margin = new Padding(2, 2, 0, 0)
            };
        }

        void LoadGrid(System.Collections.Generic.List<TableRowDef> defs)
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
                string srcStr = d.Type == SheetRowKind.Height ? TableRowDef.SrcDisplayName(d.Src) : "";
                string decStr = d.RoundTo > 0 ? d.RoundTo.ToString() : "";
                _grid.Rows.Add(d.Show, typeStr, srcStr, d.IsFact, d.Label ?? "", decStr);
            }
        }

        System.Collections.Generic.List<TableRowDef> SaveGrid()
        {
            var list = new System.Collections.Generic.List<TableRowDef>();
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
                    d.Src = TableRowDef.SrcFromDisplayName(srcStr);
                }
                d.IsFact  = row.Cells["fact"].Value  as bool? ?? false;
                d.Label   = row.Cells["label"].Value as string ?? "";
                string decStr2 = row.Cells["dec"].Value as string ?? "";
                int dec;
                d.RoundTo = int.TryParse(decStr2, out dec) ? dec : 0;
                list.Add(d);
            }
            return list;
        }

        void MoveRow(int dir)
        {
            int i = _grid.CurrentRow != null ? _grid.CurrentRow.Index : -1;
            if (i < 0) return;
            int j = i + dir;
            if (j < 0 || j >= _grid.Rows.Count || _grid.Rows[j].IsNewRow) return;
            var vals = new object[_grid.Columns.Count];
            for (int c = 0; c < _grid.Columns.Count; c++) vals[c] = _grid.Rows[i].Cells[c].Value;
            for (int c = 0; c < _grid.Columns.Count; c++)
            {
                _grid.Rows[i].Cells[c].Value = _grid.Rows[j].Cells[c].Value;
                _grid.Rows[j].Cells[c].Value = vals[c];
            }
            _grid.CurrentCell = _grid.Rows[j].Cells[0];
        }

        void AddRow()
        {
            _grid.Rows.Add(true, "Отметка", "Дно траншеи", false, "", "2");
            _grid.CurrentCell = _grid.Rows[_grid.Rows.Count - 1].Cells[0];
        }

        void DelRow()
        {
            var row = _grid.CurrentRow;
            if (row == null || row.IsNewRow) return;
            int idx = row.Index;
            _grid.Rows.Remove(row);
            if (_grid.Rows.Count > 0)
                _grid.CurrentCell = _grid.Rows[Math.Min(idx, _grid.Rows.Count - 1)].Cells[0];
        }

        static string KindName(ProfileKind k)
        {
            switch (k)
            {
                case ProfileKind.Razrabotka: return "L1 Разработка";
                case ProfileKind.Pesok:      return "L2 Песок";
                case ProfileKind.Truba:      return "L3 Труба";
                case ProfileKind.Zashita:    return "L4 Защита";
                case ProfileKind.Zasypka:    return "L5 Засыпка";
                default:                     return k.ToString();
            }
        }
    }
}
