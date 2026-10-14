using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Nvk.Core;
using CivilDb = Autodesk.Civil.DatabaseServices;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class PipeReader
    {
        public static PipeSeg Read(Transaction tr, CivilDb.Pipe pipe)
        {
            Point3d p0 = pipe.StartPoint;
            Point3d p1 = pipe.EndPoint;

            double diam = pipe.InnerDiameterOrWidth;
            if (diam <= 0.0) diam = pipe.OuterDiameterOrWidth;
            diam = Safe.Meters(diam);
            if (diam <= 0.0) diam = 0.3;

            double siCtr = Safe.PropDouble(pipe, "StartCenterlineElevation", p0.Z);
            double eiCtr = Safe.PropDouble(pipe, "EndCenterlineElevation", p1.Z);
            double si = siCtr - diam * 0.5;
            double ei = eiCtr - diam * 0.5;

            string ssName = null, esName = null;
            double ssR = 0.0, esR = 0.0, ssSump = si, esSump = ei;
            ReadStruct(tr, pipe, true, si, diam, out ssName, out ssR, out ssSump);
            ReadStruct(tr, pipe, false, ei, diam, out esName, out esR, out esSump);

            return new PipeSeg
            {
                Handle = pipe.Handle.ToString(),
                X0 = p0.X, Y0 = p0.Y, X1 = p1.X, Y1 = p1.Y,
                InvertStart = si, InvertEnd = ei, Diam = diam,
                StartName = ssName, EndName = esName,
                StartR = ssR, EndR = esR,
                StartSump = ssSump, EndSump = esSump
            };
        }

        static void ReadStruct(Transaction tr, CivilDb.Pipe pipe, bool start, double invert, double pipeDiam,
            out string name, out double r, out double sump)
        {
            name = null;
            r = 0.0;
            sump = invert;
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
            CivilDb.Structure st = null;
            if (!id.IsNull)
            {
                try { st = tr.GetObject(id, OpenMode.ForRead) as CivilDb.Structure; }
                catch (Exc) { }
            }
            if (st == null && raw is CivilDb.Structure st2) st = st2;
            if (st == null) return;
            name = st.Name;
            double dv = Safe.PropDouble(st, "StructureInnerDiameterOrWidth", 0.0);
            if (dv <= 0.0) dv = Safe.PropDouble(st, "InnerDiameterOrWidth", 0.0);
            if (dv <= 0.0) dv = Safe.PropDouble(st, "InnerDiameter", 0.0);
            if (dv <= 0.0) dv = Safe.PropDouble(st, "Diameter", 0.0);
            if (dv <= 0.0) dv = Safe.PropDouble(st, "BoundingShapeWidth", 0.0);
            if (dv > 0.0)
            {
                dv = Safe.Meters(dv);
                r = dv * 0.5;
            }
            if (r < 0.05)
                r = Math.Max(0.5, pipeDiam > 0 ? pipeDiam : 0.5);
            double sv = Safe.PropDouble(st, "SumpElevation", invert);
            if (sv > 1.0 && sv < 10000.0) sump = sv;
            else
            {
                double rim = Safe.PropDouble(st, "RimElevation", 0.0);
                double depth = Safe.PropDouble(st, "SumpDepth", 0.0);
                if (rim > 1.0 && depth > 0.0) sump = rim - Safe.Meters(depth);
            }
        }

        public static string NetworkName(Transaction tr, CivilDb.Pipe pipe)
        {
            if (pipe == null) return null;
            try
            {
                string n = pipe.NetworkName;
                if (!string.IsNullOrWhiteSpace(n)) return n.Trim();
            }
            catch (Exc) { }
            string via = Safe.PropString(pipe, "NetworkName");
            if (!string.IsNullOrWhiteSpace(via)) return via.Trim();
            try
            {
                ObjectId nid = pipe.NetworkId;
                if (!nid.IsNull && tr != null)
                {
                    var net = tr.GetObject(nid, OpenMode.ForRead) as CivilDb.Network;
                    if (net != null && !string.IsNullOrWhiteSpace(net.Name))
                        return net.Name.Trim();
                    string n = Safe.PropString(tr.GetObject(nid, OpenMode.ForRead), "Name");
                    if (!string.IsNullOrWhiteSpace(n)) return n.Trim();
                }
            }
            catch (Exc) { }
            return FindByPipeId(tr, pipe);
        }

        static string FindByPipeId(Transaction tr, CivilDb.Pipe pipe)
        {
            if (tr == null || pipe == null) return null;
            try
            {
                var civil = CivilApplication.ActiveDocument;
                if (civil == null) return null;
                var ids = civil.GetPipeNetworkIds();
                if (ids == null) return null;
                ObjectId pid = pipe.ObjectId;
                foreach (ObjectId nid in ids)
                {
                    var net = tr.GetObject(nid, OpenMode.ForRead) as CivilDb.Network;
                    if (net == null) continue;
                    ObjectIdCollection pipes = null;
                    try { pipes = net.GetPipeIds(); } catch (Exc) { }
                    if (pipes == null) continue;
                    foreach (ObjectId id in pipes)
                    {
                        if (id == pid && !string.IsNullOrWhiteSpace(net.Name))
                            return net.Name.Trim();
                    }
                }
            }
            catch (Exc) { }
            return null;
        }
    }
}
