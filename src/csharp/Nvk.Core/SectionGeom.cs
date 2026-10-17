using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;

namespace Nvk.Core
{
    public sealed class SectionConfig
    {
        public double Bedding { get; set; } = 0.10;
        public double ThkZash { get; set; } = 0.30;
        public JointType Joint { get; set; } = JointType.Socket;
        public double HalfWidth { get; set; } = 3.0;
        public double SlopeM { get; set; } = 0.0;
        /// <summary>NaN = взять SlopeM. 0 = вертикаль с этой стороны.</summary>
        public double SlopeL { get; set; } = double.NaN;
        public double SlopeR { get; set; } = double.NaN;
        /// <summary>0 = половина каталога. Иначе ширина дна слева/справа от оси, м.</summary>
        public double TrenchLeft { get; set; }
        public double TrenchRight { get; set; }

        public double SlopeLeft()
        {
            double m = double.IsNaN(SlopeL) ? SlopeM : SlopeL;
            return m < 0.0 ? 0.0 : m;
        }

        public double SlopeRight()
        {
            double m = double.IsNaN(SlopeR) ? SlopeM : SlopeR;
            return m < 0.0 ? 0.0 : m;
        }

        public double BottomLeft(double diam)
        {
            if (TrenchLeft > 1e-9) return TrenchLeft;
            return TrenchWidth.For(diam, Joint) * 0.5;
        }

        public double BottomRight(double diam)
        {
            if (TrenchRight > 1e-9) return TrenchRight;
            return TrenchWidth.For(diam, Joint) * 0.5;
        }

        public double BottomFull(double diam)
        {
            return BottomLeft(diam) + BottomRight(diam);
        }
        /// <summary>Каталог толщин. На трассу попадают только через зоны или PavAll.</summary>
        public double ThkAsph { get; set; } = 1.02;
        public double ThkGazn { get; set; } = 0.15;
        public double ThkTrot { get; set; } = 0.31;
        public double ThkOther { get; set; } = 0.35;
        public double ThkPavement { get; set; } = 0.0;
        public bool UseBlackEarth { get; set; }
        public string ZoneType { get; set; }
        public List<RoadZone> Zones { get; set; }
        public bool PavAll { get; set; }
        /// <summary>L5 засыпка: SAND или SOIL под типом покрытия.</summary>
        public string FillAsph { get; set; } = "SAND";
        public string FillTrot { get; set; } = "SAND";
        public string FillGazn { get; set; } = "SOIL";
        public string FillOther { get; set; } = "SOIL";

        public bool ProjFact { get; set; }
        public bool SingleScatter { get; set; } = true;
        public bool UseScatterRazr { get; set; } = true;
        public bool UseScatterTruba { get; set; }
        public bool UseScatterZashita { get; set; } = true;
        public bool UseScatterZasypka { get; set; } = true;
        public double ScatterRazr { get; set; } = 0.05;
        public double ScatterTruba { get; set; } = 0.005;
        public double ScatterZashita { get; set; } = 0.05;
        public double ScatterZasypka { get; set; } = 0.05;

        public bool Annotative { get; set; }
        public double AnnGap { get; set; } = 10.0;
        public double CsYgap { get; set; } = 100.0;
        public bool PlanEn { get; set; } = true;
        public double PlanInt { get; set; } = 50.0;
        public double PlanMin { get; set; } = 15.0;
        public bool DrawProjections { get; set; } = true;
        public short ProjectionColor { get; set; } = 256;
        public bool UseTextHeight { get; set; } = true;
        public bool UsePipeWall { get; set; }
        public List<PipeWallRow> Walls { get; set; }

        public double WallAt(double diam)
        {
            return PipeWalls.MetersAt(diam, Walls, UsePipeWall);
        }
        public bool DeltaH { get; set; } = true;
        public string LabelHeight { get; set; }
        public string LabelLength { get; set; }
        public string LabelSlope { get; set; }

        public void ApplyPavement()
        {
            if (Zones != null && Zones.Count > 0)
                return;
            if (UseBlackEarth || !PavAll)
            {
                ThkPavement = 0.0;
                ZoneType = null;
                return;
            }
            if (ThkAsph > 0.0)
            {
                ThkPavement = ThkAsph;
                if (string.IsNullOrEmpty(ZoneType)) ZoneType = "ASPH";
                return;
            }
            if (ThkTrot > 0.0)
            {
                ThkPavement = ThkTrot;
                ZoneType = "TROT";
                return;
            }
            if (ThkGazn > 0.0)
            {
                ThkPavement = ThkGazn;
                ZoneType = "GAZN";
                return;
            }
            if (ThkOther > 0.0)
            {
                ThkPavement = ThkOther;
                ZoneType = "OTHER";
            }
        }

