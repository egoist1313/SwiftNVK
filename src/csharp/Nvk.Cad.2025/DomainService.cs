using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Nvk.Core;
using CivilDb = Autodesk.Civil.DatabaseServices;

namespace Nvk.Cad
{
    static class DomainService
    {
        public static DomainResult Build(
            Transaction tr,
            IList<ObjectId> pipeIds,
            ObjectId surfId,
            string surfName,
            SectionConfig cfg,
            string stationsStr)
        {
            var segs = new List<PipeSeg>();
            string netName = null;
            foreach (var id in pipeIds)
            {
                var pipe = tr.GetObject(id, OpenMode.ForRead) as CivilDb.Pipe;
                if (pipe != null)
                {
                    segs.Add(PipeReader.Read(tr, pipe));
                    if (string.IsNullOrEmpty(netName))
                        netName = PipeReader.NetworkName(tr, pipe);
                }
            }
            if (segs.Count == 0)
                return null;

            var comps = Chain.Components(segs);
            if (comps.Count == 0)
                return null;
            var runs = new List<DomainResult>();
            for (int ri = 0; ri < comps.Count; ri++)
            {
                var r = BuildFromChain(tr, comps[ri], netName, surfId, surfName, cfg, stationsStr,
                    pipeIds.Count > 0 ? pipeIds[0].Database : null, ri);
                if (r != null) runs.Add(r);
            }
            NvkSession.SetRuns(runs);
            return NvkSession.Last;
        }

