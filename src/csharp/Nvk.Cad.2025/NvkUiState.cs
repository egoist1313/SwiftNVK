using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Nvk.Core;

namespace Nvk.Cad
{
    /// <summary>Настройки панели. Lisp тянет через nvk-ui-get перед прогоном.</summary>
    static class NvkUiState
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static double Bedding = 0.10;
        public static double ThkZash = 0.30;
        public static double ThkAsph = 1.02;
        public static double ThkGazn = 0.15;
        public static double ThkTrot = 0.31;
        public static double ThkOther = 0.35;
        public static string FillAsph = "SAND";
        public static string FillTrot = "SAND";
        public static string FillGazn = "SOIL";
        public static string FillOther = "SOIL";
        public static bool UseBlackEarth;
        public static string BlackEarthHandle = "";
        public static string BlackEarthName = "";
        public static string DesignHandle = "";
        public static string DesignName = "";
        public static bool L1ShowMh = true;
        public static double TableOffsetM = 1.0;
        public static string L1Preset = "default";
        public static string LabelRoad = "";
        public static string LabelEarth = "";
        public static string LabelBottom = "";
        public static short L1RoadCol = 1;
        public static short L1EarthCol = 5;
        public static short L1BotCol = 1;
        public static string L1RoadLt = "Continuous";
        public static string L1EarthLt = "Continuous";
        public static string L1BotLt = "Continuous";
        public static string L1HatPat = "ANSI31";
        public static short L1HatCol = 8;
        public static string L1PavPat = "ANSI31";
        public static short L1PavCol = 8;
        public static string Joint = "SOCKET";
        public static double SlopeM = 0.0;
        public static double SlopeL = 0.0;
        public static double SlopeR = 0.0;
        public static double TrenchLeft = 0.0;
        public static double TrenchRight = 0.0;
        public static double HalfWidth = 3.0;
        public static double HScale = 10.0;
        public static double LScale = 1.0;
        public static double CsScale = 1.0;
        public static double CsYgap = 100.0;
        public static double CsTextH = 0.5;
        public static double TextH = 2.5;
        public static double TextOff = 10.0;
        public static int RoundTo = 2;
        public static int SlopeRound = 1;
        public static string SlopeUnit = "permille";
        public static bool ShowPermille = true;
        public static bool ShowLen = true;
        public static bool ShowSlope = true;
        public static double LabelColW = 0.0;
        public static bool BlockCs = true;
        public static bool BlockEp = false;
        public static bool ShowLineTags = true;
        public static string Stations = "AUTO";
        public static bool DoCs = true;
        public static bool DoEp = true;
        public static bool DoPoper = true;
        public static bool DoVol = true;
        public static bool DoPlan = true;
        public static bool DoSheets = true;
        public static double VpScaleEp = 500;
        public static double VpScaleCs = 200;
        public static bool ProjFact;
        public static bool SingleScatter = true;
        public static bool UseScatterRazr = true;
        public static bool UseScatterTruba;
        public static bool UseScatterZashita = true;
        public static bool UseScatterZasypka = true;
        public static double ScatterRazr = 0.05;
        public static double ScatterTruba = 0.005;
        public static double ScatterZashita = 0.05;
        public static double ScatterZasypka = 0.05;
        public static bool Annotative;
        public static double AnnGap = 10.0;
        public static bool PavAll;
        public static bool PickZones;
        public static string Zones = "";
        public static bool UsePipeWall = true;
        public static string Walls = "";
        public static bool PlanEn = true;
        public static double PlanInt = 50.0;
        public static double PlanMin = 15.0;
        public static bool DrawProjections = true;
        public static int ProjectionColor = 256;
        public static bool UseTextHeight = true;
        public static bool DeltaH = true;
        public static string LabelHeight = "";
        public static string LabelLength = "";
        public static string LabelSlope = "";
        public static string TblL2Label = "";
        public static string TblL3Label = "";
        public static string TblL4Label = "";
        public static string TblL5Label = "";
        // per-level строки таблицы (сериализованный List<TableRowDef>; пусто = умолчание)
        public static string RowsL1 = "";
        public static string RowsL2 = "";
        public static string RowsL3 = "";
        public static string RowsL4 = "";
        public static string RowsL5 = "";
        // handle Civil 3D Profile-объекта для строк «факт» каждого уровня (пусто = без реального профиля)
        public static string FactProfileHandleL1 = "";
        public static string FactProfileHandleL2 = "";
        public static string FactProfileHandleL3 = "";
        public static string FactProfileHandleL4 = "";
        public static string FactProfileHandleL5 = "";
        public static double? OshOverride = null;
        public static bool HasPush;
        public static string LoadedDocKey;