        public double PavementAt(double ch)
        {
            if (UseBlackEarth) return 0.0;
            if (Zones != null && Zones.Count > 0)
                return RoadZones.ThkAt(ch, Zones, this);
            if (!PavAll) return 0.0;
            if (ThkPavement > 0.0) return ThkPavement;
            if (ThkAsph > 0.0) return ThkAsph;
            if (ThkTrot > 0.0) return ThkTrot;
            if (ThkGazn > 0.0) return ThkGazn;
            return ThkOther > 0.0 ? ThkOther : 0.0;
        }

        public string ZoneAt(double ch)
        {
            if (Zones != null && Zones.Count > 0)
                return RoadZones.TypeAt(ch, Zones);
            return string.IsNullOrEmpty(ZoneType) ? null : ZoneType;
        }
    }

    /// <summary>Геометрия одного поперечника: именованные линии + ширина дна. Без графики.</summary>
    public sealed class SectionGeom
    {
        public StationHit Hit { get; set; }
        public StationElevations Elev { get; set; }
        public double TrenchW { get; set; }
        public double TrenchLeft { get; set; }
        public double TrenchRight { get; set; }
        public JointType Joint { get; set; }
        public List<SurfPt> SurfPts { get; set; }
        public string ZoneType { get; set; }
        public double LatOff { get; set; }
        /// <summary>Пикет строго внутри колодца: шахта, траншеи нет.</summary>
        public bool InManhole { get; set; }
        public double ManholeHalfW { get; set; }
        public double? FactBottom { get; set; }
        public double? FactPesokBot { get; set; }
        public double? FactSand { get; set; }
        public double? FactInvert { get; set; }
        public double? FactZash { get; set; }
        public double? FactSurf { get; set; }
        /// <summary>Толщина дорожного покрытия. SurfPts уже вычтены (TIN - Pavement). SurfaceRaw = Surface + Pavement.</summary>
        public double Pavement { get; set; }

        public static SectionGeom From(StationHit hit, SectionConfig cfg, double surfaceRaw, double pav = 0.0)
        {
            double pavUse = pav < 0.0 ? 0.0 : pav;
            var elev = new StationElevations(
                hit.Station, hit.Invert, hit.Diam,
                cfg.Bedding, cfg.ThkZash, surfaceRaw, surfaceRaw + pavUse,
                cfg.WallAt(hit.Diam));
            double bL = cfg.BottomLeft(hit.Diam);
            double bR = cfg.BottomRight(hit.Diam);
            return new SectionGeom
            {
                Hit = hit,
                Elev = elev,
                Joint = cfg.Joint,
                TrenchLeft = bL,
                TrenchRight = bR,
                TrenchW = bL + bR,
                ZoneType = cfg != null ? cfg.ZoneType : null,
                Pavement = pav < 0.0 ? 0.0 : pav
            };
        }

        public double Thickness(ProfileKind kind)
        {
            return LayerStack.Thickness(kind, Elev);
        }

        /// <summary>
        /// Однострочный scatter: L1…L4 как на продольнике (после рандома).
        /// L6 остаётся TIN−одежда и не поднимается к L1/L4 — иначе песок
        /// на L2 и котлован на L1 рисуются на разных отметках.
        /// </summary>
        public void ApplyDrawnScatter(bool singleScatter)
        {
            if (!singleScatter || Elev == null) return;
            double trench = FactBottom ?? Elev.TrenchBottom;
            double sand = FactSand ?? Elev.SandTop;
            double inv = FactInvert ?? Elev.Invert;
            double wall = Math.Max(0.0, Elev.Invert - Elev.SandTop);
            double pipe = inv + Elev.Diam + wall;
            double zash = FactZash ?? Elev.ZashTop;
            double earth = Elev.Surface;
            trench = LineClamp.AfterScatterTrench(trench, sand);
            zash = LineClamp.AfterScatterZash(zash, sand, earth);
            Elev.SetDrawn(trench, sand, inv, pipe, zash, earth);
        }