        static DomainResult BuildFromChain(
            Transaction tr,
            Chain chain,
            string netName,
            ObjectId surfId,
            string surfName,
            SectionConfig cfgSrc,
            string stationsStr,
            Database db,
            int runIndex)
        {
            var cfg = DupCfg(cfgSrc);
            if (!string.IsNullOrEmpty(NvkUiState.Zones))
                cfg.Zones = RoadZones.Parse(NvkUiState.Zones, chain.Total, runIndex);
            else if (cfg.Zones != null && runIndex == 0)
                cfg.Zones = RoadZones.FixOverlaps(cfg.Zones, chain.Total);
            else
                cfg.Zones = null;
            if (cfg.Zones == null || cfg.Zones.Count == 0)
                cfg.ApplyPavement();
            double mSt = cfg.SlopeLeft() > cfg.SlopeRight() ? cfg.SlopeLeft() : cfg.SlopeRight();
            var stations = StationCalc.Parse(stationsStr, chain, mSt, cfg.Zones);
            bool showMh = NvkSession.L1 == null || NvkSession.L1.ShowManholes;
            if (showMh)
            {
                var keep = new List<double>();
                foreach (var s in stations)
                    if (!chain.InManhole(s)) keep.Add(s);
                stations = keep;
                if (stations.Count == 0 && chain.Total > 1e-6)
                    stations.Add(chain.Total * 0.5);
            }
            var axis = AxisBuilder.FromChain(chain, AxisBuilder.DefaultStep);
            ObjectId earthId = ObjectId.Null;
            if (cfg.UseBlackEarth && !string.IsNullOrWhiteSpace(NvkUiState.BlackEarthHandle) && db != null)
                earthId = SurfaceSampler.ByHandle(db, NvkUiState.BlackEarthHandle);
            foreach (var p in axis)
                SampleAxis(tr, p, surfId, earthId, cfg);
            if (cfg.Zones != null)
            {
                foreach (var z in cfg.Zones)
                {
                    AddZoneStepPair(tr, axis, chain, z.Ch0, surfId, earthId, cfg);
                    AddZoneStepPair(tr, axis, chain, z.Ch1, surfId, earthId, cfg);
                }
            }
            foreach (double s in stations)
                AddEdge(tr, axis, chain, s, surfId, earthId, cfg);
            axis.Sort((a, b) => a.Ch.CompareTo(b.Ch));

            int seed = NvkSession.FreshRandom ? Environment.TickCount : StableSeed(chain.Segs);
            int nSc = Scatter.DenseCount(chain.Total);
            int fk = NvkSession.FreshRandomKind;
            bool freshAll = NvkSession.FreshRandom && fk == 0;
            bool FreshK(int k) => freshAll || (NvkSession.FreshRandom && fk == k);
            var scatter = ReuseOrGen(NvkSession.CacheScatter, nSc, seed + 1009, FreshK(1));
            var sc2 = ReuseOrGen(NvkSession.CacheScatter2, nSc, seed + 2017, FreshK(2));
            var sc3 = ReuseOrGen(NvkSession.CacheScatter3, nSc, seed + 3023, FreshK(3));
            var sc4 = ReuseOrGen(NvkSession.CacheScatter4, nSc, seed + 4049, FreshK(4));
            var sc5 = ReuseOrGen(NvkSession.CacheScatter5, nSc, seed + 5051, FreshK(5));
            double hw = cfg.HalfWidth > 0.05 ? cfg.HalfWidth : 3.0;
            var latOffs = ReuseOrLat(NvkSession.CacheLatOff, stations.Count, hw, seed, NvkSession.FreshRandom && fk == 0);
            var planTgt = ReuseOrGenPlan(NvkSession.CachePlanTgt, chain.Total, cfg.PlanInt, seed, NvkSession.FreshRandom && fk == 0);
            var planLat = ReuseOrPlanLat(NvkSession.CachePlanLat, planTgt.Count, hw, seed, NvkSession.FreshRandom && fk == 0);
            NvkSession.FreshRandom = false;
            NvkSession.FreshRandomKind = 0;

            var geoms = new List<SectionGeom>(stations.Count);
            const double step = 0.2;
            for (int si = 0; si < stations.Count; si++)
            {
                double s = stations[si];
                var hit = chain.Locate(s);
                // Сечение режется по оси трубы, как продольник. Смещение lat — только точки отбора.
                double pav = cfg.UseBlackEarth ? 0.0 : cfg.PavementAt(s);
                string zt = cfg.ZoneAt(s);
                double z = 0.0;
                bool hasZ = false;
                double sx0 = hit.X, sy0 = hit.Y;
                if (cfg.UseBlackEarth && !earthId.IsNull)
                {
                    var bz = SurfaceSampler.ElevationAt(tr, earthId, sx0, sy0);
                    if (bz.HasValue) { z = bz.Value; hasZ = true; }
                }
                if (!hasZ && !surfId.IsNull)
                {
                    var ez = SurfaceSampler.ElevationAt(tr, surfId, sx0, sy0);
                    if (ez.HasValue) { z = ez.Value - pav; hasZ = true; }
                }
                if (!hasZ)
                    z = hit.Invert + hit.Diam + cfg.ThkZash + 1.5;
                var g = SectionGeom.From(hit, cfg, z, pav);
                g.ZoneType = zt;
                g.LatOff = 0.0;
                g.InManhole = showMh && chain.InManhole(s);
                g.ManholeHalfW = chain.ManholeRAt(s, false);
                if (g.InManhole) continue;
                double fb = g.Elev.TrenchBottom + Scatter.Meters(s, chain.Total, scatter, RowScatter.Amp(HeightSrc.TrenchBottom));
                double fsa = g.Elev.SandTop + Scatter.Meters(s, chain.Total, sc2, RowScatter.Amp(HeightSrc.SandTop));
                double fi = g.Elev.Invert + Scatter.Meters(s, chain.Total, sc3, RowScatter.Amp(HeightSrc.Invert));
                double fz = g.Elev.ZashTop + Scatter.Meters(s, chain.Total, sc4, RowScatter.Amp(HeightSrc.ZashTop));
                double fs = g.Elev.Surface + Scatter.Meters(s, chain.Total, sc5, RowScatter.Amp(HeightSrc.Surface));
                g.FactSand = fsa;
                g.FactBottom = LineClamp.AfterScatterTrench(fb, g.FactSand ?? g.Elev.SandTop);
                g.FactPesokBot = g.FactBottom;
                g.FactInvert = fi;
                g.FactZash = LineClamp.AfterScatterZash(fz, g.FactSand ?? g.Elev.SandTop, g.Elev.Surface);
                g.FactSurf = LineClamp.AfterScatterBackfill(fs, g.FactZash ?? g.Elev.ZashTop);
                g.ApplyDrawnScatter(cfg.SingleScatter);
                var samples = new List<SurfPt>();
                for (double t = -hw; t <= hw + 1e-9; t += step)
                {
                    double sx = hit.X + hit.Nx * t;
                    double sy = hit.Y + hit.Ny * t;
                    double ztZ = z;
                    if (cfg.UseBlackEarth && !earthId.IsNull)
                    {
                        var bz = SurfaceSampler.ElevationAt(tr, earthId, sx, sy);
                        if (bz.HasValue) ztZ = bz.Value;
                    }
                    else if (!surfId.IsNull)
                    {
                        var ez = SurfaceSampler.ElevationAt(tr, surfId, sx, sy);
                        if (ez.HasValue) ztZ = ez.Value - pav;
                    }
                    samples.Add(new SurfPt(t, ztZ));
                }
                g.SurfPts = samples;
                geoms.Add(g);
            }

            string segId = MakeSegId(chain.Segs);

            var result = new DomainResult
            {
                Chain = chain,
                Geoms = geoms,
                Cfg = cfg,
                SurfName = surfName,
                SegId = segId,
                NetName = netName,
                Axis = axis,
                ScatterVals = scatter,
                ScatterL2 = sc2,
                ScatterL3 = sc3,
                ScatterL4 = sc4,
                ScatterL5 = sc5,
                LatOffs = latOffs,
                PlanTargets = planTgt,
                PlanLatOffs = planLat,
                Dump = DomainDump.Format(chain, geoms, surfName, axis, cfg)
            };
            NvkSession.CacheScatter = scatter;
            NvkSession.CacheScatter2 = sc2;
            NvkSession.CacheScatter3 = sc3;
            NvkSession.CacheScatter4 = sc4;
            NvkSession.CacheScatter5 = sc5;
            NvkSession.CacheLatOff = latOffs;
            NvkSession.CachePlanTgt = planTgt;
            NvkSession.CachePlanLat = planLat;
            return result;
        }

