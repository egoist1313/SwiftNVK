using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Nvk.Core;
using Exc = System.Exception;

namespace Nvk.Cad
{
    static class CadTable
    {
        public static Table Make(BlockTableRecord ms, Transaction tr, Point3d origin,
            string layer, int rows, int cols, double textH)
        {
            var tbl = new Table();
            tbl.SetDatabaseDefaults();
            tbl.Layer = layer;
            tbl.Position = origin;
            tbl.TableStyle = ms.Database.Tablestyle;
            tbl.SetSize(rows < 1 ? 1 : rows, cols < 1 ? 1 : cols);
            tbl.SetRowHeight(textH * 2.2);
            for (int c = 0; c < cols; c++)
                tbl.Columns[c].Width = textH * 12.0;
            ms.AppendEntity(tbl);
            tr.AddNewlyCreatedDBObject(tbl, true);
            return tbl;
        }

        public static void Cell(Table tbl, int r, int c, string text, double textH, bool header)
        {
            try
            {
                var cell = tbl.Cells[r, c];
                cell.TextString = text ?? "";
                cell.TextHeight = textH;
                cell.Alignment = CellAlignment.MiddleCenter;
            }
            catch (Exc) { }
        }

        public static void ColW(Table tbl, int c, double w)
        {
            try { tbl.Columns[c].Width = w; } catch (Exc) { }
        }

        public static void Finish(Table tbl)
        {
            try { tbl.GenerateLayout(); } catch (Exc) { }
        }

        /// <summary>
        /// Координатная таблица: заголовок на всю ширину, затем № / X / Y.
        /// Ширина колонок от высоты текста и длины строк.
        /// </summary>
        public static Table Coord(BlockTableRecord ms, Transaction tr, Point3d origin,
            string layer, string title, IList<string[]> body, double textH)
        {
            int n = body != null ? body.Count : 0;
            var tbl = Make(ms, tr, origin, layer, n + 2, 3, textH);
            try { tbl.MergeCells(CellRange.Create(tbl, 0, 0, 0, 2)); } catch (Exc) { }
            Cell(tbl, 0, 0, title ?? "", textH, true);
            try { tbl.Cells[0, 0].Alignment = CellAlignment.MiddleCenter; } catch (Exc) { }
            Cell(tbl, 1, 0, TableLayout.CoordNo, textH, true);
            Cell(tbl, 1, 1, "X", textH, true);
            Cell(tbl, 1, 2, "Y", textH, true);
            var c0 = new List<string> { TableLayout.CoordNo };
            var c1 = new List<string> { "X" };
            var c2 = new List<string> { "Y" };
            for (int i = 0; i < n; i++)
            {
                string a = body[i] != null && body[i].Length > 0 ? body[i][0] : "";
                string b = body[i] != null && body[i].Length > 1 ? body[i][1] : "";
                string c = body[i] != null && body[i].Length > 2 ? body[i][2] : "";
                Cell(tbl, i + 2, 0, a, textH, false);
                Cell(tbl, i + 2, 1, b, textH, false);
                Cell(tbl, i + 2, 2, c, textH, false);
                c0.Add(a);
                c1.Add(b);
                c2.Add(c);
            }
            double w0 = TableLayout.TextColW(textH, c0);
            double w1 = TableLayout.TextColW(textH, c1);
            double w2 = TableLayout.TextColW(textH, c2);
            double titleW = TableLayout.TextColW(textH, new List<string> { title ?? "" });
            double extra = titleW - (w0 + w1 + w2);
            if (extra > 0.0) w0 += extra;
            ColW(tbl, 0, w0);
            ColW(tbl, 1, w1);
            ColW(tbl, 2, w2);
            try { tbl.Rows[0].Height = textH * 4.0; } catch (Exc) { }
            Finish(tbl);
            return tbl;
        }
    }
}