        public bool IsDrawn(ProfileKind kind)
        {
            return LayerStack.IsDrawn(kind, Elev);
        }
    }

    public static class DomainDump
    {
        public static string Format(Chain chain, IReadOnlyList<SectionGeom> geoms, string surfName)
        {
            return Format(chain, geoms, surfName, null, null);
        }

        public static string Format(Chain chain, IReadOnlyList<SectionGeom> geoms, string surfName,
            IReadOnlyList<AxisPt> axis, SectionConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[НВК] === домен (без графики) ===");
            int nMismatch = 0;
            if (cfg != null)
            {
                sb.AppendLine("[НВК] одежда каталог А=" + F(cfg.ThkAsph)
                    + " Т=" + F(cfg.ThkTrot) + " Г=" + F(cfg.ThkGazn) + " П=" + F(cfg.ThkOther)
                    + "  PavAll=" + (cfg.PavAll ? "1" : "0")
                    + "  черн.земля=" + (cfg.UseBlackEarth ? "1" : "0")
                    + "  зоны=" + (cfg.Zones == null ? 0 : cfg.Zones.Count)
                    + (cfg.Zones != null && cfg.Zones.Count > 0 ? " [" + RoadZones.Format(cfg.Zones) + "]" : ""));
            }
            sb.AppendLine("[НВК] труб: " + chain.Segs.Count.ToString(CultureInfo.InvariantCulture)
                          + "  длина: " + F(chain.Total) + " м"
                          + "  TIN: " + (string.IsNullOrEmpty(surfName) ? "(нет)" : surfName));
            int i = 0;
            double ch = 0.0;
            foreach (var pd in chain.Segs)
            {
                i++;
                sb.AppendLine("[НВК]   труба " + i.ToString(CultureInfo.InvariantCulture)
                    + "  d=" + F(pd.Diam)
                    + "  L=" + F(pd.Length2d)
                    + "  pik " + F(ch) + "…" + F(ch + pd.Length2d)
                    + "  лоток " + F(pd.InvertStart) + "→" + F(pd.InvertEnd)
                    + (string.IsNullOrEmpty(pd.StartName) ? "" : "  " + pd.StartName)
                    + (string.IsNullOrEmpty(pd.EndName) ? "" : "–" + pd.EndName));
                ch += pd.Length2d;
            }
            sb.AppendLine("[НВК] станции: " + geoms.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var g in geoms)
            {
                var e = g.Elev;
                sb.AppendLine("[НВК]   ст " + F(e.Station)
                    + "  d=" + F(e.Diam)
                    + "  inv=" + F(e.Invert)
                    + "  xy=" + F(g.Hit.X) + "," + F(g.Hit.Y));
                double pav = g.Pavement;
                string zone = g.ZoneType ?? "-";
                double csL0 = e.Surface + pav;
                double csThk = csL0 - e.Surface;
                bool csOk = Math.Abs(csThk - pav) < 0.0015;
                if (!csOk) nMismatch++;
                sb.AppendLine("[НВК]     TrenchBottom(L1)=" + F(e.TrenchBottom)
                    + "  SandTop(L2)=" + F(e.SandTop)
                    + "  PipeTop=" + F(e.PipeTop)
                    + "  ZashTop(L4)=" + F(e.ZashTop)
                    + "  Surface(L6)=" + F(e.Surface)
                    + "  L0=" + F(csL0)
                    + "  pav=" + F(pav) + "  зона=" + zone
                    + "  CS_одежда=" + F(csThk)
                    + (csOk ? "  ок" : "  *** СЪЕХАЛО ***")
                    + (g.InManhole ? "  колодец(пусто)" : "")
                    + (e.Surface < e.TrenchBottom - 1e-4 ? "  L6<L1(клип штриховки)" : ""));
                double mL = cfg != null ? cfg.SlopeLeft() : 0.0;
                double mR = cfg != null ? cfg.SlopeRight() : 0.0;
                double a1 = SectionArea.For(g, ProfileKind.Razrabotka, mL, mR) ?? 0.0;
                double a2 = SectionArea.For(g, ProfileKind.Pesok, mL, mR) ?? 0.0;
                double a4 = SectionArea.For(g, ProfileKind.Zashita, mL, mR) ?? 0.0;
                double a5 = SectionArea.For(g, ProfileKind.Zasypka, mL, mR) ?? 0.0;
                double hole = SectionArea.PipeInLayer(e.Invert, e.Diam, 0.0, e.SandTop, e.ZashTop);
                sb.AppendLine("[НВК]     CS_S м²  L1=" + F(a1) + "  L2песок=" + F(a2)
                    + "  L4защ=" + F(a4) + "  L5засып=" + F(a5)
                    + "  дырка_трубы=" + F(hole)
                    + "  L2+L4+L5+дырка=" + F(a2 + a4 + a5 + hole));
                sb.AppendLine("[НВК]     ширина дна=" + F(g.TrenchW)
                    + "  L=" + F(g.TrenchLeft) + "  R=" + F(g.TrenchRight)
                    + "  mL=" + F(mL) + "  mR=" + F(mR)
                    + "  толщина L2песок=" + F(g.Thickness(ProfileKind.Pesok))
                    + "  L4защ=" + F(g.Thickness(ProfileKind.Zashita))
                    + "  защ_над_трубой=" + F(e.ZashTop - e.PipeTop)
                    + "  L5засып=" + (g.IsDrawn(ProfileKind.Zasypka) ? F(g.Thickness(ProfileKind.Zasypka)) : "нет")
                    + "  d=" + F(e.Diam));
                if (g.SurfPts != null && g.SurfPts.Count > 0)
                    sb.AppendLine("[НВК]     TIN-проб " + g.SurfPts.Count.ToString(CultureInfo.InvariantCulture)
                        + "  T=" + F(g.SurfPts[0].T) + "…" + F(g.SurfPts[g.SurfPts.Count - 1].T)
                        + "  Z=" + F(MinZ(g.SurfPts)) + "…" + F(MaxZ(g.SurfPts)));
            }
            if (axis != null && axis.Count > 0 && cfg != null)
            {
                sb.AppendLine("[НВК] --- ось L0/L6 (одежда = L0−L6, должна = pav_задан) ---");
                for (int k = 0; k < axis.Count; k++)
                {
                    var p = axis[k];
                    double l0 = p.DesignZ ?? p.NamedZ(NamedLine.Surface, cfg);
                    double l6 = p.EarthZ ?? p.NamedZ(NamedLine.Surface, cfg);
                    double l1 = p.NamedZ(NamedLine.TrenchBottom, cfg);
                    double l4 = p.NamedZ(NamedLine.ZashTop, cfg);
                    double pipe = p.NamedZ(NamedLine.PipeTop, cfg);
                    double pav = cfg.PavementAt(p.Ch);
                    string zt = cfg.ZoneAt(p.Ch) ?? "-";
                    double thk = l0 - l6;
                    bool exact = Math.Abs(thk - pav) < 0.0015;
                    if (!exact) nMismatch++;
                    sb.AppendLine("[НВК]   pik=" + F(p.Ch)
                        + "  зона=" + zt
                        + "  pav_задан=" + F(pav)
                        + "  L0=" + F(l0)
                        + "  L6=" + F(l6)
                        + "  L0-L6=" + F(thk)
                        + (exact ? "  ок" : "  *** СЪЕХАЛО ***")
                        + "  L1=" + F(l1)
                        + "  L4=" + F(l4)
                        + "  над_трубой=" + F(l0 - pipe)
                        + (l6 < l4 - 1e-4 ? "  L6<L4" : "")
                        + (l6 < l1 - 1e-4 ? "  L6<L1(клип штриховки)" : ""));
                }
            }
            sb.AppendLine("[НВК] съехало точек: " + nMismatch.ToString(CultureInfo.InvariantCulture)
                + (nMismatch == 0 ? "  (одежда = заданная)" : "  *** толщина не совпала ***"));
            sb.AppendLine("[НВК] низ разработки = низ песка; верх песка = низ защиты; верх защиты = низ засыпки.");
            return sb.ToString();
        }

        static double MinZ(List<SurfPt> pts)
        {
            double z = pts[0].Z;
            for (int i = 1; i < pts.Count; i++)
                if (pts[i].Z < z) z = pts[i].Z;
            return z;
        }

        static double MaxZ(List<SurfPt> pts)
        {
            double z = pts[0].Z;
            for (int i = 1; i < pts.Count; i++)
                if (pts[i].Z > z) z = pts[i].Z;
            return z;
        }

        static string F(double v)
        {
            return v.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