        public static string BuildDump(
            Transaction tr,
            IList<ObjectId> pipeIds,
            ObjectId surfId,
            string surfName,
            SectionConfig cfg,
            string stationsStr)
        {
            Build(tr, pipeIds, surfId, surfName, cfg, stationsStr);
            var runs = NvkSession.AllRuns();
            if (runs.Count == 0) return "[НВК] трубы не прочитаны.";
            if (runs.Count == 1) return runs[0].Dump;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[НВК] участков: " + runs.Count.ToString());
            for (int i = 0; i < runs.Count; i++)
            {
                sb.AppendLine("[НВК] --- участок " + (i + 1).ToString()
                    + "  " + (runs[i].NetName ?? "") + "  " + (runs[i].SegId ?? "") + " ---");
                sb.Append(runs[i].Dump);
            }
            return sb.ToString();
        }

        static void SampleAxis(Transaction tr, AxisPt p, ObjectId surfId, ObjectId earthId, SectionConfig cfg)
        {
            if (!surfId.IsNull)
            {
                var ez = SurfaceSampler.ElevationAt(tr, surfId, p.X, p.Y);
                if (ez.HasValue) p.DesignZ = ez.Value;
            }
            if (!earthId.IsNull)
            {
                var bz = SurfaceSampler.ElevationAt(tr, earthId, p.X, p.Y);
                if (bz.HasValue)
                {
                    p.EarthZ = bz.Value;
                    p.Surface = bz.Value;
                }
                else if (p.DesignZ.HasValue)
                    p.Surface = p.DesignZ;
            }
            else if (p.DesignZ.HasValue)
            {
                double pav = cfg.UseBlackEarth ? 0.0 : cfg.PavementAt(p.Ch);
                double earthRaw = p.DesignZ.Value - pav;
                p.EarthZ = earthRaw;
                p.Surface = earthRaw;
            }
            p.ClampToExcavation(cfg);
        }