        public static void ApplyTo(SectionConfig cfg, TableCfg tbl)
        {
            if (cfg == null) return;
            cfg.ThkAsph = ThkAsph;
            cfg.ThkGazn = ThkGazn;
            cfg.ThkTrot = ThkTrot;
            cfg.ThkOther = ThkOther;
            cfg.FillAsph = FillAsph ?? "SAND";
            cfg.FillTrot = FillTrot ?? "SAND";
            cfg.FillGazn = FillGazn ?? "SOIL";
            cfg.FillOther = FillOther ?? "SOIL";
            cfg.UseBlackEarth = UseBlackEarth;
            cfg.PavAll = PavAll;
            cfg.ProjFact = ProjFact;
            cfg.SingleScatter = SingleScatter;
            cfg.UseScatterRazr = UseScatterRazr;
            cfg.UseScatterTruba = UseScatterTruba;
            cfg.UseScatterZashita = UseScatterZashita;
            cfg.UseScatterZasypka = UseScatterZasypka;
            cfg.ScatterRazr = ScatterRazr;
            cfg.ScatterTruba = ScatterTruba;
            cfg.ScatterZashita = ScatterZashita;
            cfg.ScatterZasypka = ScatterZasypka;
            cfg.Annotative = Annotative;
            cfg.AnnGap = AnnGap;
            cfg.CsYgap = CsYgap;
            cfg.PlanEn = PlanEn;
            cfg.PlanInt = PlanInt;
            cfg.PlanMin = PlanMin;
            cfg.DrawProjections = DrawProjections;
            cfg.ProjectionColor = (short)ProjectionColor;
            cfg.UseTextHeight = true;
            cfg.DeltaH = true;
            cfg.LabelHeight = LabelHeight;
            cfg.LabelLength = LabelLength;
            cfg.LabelSlope = LabelSlope;
            cfg.SlopeL = SlopeL < 0.0 ? 0.0 : SlopeL;
            cfg.SlopeR = SlopeR < 0.0 ? 0.0 : SlopeR;
            cfg.TrenchLeft = TrenchLeft < 0.0 ? 0.0 : TrenchLeft;
            cfg.TrenchRight = TrenchRight < 0.0 ? 0.0 : TrenchRight;
            cfg.Zones = RoadZones.Parse(Zones, 0.0);
            cfg.UsePipeWall = UsePipeWall;
            cfg.Walls = PipeWalls.Parse(string.IsNullOrWhiteSpace(Walls)
                ? PipeWalls.Format(PipeWalls.KorsisSn8()) : Walls);
            if (tbl != null)
            {
                tbl.Annotative = Annotative;
                tbl.AnnGap = AnnGap;
                tbl.DrawProjections = DrawProjections;
                tbl.ProjectionColor = (short)ProjectionColor;
                tbl.RoundTo = 2;
                tbl.SlopeRoundTo = 1;
                tbl.ShowLengths = true;
                tbl.ShowSlopes = true;
                tbl.UseTextHeight = true;
                tbl.DeltaH = true;
                tbl.LabelHeight = LabelHeight;
                tbl.LabelLength = LabelLength;
                tbl.LabelSlope = LabelSlope;
            }
            SyncL1();
        }

