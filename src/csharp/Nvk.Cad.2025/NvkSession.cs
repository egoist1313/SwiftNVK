using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;

namespace Nvk.Cad
{
    sealed class DomainResult
    {
        public Chain Chain;
        public List<SectionGeom> Geoms;
        public SectionConfig Cfg;
        public string SurfName;
        public string Dump;
        public string SegId;
        public string NetName;
        public List<AxisPt> Axis;
        public List<double> ScatterVals;
        public List<double> ScatterL2;
        public List<double> ScatterL3;
        public List<double> ScatterL4;
        public List<double> ScatterL5;
        public List<double> LatOffs;
        public List<double> PlanTargets;
        public List<double> PlanLatOffs;

        public List<double> Vals(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Pesok:    return ScatterL2 ?? ScatterVals;
                case ProfileKind.Truba:    return ScatterL3 ?? ScatterVals;
                case ProfileKind.Zashita:  return ScatterL4 ?? ScatterVals;
                case ProfileKind.Zasypka:  return ScatterL5 ?? ScatterVals;
                default:                   return ScatterVals;
            }
        }
    }

    sealed class EpLayout
    {
        public Point3d Origin;
        public double LScale;
        public double HScale;
        public double Osh;
        public double RowGap;
        public List<ProfileKind> Kinds = new List<ProfileKind>();
    }

    static class NvkSession
    {
        public const string Version = "0.198";
        public static int RunId;
        public static DomainResult Last;
        public static List<DomainResult> Runs = new List<DomainResult>();
        public static List<ObjectId> LastPipes = new List<ObjectId>();
        public static List<ObjectId> LastCsInserts = new List<ObjectId>();
        public static EpLayout LastEp;
        public static TableCfg Table;
        public static double Cannoscale = 1000.0;
        public static double CsYgap = 100.0;
        public static string RootDir;
        public static Point3d CsIns;
        public static bool CsInsHas;
        public static Point3d EpIns;
        public static bool EpInsHas;
        public static Point3d VolIns;
        public static bool VolInsHas;
        public static L1Style L1 = L1Style.Default();
        public static SheetStyle L2 = SheetStyle.Pesok();
        public static SheetStyle L3 = SheetStyle.Truba();
        public static SheetStyle L4 = SheetStyle.Zashita();
        public static SheetStyle L5 = SheetStyle.Zasypka();

        public static SheetStyle Sheet(ProfileKind kind)
        {
            switch (kind)
            {
                case ProfileKind.Pesok: return L2;
                case ProfileKind.Truba: return L3;
                case ProfileKind.Zashita: return L4;
                case ProfileKind.Zasypka: return L5;
                default: return L2;
            }
        }
        public static List<double> CacheScatter;
        public static List<double> CacheScatter2;
        public static List<double> CacheScatter3;
        public static List<double> CacheScatter4;
        public static List<double> CacheScatter5;
        public static List<double> CacheLatOff;
        public static List<double> CachePlanTgt;
        public static List<double> CachePlanLat;
        public static string CachePlanLdr = "";
        public static bool FreshRandom;
        /// <summary>0 = все уровни, 1..5 = только этот ProfileKind.</summary>
        public static int FreshRandomKind;
        public static readonly Point3d[] EpLvl = new Point3d[5];
        public static readonly bool[] EpHas = new bool[5];
        public static readonly Point3d[] CsLvl = new Point3d[5];
        public static readonly bool[] CsHas = new bool[5];
        public static Dictionary<ProfileKind, SheetWin> EpWins = new Dictionary<ProfileKind, SheetWin>();
        public static Dictionary<ProfileKind, List<CsCell>> CsCells = new Dictionary<ProfileKind, List<CsCell>>();
        public static Dictionary<ProfileKind, SheetWin> VolWins = new Dictionary<ProfileKind, SheetWin>();
        public static SheetWin PlanWin = new SheetWin();
        public static SheetWin CoordWin = new SheetWin();
        public static SheetWin SampWin = new SheetWin();

        /// <summary>
        /// Сброс кэшей, привязанных к чертежу: трубы, разброс, вставки, окна листов.
        /// Не трогает стили L1…L5 и Table.
        /// </summary>
        public static void ClearDrawingCaches()
        {
            Last = null;
            Runs = new List<DomainResult>();
            LastPipes = new List<ObjectId>();
            LastCsInserts = new List<ObjectId>();
            LastEp = null;
            CsInsHas = EpInsHas = VolInsHas = false;
            CsIns = EpIns = VolIns = Point3d.Origin;
            if (EpHas != null)
            {
                for (int i = 0; i < EpHas.Length; i++) EpHas[i] = false;
            }
            if (CsHas != null)
            {
                for (int i = 0; i < CsHas.Length; i++) CsHas[i] = false;
            }
            CacheScatter = CacheScatter2 = CacheScatter3 = CacheScatter4 = CacheScatter5 = null;
            CacheLatOff = CachePlanTgt = CachePlanLat = null;
            CachePlanLdr = "";
            ClearSheetWins();
        }

        public static void ClearSheetWins()
        {
            if (EpWins == null) EpWins = new Dictionary<ProfileKind, SheetWin>();
            else EpWins.Clear();
            if (CsCells == null) CsCells = new Dictionary<ProfileKind, List<CsCell>>();
            else CsCells.Clear();
            if (VolWins == null) VolWins = new Dictionary<ProfileKind, SheetWin>();
            else VolWins.Clear();
            PlanWin = new SheetWin();
            CoordWin = new SheetWin();
            SampWin = new SheetWin();
        }

        public static void AddEpWin(ProfileKind kind, double x0, double y0, double x1, double y1)
        {
            if (EpWins == null) EpWins = new Dictionary<ProfileKind, SheetWin>();
            SheetWin w;
            if (!EpWins.TryGetValue(kind, out w) || w == null)
            {
                w = new SheetWin();
                EpWins[kind] = w;
            }
            w.Union(x0, y0, x1, y1);
        }

        public static void AddCsCell(ProfileKind kind, int index, double x0, double y0, double x1, double y1)
        {
            if (CsCells == null) CsCells = new Dictionary<ProfileKind, List<CsCell>>();
            List<CsCell> list;
            if (!CsCells.TryGetValue(kind, out list) || list == null)
            {
                list = new List<CsCell>();
                CsCells[kind] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Index == index)
                {
                    list[i].Win.Union(x0, y0, x1, y1);
                    return;
                }
            }
            var cell = new CsCell { Index = index };
            cell.Win.Union(x0, y0, x1, y1);
            list.Add(cell);
        }

        public static void AddVolWin(ProfileKind kind, double x0, double y0, double x1, double y1)
        {
            if (VolWins == null) VolWins = new Dictionary<ProfileKind, SheetWin>();
            SheetWin w;
            if (!VolWins.TryGetValue(kind, out w) || w == null)
            {
                w = new SheetWin();
                VolWins[kind] = w;
            }
            w.Union(x0, y0, x1, y1);
        }

        public static void AddPlanWin(double x0, double y0, double x1, double y1)
        {
            if (PlanWin == null) PlanWin = new SheetWin();
            PlanWin.Union(x0, y0, x1, y1);
        }

        public static void AddCoordWin(double x0, double y0, double x1, double y1)
        {
            if (CoordWin == null) CoordWin = new SheetWin();
            CoordWin.Union(x0, y0, x1, y1);
        }

        public static void AddSampWin(double x0, double y0, double x1, double y1)
        {
            if (SampWin == null) SampWin = new SheetWin();
            SampWin.Union(x0, y0, x1, y1);
        }

        public static void SetRuns(List<DomainResult> list)
        {
            Runs = list ?? new List<DomainResult>();
            Last = Runs.Count > 0 ? Runs[0] : null;
            NvkSites.AttachRuns(Runs);
        }

        public static List<DomainResult> AllRuns()
        {
            if (Runs != null && Runs.Count > 0) return Runs;
            var one = new List<DomainResult>();
            if (Last != null) one.Add(Last);
            return one;
        }

        /// <summary>Домены для объёмов: все участки, если VOL_JOIN, иначе текущий.</summary>
        public static List<DomainResult> VolRuns()
        {
            if (NvkUiState.VolJoinSites)
            {
                var joined = NvkSites.JoinedRuns();
                if (joined != null && joined.Count > 0) return joined;
            }
            return AllRuns();
        }

        public static TableCfg Tbl()
        {
            return Table ?? new TableCfg();
        }

        public static TableCfg Tbl(ProfileKind kind)
        {
            var t = (Table ?? new TableCfg()).Clone();
            string lbl = null;
            switch (kind)
            {
                case ProfileKind.Pesok:   lbl = NvkUiState.TblL2Label; break;
                case ProfileKind.Truba:   lbl = NvkUiState.TblL3Label; break;
                case ProfileKind.Zashita: lbl = NvkUiState.TblL4Label; break;
                case ProfileKind.Zasypka: lbl = NvkUiState.TblL5Label; break;
            }
            if (!string.IsNullOrEmpty(lbl)) t.LabelHeight = lbl;
            return t;
        }

        /// <summary>
        /// Вернуть явный список строк таблицы для уровня, или null (= использовать умолчание).
        /// </summary>
        public static List<Nvk.Core.TableRowDef> GetLevelRows(ProfileKind kind)
        {
            string s;
            switch (kind)
            {
                case ProfileKind.Razrabotka: s = NvkUiState.RowsL1; break;
                case ProfileKind.Pesok:      s = NvkUiState.RowsL2; break;
                case ProfileKind.Truba:      s = NvkUiState.RowsL3; break;
                case ProfileKind.Zashita:    s = NvkUiState.RowsL4; break;
                case ProfileKind.Zasypka:    s = NvkUiState.RowsL5; break;
                default: return null;
            }
            if (string.IsNullOrEmpty(s)) return null;
            return TableRowDef.EnsureCoverFirst(kind, TableRowDef.DeserializeList(s));
        }

        public static int Idx(ProfileKind kind)
        {
            int i = (int)kind - 1;
            if (i < 0) i = 0;
            if (i > 4) i = 4;
            return i;
        }

        public static bool TryEp(ProfileKind kind, out Point3d p)
        {
            int i = Idx(kind);
            p = EpLvl[i];
            return EpHas[i];
        }

        public static bool TryCs(ProfileKind kind, out Point3d p)
        {
            int i = Idx(kind);
            p = CsLvl[i];
            return CsHas[i];
        }
    }
}