        static void AddEdge(Transaction tr, List<AxisPt> axis, Chain chain, double ch,
            ObjectId surfId, ObjectId earthId, SectionConfig cfg)
        {
            if (ch < 1e-4 || ch > chain.Total - 1e-4) return;
            foreach (var p in axis)
                if (Math.Abs(p.Ch - ch) < 1e-3) return;
            var hit = chain.Locate(ch);
            var pt = new AxisPt
            {
                Ch = ch, X = hit.X, Y = hit.Y, Invert = hit.Invert, Diam = hit.Diam
            };
            SampleAxis(tr, pt, surfId, earthId, cfg);
            axis.Add(pt);
        }

        // Добавляет пару точек на границе зоны для ступенчатого профиля в EarthPts.
        // pavLeft  = покрытие чуть левее ch (левая зона).
        // pavRight = покрытие чуть правее ch (правая зона): используем ch+1e-4, чтобы корректно
        //            поймать конец зоны (TypeAt имеет включительную правую границу ch <= Ch1+ε,
        //            поэтому PavementAt(Ch1) = thk_left, а PavementAt(Ch1+1e-4) = 0 или thk_right).
        static void AddZoneStepPair(Transaction tr, List<AxisPt> axis, Chain chain, double ch,
            ObjectId surfId, ObjectId earthId, SectionConfig cfg)
        {
            if (ch < 1e-4 || ch > chain.Total - 1e-4) return;
            double pavLeft  = cfg.PavementAt(Math.Max(0.0, ch - 1e-4));
            double pavRight = cfg.PavementAt(Math.Min(chain.Total, ch + 1e-4));
            if (Math.Abs(pavLeft - pavRight) < 1e-6) return;
            var hit = chain.Locate(ch);
            // Граничная точка (правая зона) — добавляем только если ещё нет в пределах 1e-4
            bool hasBoundary = false;
            foreach (var p in axis)
                if (Math.Abs(p.Ch - ch) < 1e-4) { hasBoundary = true; break; }
            if (!hasBoundary)
            {
                var ptR = new AxisPt { Ch = ch, X = hit.X, Y = hit.Y, Invert = hit.Invert, Diam = hit.Diam };
                SampleAxis(tr, ptR, surfId, earthId, cfg);
                // Override EarthZ to right-side pav: TypeAt(ch) may return left zone (inclusive boundary).
                if (earthId.IsNull && ptR.DesignZ.HasValue)
                {
                    double earthRaw = ptR.DesignZ.Value - pavRight;
                    ptR.EarthZ = earthRaw;
                    ptR.Surface = earthRaw;
                    ptR.ClampToExcavation(cfg);
                }
                axis.Add(ptR);
            }
            // Точка-спутник (левая зона) на ch-1e-7 — попадает в условие condition-1 EarthPts
            bool hasCompanion = false;
            foreach (var p in axis)
                if (Math.Abs(p.Ch - (ch - 1e-7)) < 1e-8) { hasCompanion = true; break; }
            if (!hasCompanion)
            {
                var ptL = new AxisPt { Ch = ch - 1e-7, X = hit.X, Y = hit.Y, Invert = hit.Invert, Diam = hit.Diam };
                SampleAxis(tr, ptL, surfId, earthId, cfg);
                axis.Add(ptL);
            }
        }