        public static void SyncL1()
        {
            var s = NvkSession.L1 ?? L1Style.Default();
            s.Name = string.IsNullOrWhiteSpace(L1Preset) ? "default" : L1Preset;
            s.ThkAsph = ThkAsph;
            s.ThkTrot = ThkTrot;
            s.ThkGazn = ThkGazn;
            s.ThkOther = ThkOther;
            s.Zones = Zones ?? "";
            s.UseBlackEarth = UseBlackEarth;
            s.BlackEarthHandle = BlackEarthHandle ?? "";
            s.BlackEarthName = BlackEarthName ?? "";
            s.DesignHandle = DesignHandle ?? "";
            s.DesignName = DesignName ?? "";
            double scL1 = RowScatter.Amp(ProfileKind.Razrabotka, HeightSrc.TrenchBottom);
            s.Scatter = scL1 > 1e-9;
            s.ScatterAmp = scL1 > 1e-9 ? scL1 : ScatterRazr;
            s.ShowManholes = L1ShowMh;
            s.TableOffsetM = TableOffsetM;
            if (!string.IsNullOrEmpty(LabelRoad)) s.LabelRoad = LabelRoad;
            s.LabelEarth = ProfileRows.MigrateEarthLabel(LabelEarth);
            if (!string.IsNullOrEmpty(LabelBottom)) s.LabelBottom = LabelBottom;
            if (s.Road == null) s.Road = LineStyle.Rgb(255, 0, 0, "DASHED", 30);
            if (s.Earth == null) s.Earth = LineStyle.Rgb(0, 0, 255, "Continuous", 30);
            if (s.Bottom == null) s.Bottom = LineStyle.Rgb(255, 0, 0, "Continuous", 30);
            if (!string.IsNullOrEmpty(L1RoadLt)) s.Road.Linetype = L1RoadLt;
            if (!string.IsNullOrEmpty(L1EarthLt)) s.Earth.Linetype = L1EarthLt;
            if (!string.IsNullOrEmpty(L1BotLt)) s.Bottom.Linetype = L1BotLt;
            if (s.HatchExcavation == null) s.HatchExcavation = FillStyle.Rgb("ANSI31", 160, 160, 160, 1);
            if (s.HatchPavement == null) s.HatchPavement = FillStyle.Rgb("ANSI31", 200, 80, 80, 1);
            if (!string.IsNullOrEmpty(L1HatPat)) s.HatchExcavation.Pattern = L1HatPat;
            if (!string.IsNullOrEmpty(L1PavPat)) s.HatchPavement.Pattern = L1PavPat;
            NvkSession.L1 = s;
        }

        /// <summary>Зеркало строк таблицы в старые NOD-флаги USE_SC_* / SC_* — для DCL и чертежей до 0.116.</summary>
        public static void SyncScatterFromRows()
        {
            double a1 = RowScatter.Amp(ProfileKind.Razrabotka, HeightSrc.TrenchBottom);
            double a2s = RowScatter.Amp(ProfileKind.Pesok, HeightSrc.SandTop);
            double a2b = RowScatter.Amp(ProfileKind.Pesok, HeightSrc.TrenchBottom);
            UseScatterRazr = a1 > 1e-9 || a2s > 1e-9 || a2b > 1e-9;
            if (a1 > 1e-9) ScatterRazr = a1;
            else if (a2s > 1e-9) ScatterRazr = a2s;
            else if (a2b > 1e-9) ScatterRazr = a2b;

            double a3 = RowScatter.Amp(ProfileKind.Truba, HeightSrc.Invert);
            UseScatterTruba = a3 > 1e-9;
            if (a3 > 1e-9) ScatterTruba = a3;

            double a4 = RowScatter.Amp(ProfileKind.Zashita, HeightSrc.ZashTop);
            UseScatterZashita = a4 > 1e-9;
            if (a4 > 1e-9) ScatterZashita = a4;

            double a5 = RowScatter.Amp(ProfileKind.Zasypka, HeightSrc.Surface);
            UseScatterZasypka = a5 > 1e-9;
            if (a5 > 1e-9) ScatterZasypka = a5;
        }

