using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Exc = System.Exception;

namespace Nvk.Cad
{
    /// <summary>
    /// MTEXT как create-mtext (PoperSwift) и pc-make-mtext / pc-make-mtext-bg (PipeBuild).
    /// 72=LTR, стиль чертежа, ширина 0 = без рамки переноса, bg = DXF 90=1 / 45=1.1.
    /// </summary>
    static class NvkText
    {
        public static MText Make(BlockTableRecord btr, Transaction tr, string layer,
            double x, double y, string contents, double height,
            AttachmentPoint att, double rot)
        {
            return Make(btr, tr, layer, x, y, contents, height, att, rot, 0.0, false);
        }

        public static MText Make(BlockTableRecord btr, Transaction tr, string layer,
            double x, double y, string contents, double height,
            AttachmentPoint att, double rot, double width, bool bgFill)
        {
            var mt = new MText();
            mt.SetDatabaseDefaults();
            if (!string.IsNullOrEmpty(layer)) mt.Layer = layer;
            mt.Contents = contents ?? "";
            mt.Location = new Point3d(x, y, 0.0);
            double h = height > 0.0 ? height : 2.5;
            var tbl = NvkSession.Tbl();
            if (tbl != null && tbl.Annotative)
                h = tbl.Scaled(h, NvkSession.Cannoscale);
            mt.TextHeight = h;
            mt.Attachment = att;
            mt.Rotation = rot;
            mt.Width = width > 0.0 ? width : 0.0;
            try { mt.TextStyleId = btr.Database.Textstyle; } catch (Exc) { }
            if (bgFill)
            {
                try
                {
                    mt.BackgroundFill = true;
                    mt.UseBackgroundColor = true;
                    mt.BackgroundScaleFactor = 1.1;
                }
                catch (Exc) { }
            }
            btr.AppendEntity(mt);
            tr.AddNewlyCreatedDBObject(mt, true);
            try { mt.Annotative = AnnotativeStates.False; } catch (Exc) { }
            NvkAnno.EnsureVisible(mt);
            NvkTag.Stamp(tr, mt);
            return mt;
        }
    }
}
