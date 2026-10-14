using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>Таблица отметок/длин/уклонов рядом с блоком продольника. Не внутри блока.</summary>
    static class PoperDrawer
    {
        const string Layer = "NVK-POPER";

        public static string Draw(Database db, Transaction tr)
        {
            var last = NvkSession.Last;
            var ep = NvkSession.LastEp;
            if (last == null || last.Chain == null)
                return "[НВК] POPER: нет домена.";
            EnsureLayer(db, tr, Layer, 7);
            EraseOld(db, tr);
            if (ep == null || ep.Kinds == null || ep.Kinds.Count == 0)
                return "[НВК] POPER: нет продольников. Сначала НВК-ПРОФИЛЬ.";
            // Таблица встроена в каждый лист профиля (NVK-EP-*) — отдельный NVK-POPER не нужен.
            return "[НВК] POPER: таблица в листах профиля (NVK-EP-*).";
        }

        static void ApplyAxisSurface(List<AnnotPoint> heights, List<AxisPt> axis, ProfileKind kind, SectionConfig cfg)
        {
            if (axis == null || axis.Count == 0) return;
            var line = ProfileAnnot.HeightLine(kind);
            foreach (var h in heights)
            {
                AxisPt best = null;
                double bestD = 1e9;
                foreach (var a in axis)
                {
                    double d = Math.Abs(a.Ch - h.Ch);
                    if (d < bestD) { bestD = d; best = a; }
                }
                if (best != null && bestD < 0.05)
                {
                    h.Z = best.NamedZ(line, cfg);
                    h.Text = ProfileAnnot.FmtZ(h.Z);
                }
            }
        }

        static int DrawTable(BlockTableRecord ms, Transaction tr, Point3d origin, double lSc,
            ProfileKind kind, List<AnnotPoint> heights, List<AnnotSeg> segs,
            SectionConfig cfg, DomainResult last)
        {
            var tbl = NvkSession.Tbl(kind);
            double th = tbl.TextH;
            double off = tbl.Offset;
            int n = 0;
            if (heights == null || heights.Count == 0) return 0;

            HeightSrc hsrc = ProfileAnnot.HeightLine(kind) == NamedLine.TrenchBottom
                ? HeightSrc.TrenchBottom
                : ProfileAnnot.HeightLine(kind) == NamedLine.ZashTop ? HeightSrc.ZashTop
                : ProfileAnnot.HeightLine(kind) == NamedLine.Surface ? HeightSrc.Surface
                : HeightSrc.SandTop;
            double amp = RowScatter.Amp(hsrc);
            var vals = last != null ? RowScatter.Vals(last, hsrc) : null;
            bool hasFact = cfg != null && cfg.ProjFact
                && amp > 0.0
                && vals != null && vals.Count > 0;
            double chainTotal = last?.Chain?.Total ?? 1.0;

            var zs = new List<double>();
            foreach (var h in heights) zs.Add(h.Z);
            var lens = new List<double>();
            if (segs != null)
                foreach (var s in segs) lens.Add(s.Ch1 - s.Ch0);
            int maxH = TableLayout.MaxChars(zs, tbl.RoundTo, 4);
            int maxL = TableLayout.MaxChars(lens, 2, 3);
            double hH = TableLayout.RowHeight(TableRowKind.Height, th, maxH, maxL);
            double hL = tbl.ShowLengths ? TableLayout.RowHeight(TableRowKind.Length, th, maxH, maxL) : 0.0;
            double hS = tbl.ShowSlopes ? TableLayout.RowHeight(TableRowKind.Slope, th, maxH, maxL) : 0.0;

            string projLabel = hasFact ? ProfileAnnot.HeightLabel(kind) + ", проект" : ProfileAnnot.HeightLabel(kind);
            string factLabel = ProfileAnnot.HeightLabel(kind) + ", факт";
            var labels = new List<string>();
            labels.Add(projLabel);
            if (hasFact) labels.Add(factLabel);
            if (tbl.ShowLengths) labels.Add("Расстояние, м");
            if (tbl.ShowSlopes) labels.Add("Уклон, ‰");
            double labW = TableLayout.LabelColW(tbl, labels);

            double x0 = origin.X + heights[0].Ch * lSc;
            double x1 = origin.X + heights[heights.Count - 1].Ch * lSc;
            double dataL = x0 - th;
            double dataR = x1 + th;
            double xLbl = dataL - labW;
            double y0 = origin.Y - th;
            double yH = y0 - hH;
            double yHF = hasFact ? yH - hH : yH;
            double yL = yHF - hL;
            double yS = yL - hS;
            double yBot = yS;

            AddLine(ms, tr, xLbl, y0, dataR, y0);
            AddLine(ms, tr, xLbl, yH, dataR, yH);
            if (hasFact) AddLine(ms, tr, xLbl, yHF, dataR, yHF);
            if (tbl.ShowLengths) AddLine(ms, tr, xLbl, yL, dataR, yL);
            if (tbl.ShowSlopes) AddLine(ms, tr, xLbl, yS, dataR, yS);
            AddLine(ms, tr, xLbl, y0, xLbl, yBot);
            AddLine(ms, tr, dataL, y0, dataL, yBot);
            AddLine(ms, tr, dataR, y0, dataR, yBot);

            var ep = NvkSession.LastEp;
            double lScLeg = ep != null ? ep.LScale : 1.0;
            double hScLeg = ep != null ? ep.HScale : 10.0;
            double oshLeg = ep != null ? ep.Osh : 0.0;
            double paper = NvkUiState.VpScaleEp >= 1.0 ? NvkUiState.VpScaleEp
                : (NvkSession.Cannoscale > 0.0 ? NvkSession.Cannoscale : 1000.0);
            NvkText.Make(ms, tr, Layer, x0, y0 + 1.5 * th,
                TableLayout.ScaleLegend(
                    TableLayout.LegendDenom(paper, lScLeg),
                    TableLayout.LegendDenom(paper, hScLeg),
                    oshLeg),
                th, AttachmentPoint.BottomRight, 0.0, 18.0 * th, false);
            n++;

            NvkText.Make(ms, tr, Layer, 0.5 * (xLbl + dataL), 0.5 * (y0 + yH), projLabel, th, AttachmentPoint.MiddleCenter, 0.0);
            n++;
            if (hasFact)
            {
                NvkText.Make(ms, tr, Layer, 0.5 * (xLbl + dataL), 0.5 * (yH + yHF), factLabel, th, AttachmentPoint.MiddleCenter, 0.0);
                n++;
            }
            if (tbl.ShowLengths)
            {
                NvkText.Make(ms, tr, Layer, 0.5 * (xLbl + dataL), 0.5 * (yHF + yL), "Расстояние, м", th, AttachmentPoint.MiddleCenter, 0.0);
                n++;
            }
            if (tbl.ShowSlopes)
            {
                NvkText.Make(ms, tr, Layer, 0.5 * (xLbl + dataL), 0.5 * (yL + yS), "Уклон, ‰", th, AttachmentPoint.MiddleCenter, 0.0);
                n++;
            }

            double prevX = double.NaN;
            foreach (var h in heights)
            {
                double x = origin.X + h.Ch * lSc;
                double disp = x;
                if (!double.IsNaN(prevX) && (disp - prevX) < th)
                    disp = prevX + th;
                prevX = disp;
                NvkText.Make(ms, tr, Layer, disp, 0.5 * (y0 + yH), TableLayout.Rtos(h.Z, tbl.RoundTo), th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
                n++;
            }

            if (hasFact)
            {
                double prevXF = double.NaN;
                foreach (var h in heights)
                {
                    double x = origin.X + h.Ch * lSc;
                    double dz = Scatter.Meters(h.Ch, chainTotal, vals, amp);
                    double factZ = h.Z + dz;
                    double disp = x;
                    if (!double.IsNaN(prevXF) && (disp - prevXF) < th)
                        disp = prevXF + th;
                    prevXF = disp;
                    NvkText.Make(ms, tr, Layer, disp, 0.5 * (yH + yHF), TableLayout.Rtos(factZ, tbl.RoundTo), th, AttachmentPoint.MiddleCenter, Math.PI * 0.5);
                    n++;
                }
            }

            if (tbl.ShowLengths && segs != null)
            {
                foreach (var h in heights)
                {
                    double x = origin.X + h.Ch * lSc;
                    AddLine(ms, tr, x, yHF, x, yL);
                }
                foreach (var s in segs)
                {
                    double xm = origin.X + s.Mid * lSc;
                    double xb = origin.X + s.Ch1 * lSc;
                    double w = (s.Ch1 - s.Ch0) * lSc;
                    string lenStr = TableLayout.Rtos(s.Ch1 - s.Ch0, tbl.RoundTo > 0 ? tbl.RoundTo : 2);
                    if (w < th)
                        NvkText.Make(ms, tr, Layer, xb, yL - th - off, lenStr, th, AttachmentPoint.TopRight, 0.0);
                    else
                    {
                        double ang = TableLayout.RotateLength(w, th) ? Math.PI * 0.5 : 0.0;
                        NvkText.Make(ms, tr, Layer, xm, 0.5 * (yHF + yL), lenStr, th, AttachmentPoint.MiddleCenter, ang);
                    }
                    n++;
                }
            }

            if (tbl.ShowSlopes && segs != null)
            {
                foreach (var h in heights)
                {
                    double x = origin.X + h.Ch * lSc;
                    AddLine(ms, tr, x, yL, x, yS);
                }
                foreach (var s in segs)
                {
                    double xa = origin.X + s.Ch0 * lSc;
                    double xb = origin.X + s.Ch1 * lSc;
                    int i0 = 0, i1 = heights.Count - 1;
                    for (int i = 0; i < heights.Count; i++)
                    {
                        if (Math.Abs(heights[i].Ch - s.Ch0) < 1e-6) i0 = i;
                        if (Math.Abs(heights[i].Ch - s.Ch1) < 1e-6) i1 = i;
                    }
                    double dz = heights[i1].Z - heights[i0].Z;
                    double len = s.Ch1 - s.Ch0;
                    if (dz > 0.0)
                        AddLine(ms, tr, xb, yL, xa, yS);
                    else
                        AddLine(ms, tr, xa, yL, xb, yS);
                    string st = TableLayout.SlopeText(dz, len, tbl);
                    double sx = dz > 0.0 ? xa + off : xb - off;
                    var att = dz > 0.0 ? AttachmentPoint.TopLeft : AttachmentPoint.TopRight;
                    NvkText.Make(ms, tr, Layer, sx, yL - off, st, th, att, 0.0);
                    n++;
                }
            }
            return n;
        }

        static void AddLine(BlockTableRecord ms, Transaction tr, double x1, double y1, double x2, double y2)
        {
            var ln = new Line(new Point3d(x1, y1, 0.0), new Point3d(x2, y2, 0.0));
            ln.SetDatabaseDefaults();
            ln.Layer = Layer;
            ln.Color = AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
            ms.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
        }

        static AcadColor Aci(short i)
        {
            return AcadColor.FromColorIndex(ColorMethod.ByAci, i);
        }

        static void EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord();
            ltr.Name = name;
            ltr.Color = Aci(color);
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        static void EraseOld(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var erase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (string.Equals(ent.Layer, Layer, StringComparison.OrdinalIgnoreCase))
                    erase.Add(id);
            }
            foreach (var id in erase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                ent.Erase();
            }
        }
    }
}