        public static void FromL1(L1Style s)
        {
            if (s == null) return;
            L1Preset = s.Name ?? "default";
            ThkAsph = s.ThkAsph;
            ThkTrot = s.ThkTrot;
            ThkGazn = s.ThkGazn;
            ThkOther = s.ThkOther;
            Zones = s.Zones ?? "";
            UseBlackEarth = s.UseBlackEarth;
            BlackEarthHandle = s.BlackEarthHandle ?? "";
            BlackEarthName = s.BlackEarthName ?? "";
            DesignHandle = s.DesignHandle ?? "";
            DesignName = s.DesignName ?? "";
            UseScatterRazr = s.Scatter;
            ScatterRazr = s.ScatterAmp;
            L1ShowMh = s.ShowManholes;
            TableOffsetM = s.TableOffsetM;
            LabelRoad = s.LabelRoad ?? "";
            LabelEarth = ProfileRows.MigrateEarthLabel(s.LabelEarth);
            LabelBottom = s.LabelBottom ?? "";
            if (s.Road != null) { L1RoadCol = s.Road.Color; L1RoadLt = s.Road.Linetype; }
            if (s.Earth != null) { L1EarthCol = s.Earth.Color; L1EarthLt = s.Earth.Linetype; }
            if (s.Bottom != null) { L1BotCol = s.Bottom.Color; L1BotLt = s.Bottom.Linetype; }
            if (s.HatchExcavation != null) { L1HatPat = s.HatchExcavation.Pattern; L1HatCol = s.HatchExcavation.Color; }
            if (s.HatchPavement != null) { L1PavPat = s.HatchPavement.Pattern; L1PavCol = s.HatchPavement.Color; }
            NvkSession.L1 = s;
        }

