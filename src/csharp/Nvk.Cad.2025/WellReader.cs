using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Nvk.Core;
using CivilDb = Autodesk.Civil.DatabaseServices;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class WellReader
    {
        public static WellPitInput Read(Transaction tr, CivilDb.Structure st)
        {
            if (st == null) return null;
            Point3d pos = Point3d.Origin;
            try { pos = st.Position; }
            catch (Exc)
            {
                pos = Safe.Prop(st, "Location") is Point3d p ? p : Point3d.Origin;
            }
            double diam = Safe.PropDouble(st, "StructureInnerDiameterOrWidth", 0.0);
            if (diam <= 0.0) diam = Safe.PropDouble(st, "InnerDiameterOrWidth", 0.0);
            if (diam <= 0.0) diam = Safe.PropDouble(st, "InnerDiameter", 0.0);
            if (diam <= 0.0) diam = Safe.PropDouble(st, "Diameter", 0.0);
            if (diam <= 0.0) diam = Safe.PropDouble(st, "BoundingShapeWidth", 0.0);
            if (diam > 0.0) diam = Safe.Meters(diam);
            if (diam < 0.10) diam = 1.0;

            double rim = Safe.PropDouble(st, "RimElevation", pos.Z);
            double sump = Safe.PropDouble(st, "SumpElevation", 0.0);
            if (sump < 1.0 || sump > 10000.0)
            {
                double depth = Safe.PropDouble(st, "SumpDepth", 0.0);
                if (rim > 1.0 && depth > 0.0) sump = rim - Safe.Meters(depth);
                else sump = pos.Z;
            }
            double height = rim - sump;
            if (height < 0.05)
            {
                double h = Safe.PropDouble(st, "Height", 0.0);
                if (h <= 0.0) h = Safe.PropDouble(st, "StructureHeight", 0.0);
                if (h > 0.0)
                {
                    height = Safe.Meters(h);
                    if (height > 0.05 && sump >= rim) sump = rim - height;
                }
            }
            if (height < 0.05) height = 1.0;

            var well = new WellPitInput
            {
                Name = st.Name,
                Handle = st.Handle.ToString(),
                X = pos.X,
                Y = pos.Y,
                Rim = rim,
                Sump = sump,
                Diam = diam,
                Height = height
            };
            FillAxis(tr, st, well);
            return well;
        }

        static void FillAxis(Transaction tr, CivilDb.Structure st, WellPitInput well)
        {
            well.AxisX = 1.0;
            well.AxisY = 0.0;
            well.Pipes = new List<WellPipeSeg>();
            var ox = new List<double>();
            var oy = new List<double>();
            double maxDiam = 0.0;
            foreach (var pipe in ConnectedPipes(tr, st, well))
            {
                double vx, vy;
                if (!Outgoing(tr, pipe, well, out vx, out vy)) continue;
                ox.Add(vx);
                oy.Add(vy);
                double d = pipe.InnerDiameterOrWidth;
                if (d <= 0.0) d = pipe.OuterDiameterOrWidth;
                d = Safe.Meters(d);
                if (d > maxDiam) maxDiam = d;
                well.Pipes.Add(MakePipeSeg(tr, pipe, well, d));
            }
            well.PipeCount = ox.Count;
            well.PipeDiam = maxDiam;
            if (ox.Count > 0)
            {
                double ax, ay;
                WellPitGeom.AxisFromOutgoing(ox, oy, out ax, out ay);
                well.AxisX = ax;
                well.AxisY = ay;
                var az = new System.Text.StringBuilder();
                for (int i = 0; i < ox.Count; i++)
                {
                    double a = Math.Atan2(oy[i], ox[i]) * 180.0 / Math.PI;
                    if (a < 0.0) a += 360.0;
                    if (i > 0) az.Append(' ');
                    az.Append(a.ToString("0.0"));
                }
                NvkLog.Write("[НВК] колодец " + (well.Name ?? "?")
                    + "  трубы° " + az
                    + "  створ " + Deg(ax, ay).ToString("0.0"));
                return;
            }
            TryAxisFromChain(well);
        }

        static double Deg(double ax, double ay)
        {
            double a = Math.Atan2(ay, ax) * 180.0 / Math.PI;
            if (a < 0.0) a += 360.0;
            return a;
        }

        static WellPipeSeg MakePipeSeg(Transaction tr, CivilDb.Pipe pipe, WellPitInput well, double diam)
        {
            Point3d a = pipe.StartPoint;
            Point3d b = pipe.EndPoint;
            double d0 = Dist2(a.X, a.Y, well.X, well.Y);
            double d1 = Dist2(b.X, b.Y, well.X, well.Y);
            bool startNear = d0 <= d1;
            Point3d far = startNear ? b : a;
            ObjectId farId = startNear ? StructId(pipe, false) : StructId(pipe, true);
            if (!farId.IsNull && tr != null)
            {
                try
                {
                    var other = tr.GetObject(farId, OpenMode.ForRead) as CivilDb.Structure;
                    if (other != null) far = other.Position;
                }
                catch (Exc) { }
            }
            double siCtr = Safe.PropDouble(pipe, "StartCenterlineElevation", a.Z);
            double eiCtr = Safe.PropDouble(pipe, "EndCenterlineElevation", b.Z);
            double si = siCtr - diam * 0.5;
            double ei = eiCtr - diam * 0.5;
            return new WellPipeSeg
            {
                X0 = well.X,
                Y0 = well.Y,
                X1 = far.X,
                Y1 = far.Y,
                Invert0 = startNear ? si : ei,
                Invert1 = startNear ? ei : si,
                Diam = diam > 0.05 ? diam : 0.20
            };
        }

        /// <summary>Вектор от колодца вдоль трубы к дальнему концу / соседнему колодцу.</summary>
        static bool Outgoing(Transaction tr, CivilDb.Pipe pipe, WellPitInput well, out double vx, out double vy)
        {
            vx = 0.0;
            vy = 0.0;
            Point3d a = pipe.StartPoint;
            Point3d b = pipe.EndPoint;
            double d0 = Dist2(a.X, a.Y, well.X, well.Y);
            double d1 = Dist2(b.X, b.Y, well.X, well.Y);
            Point3d near = d0 <= d1 ? a : b;
            Point3d far = d0 <= d1 ? b : a;
            ObjectId farId = d0 <= d1
                ? StructId(pipe, false)
                : StructId(pipe, true);
            if (!farId.IsNull && tr != null)
            {
                try
                {
                    var other = tr.GetObject(farId, OpenMode.ForRead) as CivilDb.Structure;
                    if (other != null)
                    {
                        Point3d p = other.Position;
                        far = p;
                    }
                }
                catch (Exc) { }
            }
            vx = far.X - well.X;
            vy = far.Y - well.Y;
            if (vx * vx + vy * vy < 1e-6)
            {
                vx = far.X - near.X;
                vy = far.Y - near.Y;
            }
            return vx * vx + vy * vy > 1e-8;
        }

        static ObjectId StructId(CivilDb.Pipe pipe, bool start)
        {
            ObjectId id = ObjectId.Null;
            string idProp = start ? "StartStructureId" : "EndStructureId";
            object rawId = Safe.Prop(pipe, idProp);
            if (rawId is ObjectId oid) id = oid;
            if (id.IsNull)
            {
                object raw = Safe.Prop(pipe, start ? "StartStructure" : "EndStructure");
                if (raw is ObjectId oid2) id = oid2;
                else if (raw is CivilDb.Structure st) id = st.ObjectId;
            }
            return id;
        }

        static bool TryAxisFromChain(WellPitInput well)
        {
            var last = NvkSession.Last;
            if (last == null || last.Chain == null) return false;
            double dist;
            double ch = last.Chain.Project(well.X, well.Y, out dist);
            double lim = WellPitGeom.Radius(well) + 2.0;
            if (dist > lim) return false;
            var hit = last.Chain.Locate(ch);
            double ax, ay;
            WellPitGeom.TangentFromNormal(hit.Nx, hit.Ny, out ax, out ay);
            well.AxisX = ax;
            well.AxisY = ay;
            if (hit.Diam > 0.05) well.PipeDiam = hit.Diam;
            well.PipeCount = Math.Max(well.PipeCount, 1);
            return true;
        }

        static List<CivilDb.Pipe> ConnectedPipes(Transaction tr, CivilDb.Structure st, WellPitInput well)
        {
            var list = new List<CivilDb.Pipe>();
            if (tr == null || st == null) return list;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in PipeIdsOf(tr, st))
                AddIfConnected(tr, st, well, id, list, seen);
            if (list.Count > 0) return list;
            if (NvkSession.LastPipes != null)
            {
                foreach (var id in NvkSession.LastPipes)
                    AddIfConnected(tr, st, well, id, list, seen);
            }
            if (list.Count > 0) return list;
            try
            {
                var civil = CivilApplication.ActiveDocument;
                if (civil == null) return list;
                var nets = civil.GetPipeNetworkIds();
                if (nets == null) return list;
                foreach (ObjectId nid in nets)
                {
                    var net = tr.GetObject(nid, OpenMode.ForRead) as CivilDb.Network;
                    if (net == null) continue;
                    ObjectIdCollection pipes = null;
                    try { pipes = net.GetPipeIds(); } catch (Exc) { }
                    if (pipes == null) continue;
                    foreach (ObjectId pid in pipes)
                        AddIfConnected(tr, st, well, pid, list, seen);
                }
            }
            catch (Exc) { }
            return list;
        }

        static IEnumerable<ObjectId> PipeIdsOf(Transaction tr, CivilDb.Structure st)
        {
            var ids = new List<ObjectId>();
            CollectIds(Safe.Prop(st, "ConnectedPipes"), ids);
            CollectIds(Safe.Prop(st, "ConnectedPipeIds"), ids);
            try
            {
                foreach (var mi in st.GetType().GetMethods())
                {
                    if (mi.GetParameters().Length != 0) continue;
                    string n = mi.Name ?? "";
                    if (n.IndexOf("ConnectedPipe", StringComparison.OrdinalIgnoreCase) < 0
                        && n != "GetPipeIds")
                        continue;
                    CollectIds(mi.Invoke(st, null), ids);
                }
            }
            catch (Exc) { }
            ObjectId nid = ObjectId.Null;
            try { nid = st.NetworkId; } catch (Exc) { }
            if (nid.IsNull)
            {
                object raw = Safe.Prop(st, "NetworkId");
                if (raw is ObjectId oid) nid = oid;
            }
            if (!nid.IsNull)
            {
                try
                {
                    var net = tr.GetObject(nid, OpenMode.ForRead) as CivilDb.Network;
                    if (net != null)
                    {
                        ObjectIdCollection pipes = null;
                        try { pipes = net.GetPipeIds(); } catch (Exc) { }
                        if (pipes != null)
                        {
                            foreach (ObjectId pid in pipes)
                                ids.Add(pid);
                        }
                    }
                }
                catch (Exc) { }
            }
            return ids;
        }

        static void CollectIds(object raw, List<ObjectId> ids)
        {
            if (raw == null || ids == null) return;
            if (raw is ObjectIdCollection col)
            {
                foreach (ObjectId id in col)
                    if (!id.IsNull) ids.Add(id);
                return;
            }
            if (raw is ObjectId one && !one.IsNull)
            {
                ids.Add(one);
                return;
            }
            var en = raw as System.Collections.IEnumerable;
            if (en == null || raw is string) return;
            foreach (var it in en)
            {
                if (it is ObjectId id && !id.IsNull) ids.Add(id);
            }
        }

        static void AddIfConnected(Transaction tr, CivilDb.Structure st, WellPitInput well,
            ObjectId pid, List<CivilDb.Pipe> list, HashSet<string> seen)
        {
            if (pid.IsNull || tr == null) return;
            string key = pid.Handle.ToString();
            if (!seen.Add(key)) return;
            CivilDb.Pipe pipe = null;
            try { pipe = tr.GetObject(pid, OpenMode.ForRead) as CivilDb.Pipe; }
            catch (Exc) { }
            if (pipe == null) return;
            if (!PipeHitsWell(tr, pipe, st, well)) return;
            list.Add(pipe);
        }

        static bool PipeHitsWell(Transaction tr, CivilDb.Pipe pipe, CivilDb.Structure st, WellPitInput well)
        {
            ObjectId wellId = st.ObjectId;
            string wellH = well.Handle ?? st.Handle.ToString();
            if (StructIs(tr, pipe, true, wellId, wellH)) return true;
            if (StructIs(tr, pipe, false, wellId, wellH)) return true;
            double r = WellPitGeom.Radius(well) + 0.60;
            Point3d a = pipe.StartPoint;
            Point3d b = pipe.EndPoint;
            double d0 = Dist2(a.X, a.Y, well.X, well.Y);
            double d1 = Dist2(b.X, b.Y, well.X, well.Y);
            return d0 <= r * r || d1 <= r * r;
        }

        static bool StructIs(Transaction tr, CivilDb.Pipe pipe, bool start, ObjectId wellId, string wellH)
        {
            ObjectId id = ObjectId.Null;
            string idProp = start ? "StartStructureId" : "EndStructureId";
            string stProp = start ? "StartStructure" : "EndStructure";
            object rawId = Safe.Prop(pipe, idProp);
            if (rawId is ObjectId oid) id = oid;
            object raw = Safe.Prop(pipe, stProp);
            if (id.IsNull && raw is ObjectId oid2) id = oid2;
            if (id.IsNull && raw != null)
            {
                try
                {
                    var p = raw.GetType().GetProperty("ObjectId");
                    if (p != null) id = (ObjectId)p.GetValue(raw, null);
                }
                catch { }
            }
            if (!id.IsNull && id == wellId) return true;
            CivilDb.Structure other = null;
            if (!id.IsNull)
            {
                try { other = tr.GetObject(id, OpenMode.ForRead) as CivilDb.Structure; }
                catch (Exc) { }
            }
            if (other == null && raw is CivilDb.Structure st2) other = st2;
            if (other == null) return false;
            return string.Equals(other.Handle.ToString(), wellH, StringComparison.OrdinalIgnoreCase)
                || other.ObjectId == wellId;
        }

        static double Dist2(double x0, double y0, double x1, double y1)
        {
            double dx = x0 - x1, dy = y0 - y1;
            return dx * dx + dy * dy;
        }
    }
}