        static SectionConfig DupCfg(SectionConfig s)
        {
            if (s == null) return new SectionConfig();
            return new SectionConfig
            {
                Bedding = s.Bedding,
                ThkZash = s.ThkZash,
                Joint = s.Joint,
                HalfWidth = s.HalfWidth,
                SlopeM = s.SlopeM,
                SlopeL = s.SlopeL,
                SlopeR = s.SlopeR,
                TrenchLeft = s.TrenchLeft,
                TrenchRight = s.TrenchRight,
                ThkAsph = s.ThkAsph,
                ThkGazn = s.ThkGazn,
                ThkTrot = s.ThkTrot,
                ThkOther = s.ThkOther,
                ThkPavement = s.ThkPavement,
                UseBlackEarth = s.UseBlackEarth,
                ZoneType = s.ZoneType,
                PavAll = s.PavAll,
                FillAsph = s.FillAsph,
                FillTrot = s.FillTrot,
                FillGazn = s.FillGazn,
                FillOther = s.FillOther,
                UsePipeWall = s.UsePipeWall,
                Walls = s.Walls,
                PlanEn = s.PlanEn,
                PlanInt = s.PlanInt,
                PlanMin = s.PlanMin,
                ProjFact = s.ProjFact,
                SingleScatter = s.SingleScatter,
                UseScatterRazr = s.UseScatterRazr,
                UseScatterTruba = s.UseScatterTruba,
                UseScatterZashita = s.UseScatterZashita,
                UseScatterZasypka = s.UseScatterZasypka,
                ScatterRazr = s.ScatterRazr,
                ScatterTruba = s.ScatterTruba,
                ScatterZashita = s.ScatterZashita,
                ScatterZasypka = s.ScatterZasypka
            };
        }

        static string MakeSegId(IEnumerable<PipeSeg> segs)
        {
            var hs = new List<string>();
            foreach (var s in segs)
                if (!string.IsNullOrEmpty(s.Handle)) hs.Add(s.Handle.ToUpperInvariant());
            hs.Sort(StringComparer.Ordinal);
            string join = string.Join(";", hs.ToArray());
            int h = 17;
            foreach (char c in join)
                h = h * 31 + c;
            string id = "P" + Math.Abs(h).ToString("X8");
            if (hs.Count > 0)
            {
                string a = hs[0], b = hs[hs.Count - 1];
                if (a.Length > 4) a = a.Substring(a.Length - 4);
                if (b.Length > 4) b = b.Substring(b.Length - 4);
                id = id + "_" + a + b;
            }
            return id;
        }

        static int StableSeed(IEnumerable<PipeSeg> segs)
        {
            int h = 17;
            foreach (var s in segs)
            {
                string id = s.Handle ?? "";
                for (int i = 0; i < id.Length; i++)
                    h = h * 31 + id[i];
            }
            return h;
        }

        static List<double> ReuseOrGen(List<double> cache, int n, int seed, bool fresh)
        {
            if (!fresh && cache != null && cache.Count >= 2)
                return cache;
            return Scatter.Generate(n, seed);
        }

        static List<double> ReuseOrLat(List<double> cache, int n, double hw, int seed, bool fresh)
        {
            if (!fresh && cache != null && cache.Count == n)
                return cache;
            var list = new List<double>(n);
            for (int i = 0; i < n; i++)
                list.Add(Scatter.LatOff(hw, seed, i));
            return list;
        }

        static List<double> ReuseOrGenPlan(List<double> cache, double total, double planInt, int seed, bool fresh)
        {
            if (!fresh && cache != null && cache.Count > 0)
                return cache;
            return Scatter.PlanTargets(total, planInt, seed);
        }

        static List<double> ReuseOrPlanLat(List<double> cache, int n, double hw, int seed, bool fresh)
        {
            if (!fresh && cache != null && cache.Count == n)
                return cache;
            var list = new List<double>(n);
            for (int i = 0; i < n; i++)
                list.Add(Scatter.PlanLat(hw, seed, i));
            return list;
        }
    }
}