        public static Dictionary<string, string> Serialize()
        {
            var rb = ToRb();
            var arr = rb.AsArray();
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < arr.Length; i += 2)
            {
                string k = arr[i].Value as string;
                string v = arr[i + 1].Value as string;
                if (k != null && v != null) d[k] = v;
            }
            return d;
        }

        public static ResultBuffer ToRb()
        {
            var rb = new ResultBuffer();
            Pair(rb, "BEDDING", F(Bedding, 4));
            Pair(rb, "THK_ZASH", F(ThkZash, 4));
            Pair(rb, "THK_ASPH", F(ThkAsph, 4));
            Pair(rb, "THK_GAZN", F(ThkGazn, 4));
            Pair(rb, "THK_TROT", F(ThkTrot, 4));
            Pair(rb, "THK_OTHER", F(ThkOther, 4));
            Pair(rb, "FILL_ASPH", FillAsph ?? "SAND");
            Pair(rb, "FILL_TROT", FillTrot ?? "SAND");
            Pair(rb, "FILL_GAZN", FillGazn ?? "SOIL");
            Pair(rb, "FILL_OTHER", FillOther ?? "SOIL");
            Pair(rb, "USE_BE", UseBlackEarth ? "1" : "0");
            Pair(rb, "BE_H", BlackEarthHandle ?? "");
            Pair(rb, "BE_N", BlackEarthName ?? "");
            Pair(rb, "DESIGN_H", DesignHandle ?? "");
            Pair(rb, "DESIGN_N", DesignName ?? "");
            Pair(rb, "L1_MH", L1ShowMh ? "1" : "0");
            Pair(rb, "TBL_OFF", F(TableOffsetM, 2));
            Pair(rb, "L1_PRESET", L1Preset ?? "default");
            Pair(rb, "LAB_ROAD", LabelRoad ?? "");
            Pair(rb, "LAB_EARTH", LabelEarth ?? "");
            Pair(rb, "LAB_BOT", LabelBottom ?? "");
            Pair(rb, "L1_RC", L1RoadCol.ToString(Inv));
            Pair(rb, "L1_EC", L1EarthCol.ToString(Inv));
            Pair(rb, "L1_BC", L1BotCol.ToString(Inv));
            Pair(rb, "L1_RL", L1RoadLt ?? "Continuous");
            Pair(rb, "L1_EL", L1EarthLt ?? "Continuous");
            Pair(rb, "L1_BL", L1BotLt ?? "Continuous");
            Pair(rb, "L1_HP", L1HatPat ?? "ANSI31");
            Pair(rb, "L1_HC", L1HatCol.ToString(Inv));
            Pair(rb, "L1_PP", L1PavPat ?? "ANSI31");
            Pair(rb, "L1_PC", L1PavCol.ToString(Inv));
            Pair(rb, "CS_YGAP", F(CsYgap, 1));
            Pair(rb, "JOINT", Joint ?? "SOCKET");
            Pair(rb, "SLOPE", F(SlopeM, 4));
            Pair(rb, "SLOPE_L", F(SlopeL, 4));
            Pair(rb, "SLOPE_R", F(SlopeR, 4));
            Pair(rb, "TRENCH_L", F(TrenchLeft, 3));
            Pair(rb, "TRENCH_R", F(TrenchRight, 3));
            Pair(rb, "HWIDTH", F(HalfWidth, 3));
            Pair(rb, "H_SCALE", F(HScale, 2));
            Pair(rb, "L_SCALE", F(LScale, 2));
            Pair(rb, "CS_SCALE", F(CsScale, 2));
            Pair(rb, "CS_TH", F(CsTextH, 3));
            Pair(rb, "TEXT_H", F(TextH, 3));
            Pair(rb, "TEXT_OFF", F(TextOff, 1));
            Pair(rb, "ROUND_TO", RoundTo.ToString(Inv));
            Pair(rb, "SLOPE_RND", SlopeRound.ToString(Inv));
            Pair(rb, "SLOPE_U", SlopeUnit ?? "permille");
            Pair(rb, "SHOW_PERM", ShowPermille ? "1" : "0");
            Pair(rb, "SHOW_LEN", ShowLen ? "1" : "0");
            Pair(rb, "SHOW_SLP", ShowSlope ? "1" : "0");
            Pair(rb, "LAB_W", F(LabelColW, 2));
            Pair(rb, "BLOCK_CS", BlockCs ? "1" : "0");
            Pair(rb, "BLOCK_EP", BlockEp ? "1" : "0");
            Pair(rb, "SHOW_TAGS", ShowLineTags ? "1" : "0");
            Pair(rb, "STATIONS", string.IsNullOrEmpty(Stations) ? "AUTO" : Stations);
            Pair(rb, "DO_CS", DoCs ? "1" : "0");
            Pair(rb, "DO_EP", DoEp ? "1" : "0");
            Pair(rb, "DO_POPER", DoPoper ? "1" : "0");
            Pair(rb, "DO_VOL", DoVol ? "1" : "0");
            Pair(rb, "DO_PLAN", DoPlan ? "1" : "0");
            Pair(rb, "DO_SHEETS", DoSheets ? "1" : "0");
            Pair(rb, "VP_SC_EP", F(VpScaleEp, 0));
            Pair(rb, "VP_SC_CS", F(VpScaleCs, 0));
            Pair(rb, "PROJ_FACT", ProjFact ? "1" : "0");
            Pair(rb, "SINGLE_SC", SingleScatter ? "1" : "0");
            Pair(rb, "USE_SC_R", UseScatterRazr ? "1" : "0");
            Pair(rb, "USE_SC_T", UseScatterTruba ? "1" : "0");
            Pair(rb, "USE_SC_Z", UseScatterZashita ? "1" : "0");
            Pair(rb, "USE_SC_S", UseScatterZasypka ? "1" : "0");
            Pair(rb, "SC_R", F(ScatterRazr, 4));
            Pair(rb, "SC_T", F(ScatterTruba, 4));
            Pair(rb, "SC_Z", F(ScatterZashita, 4));
            Pair(rb, "SC_S", F(ScatterZasypka, 4));
            Pair(rb, "ANNOT", Annotative ? "1" : "0");
            Pair(rb, "ANN_GAP", F(AnnGap, 1));
            Pair(rb, "PAV_ALL", PavAll ? "1" : "0");
            Pair(rb, "PICK_ZONES", PickZones ? "1" : "0");
            Pair(rb, "ZONES", Zones ?? "");
            Pair(rb, "USE_WALL", UsePipeWall ? "1" : "0");
            Pair(rb, "WALLS", string.IsNullOrEmpty(Walls) ? PipeWalls.Format(PipeWalls.KorsisSn8()) : Walls);
            Pair(rb, "PLAN_EN", PlanEn ? "1" : "0");
            Pair(rb, "PLAN_INT", F(PlanInt, 1));
            Pair(rb, "PLAN_MIN", F(PlanMin, 1));
            Pair(rb, "DRAW_PROJ", DrawProjections ? "1" : "0");
            Pair(rb, "PROJ_COL", ProjectionColor.ToString(Inv));
            Pair(rb, "USE_TH", UseTextHeight ? "1" : "0");
            Pair(rb, "DELTA_H", DeltaH ? "1" : "0");
            Pair(rb, "LAB_H", LabelHeight ?? "");
            Pair(rb, "LAB_L", LabelLength ?? "");
            Pair(rb, "LAB_S", LabelSlope ?? "");
            Pair(rb, "TBL_L2_LAB",   TblL2Label ?? "");
            Pair(rb, "TBL_L3_LAB",   TblL3Label ?? "");
            Pair(rb, "TBL_L4_LAB",   TblL4Label ?? "");
            Pair(rb, "TBL_L5_LAB",   TblL5Label ?? "");
            Pair(rb, "TBL_ROWS_L1",  RowsL1 ?? "");
            Pair(rb, "TBL_ROWS_L2",  RowsL2 ?? "");
            Pair(rb, "TBL_ROWS_L3",  RowsL3 ?? "");
            Pair(rb, "TBL_ROWS_L4",  RowsL4 ?? "");
            Pair(rb, "TBL_ROWS_L5",  RowsL5 ?? "");
            Pair(rb, "FACT_PROF_L1", FactProfileHandleL1 ?? "");
            Pair(rb, "FACT_PROF_L2", FactProfileHandleL2 ?? "");
            Pair(rb, "FACT_PROF_L3", FactProfileHandleL3 ?? "");
            Pair(rb, "FACT_PROF_L4", FactProfileHandleL4 ?? "");
            Pair(rb, "FACT_PROF_L5", FactProfileHandleL5 ?? "");
            return rb;
        }

        public static void FromRb(ResultBuffer args)
        {
            if (args == null) return;
            var arr = args.AsArray();
            for (int i = 0; i + 1 < arr.Length; i += 2)
            {
                string k = arr[i].Value as string;
                string v = arr[i + 1].Value as string;
                if (k == null || v == null) continue;
                Apply(k, v);
            }
            HasPush = true;
        }

        public static void MarkCurrent(string docKey)
        {
            LoadedDocKey = docKey ?? "";
        }

        public static bool IsCurrent(string docKey)
        {
            return LoadedDocKey != null
                && docKey != null
                && string.Equals(LoadedDocKey, docKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Подтянуть STATE из NOD чертежа. Возвращает true, если реально читали
        /// (другой документ или force) — тогда панель надо LoadFromState, не SaveToState.
        /// </summary>
        /// <summary>
        /// Вернуть true только если NOD содержал данные (не пустой).
        /// false = NOD пуст или пропущено (IsCurrent) → вызывающий может загрузить профиль.
        /// </summary>
        public static bool LoadFromDatabase(Database db, string docKey, bool force)
        {
            if (db == null) return false;
            if (!force && IsCurrent(docKey)) return false;
            var map = NvkNod.Read(db);
            LoadedDocKey = docKey ?? "";
            FromMap(map);
            NvkNod.ApplyLayout(db, map);
            return map != null && map.Count > 0;
        }

        public static void FromMap(Dictionary<string, string> map)
        {
            Zones = "";
            PavAll = false;
            if (map == null || map.Count == 0) return;
            bool hasSlopeL = false;
            bool hasSlopeR = false;
            foreach (var kv in map)
            {
                string k = kv.Key != null ? kv.Key.ToUpperInvariant() : "";
                if (k == "SLOPE_L") hasSlopeL = true;
                if (k == "SLOPE_R") hasSlopeR = true;
                Apply(kv.Key, kv.Value ?? "");
            }
            if (!hasSlopeL) SlopeL = SlopeM;
            if (!hasSlopeR) SlopeR = SlopeM;
            HasPush = true;
        }

        static void Apply(string k, string v)
        {
            switch (k.ToUpperInvariant())
            {
                case "BEDDING": Bedding = D(v, Bedding); break;
                case "THK_ZASH": ThkZash = D(v, ThkZash); break;
                case "THK_ASPH": ThkAsph = D(v, ThkAsph); break;
                case "THK_GAZN": ThkGazn = D(v, ThkGazn); break;
                case "THK_TROT": ThkTrot = D(v, ThkTrot); break;
                case "THK_OTHER": ThkOther = D(v, ThkOther); break;
                case "FILL_ASPH": FillAsph = string.IsNullOrEmpty(v) ? "SAND" : v; break;
                case "FILL_TROT": FillTrot = string.IsNullOrEmpty(v) ? "SAND" : v; break;
                case "FILL_GAZN": FillGazn = string.IsNullOrEmpty(v) ? "SOIL" : v; break;
                case "FILL_OTHER": FillOther = string.IsNullOrEmpty(v) ? "SOIL" : v; break;
                case "USE_BE": UseBlackEarth = v != "0"; break;
                case "BE_H": BlackEarthHandle = v; break;
                case "BE_N": BlackEarthName = v; break;
                case "DESIGN_H":
                case "SURF_H": DesignHandle = v; break;
                case "DESIGN_N":
                case "SURF_N": DesignName = v; break;
                case "L1_MH": L1ShowMh = v != "0"; break;
                case "TBL_OFF": TableOffsetM = D(v, TableOffsetM); break;
                case "L1_PRESET": L1Preset = v; break;
                case "LAB_ROAD": LabelRoad = v; break;
                case "LAB_EARTH": LabelEarth = ProfileRows.MigrateEarthLabel(v); break;
                case "LAB_BOT": LabelBottom = v; break;
                case "L1_RC": L1RoadCol = (short)D(v, L1RoadCol); break;
                case "L1_EC": L1EarthCol = (short)D(v, L1EarthCol); break;
                case "L1_BC": L1BotCol = (short)D(v, L1BotCol); break;
                case "L1_RL": L1RoadLt = v; break;
                case "L1_EL": L1EarthLt = v; break;
                case "L1_BL": L1BotLt = v; break;
                case "L1_HP": L1HatPat = v; break;
                case "L1_HC": L1HatCol = (short)D(v, L1HatCol); break;
                case "L1_PP": L1PavPat = v; break;
                case "L1_PC": L1PavCol = (short)D(v, L1PavCol); break;
                case "CS_YGAP": CsYgap = D(v, CsYgap); break;
                case "JOINT": if (v.Length > 0) Joint = v; break;
                case "SLOPE":
                case "CS_SLOPE": SlopeM = D(v, SlopeM); break;
                case "SLOPE_L": SlopeL = D(v, SlopeL); break;
                case "SLOPE_R": SlopeR = D(v, SlopeR); break;
                case "TRENCH_L": TrenchLeft = D(v, TrenchLeft); break;
                case "TRENCH_R": TrenchRight = D(v, TrenchRight); break;
                case "HWIDTH": HalfWidth = D(v, HalfWidth); break;
                case "H_SCALE": HScale = D(v, HScale); break;
                case "L_SCALE": LScale = D(v, LScale); break;
                case "CS_SCALE": CsScale = D(v, CsScale); break;
                case "CS_TH": CsTextH = D(v, CsTextH); break;
                case "TEXT_H": TextH = D(v, TextH); break;
                case "TEXT_OFF": TextOff = D(v, TextOff); break;
                case "ROUND_TO": RoundTo = (int)D(v, RoundTo); break;
                case "SLOPE_RND": SlopeRound = (int)D(v, SlopeRound); break;
                case "SLOPE_U": if (v.Length > 0) SlopeUnit = v; break;
                case "SHOW_PERM": ShowPermille = v != "0"; break;
                case "SHOW_LEN": ShowLen = v != "0"; break;
                case "SHOW_SLP": ShowSlope = v != "0"; break;
                case "LAB_W": LabelColW = D(v, LabelColW); break;
                case "BLOCK_CS": BlockCs = v != "0"; break;
                case "BLOCK_EP": BlockEp = v != "0"; break;
                case "SHOW_TAGS": ShowLineTags = v != "0"; break;
                case "STATIONS": Stations = v; break;
                case "DO_CS": DoCs = v != "0"; break;
                case "DO_EP": DoEp = v != "0"; break;
                case "DO_POPER": DoPoper = v != "0"; break;
                case "DO_VOL": DoVol = v != "0"; break;
                case "DO_PLAN": DoPlan = v != "0"; break;
                case "DO_SHEETS": DoSheets = v != "0"; break;
                case "VP_SC_EP": VpScaleEp = D(v, VpScaleEp); if (VpScaleEp < 1) VpScaleEp = 500; break;
                case "VP_SC_CS": VpScaleCs = D(v, VpScaleCs); if (VpScaleCs < 1) VpScaleCs = 200; break;
                case "PROJ_FACT": ProjFact = v != "0"; break;
                case "SINGLE_SC": SingleScatter = v != "0"; break;
                case "USE_SC_R": UseScatterRazr = v != "0"; break;
                case "USE_SC_T": UseScatterTruba = v != "0"; break;
                case "USE_SC_Z": UseScatterZashita = v != "0"; break;
                case "USE_SC_S": UseScatterZasypka = v != "0"; break;
                case "SC_R": ScatterRazr = D(v, ScatterRazr); break;
                case "SC_T": ScatterTruba = D(v, ScatterTruba); break;
                case "SC_Z": ScatterZashita = D(v, ScatterZashita); break;
                case "SC_S": ScatterZasypka = D(v, ScatterZasypka); break;
                case "ANNOT": Annotative = v != "0"; break;
                case "ANN_GAP": AnnGap = D(v, AnnGap); break;
                case "PAV_ALL": PavAll = v != "0"; break;
                case "PICK_ZONES": PickZones = v != "0"; break;
                case "ZONES":
                    if (string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(Zones)) break;
                    Zones = v;
                    break;
                case "USE_WALL": UsePipeWall = v != "0"; break;
                case "WALLS":
                    if (!string.IsNullOrEmpty(v)) Walls = v;
                    break;
                case "PLAN_EN": PlanEn = v != "0"; break;
                case "PLAN_INT": PlanInt = D(v, PlanInt); break;
                case "PLAN_MIN": PlanMin = D(v, PlanMin); break;
                case "DRAW_PROJ": DrawProjections = v != "0"; break;
                case "PROJ_COL": ProjectionColor = (int)D(v, ProjectionColor); break;
                case "USE_TH": UseTextHeight = v != "0"; break;
                case "DELTA_H": DeltaH = v != "0"; break;
                case "LAB_H": LabelHeight = v; break;
                case "LAB_L": LabelLength = v; break;
                case "LAB_S": LabelSlope = v; break;
                case "TBL_L2_LAB":  TblL2Label = v; break;
                case "TBL_L3_LAB":  TblL3Label = v; break;
                case "TBL_L4_LAB":  TblL4Label = v; break;
                case "TBL_L5_LAB":  TblL5Label = v; break;
                case "TBL_ROWS_L1": RowsL1 = v; break;
                case "TBL_ROWS_L2": RowsL2 = v; break;
                case "TBL_ROWS_L3": RowsL3 = v; break;
                case "TBL_ROWS_L4": RowsL4 = v; break;
                case "TBL_ROWS_L5": RowsL5 = v; break;
                case "FACT_PROF_L1": FactProfileHandleL1 = v; break;
                case "FACT_PROF_L2": FactProfileHandleL2 = v; break;
                case "FACT_PROF_L3": FactProfileHandleL3 = v; break;
                case "FACT_PROF_L4": FactProfileHandleL4 = v; break;
                case "FACT_PROF_L5": FactProfileHandleL5 = v; break;
            }
        }

        static void Pair(ResultBuffer rb, string k, string v)
        {
            rb.Add(new TypedValue((int)LispDataType.Text, k));
            rb.Add(new TypedValue((int)LispDataType.Text, v));
        }

        static string F(double x, int p)
        {
            return x.ToString("0." + new string('0', p), Inv);
        }

        static double D(string s, double def)
        {
            double x;
            if (double.TryParse(s, NumberStyles.Float, Inv, out x)) return x;
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out x)) return x;
            return def;
        }
    }
}
