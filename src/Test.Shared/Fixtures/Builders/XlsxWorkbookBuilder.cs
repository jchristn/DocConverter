namespace Test.Shared.Fixtures.Builders
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;
    using A = DocumentFormat.OpenXml.Drawing;
    using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

    /// <summary>
    /// Minimal workbook writer used by the builders.
    /// </summary>
    public sealed class XlsxWorkbookBuilder
    {
        private readonly SpreadsheetDocument _Doc;
        private readonly WorkbookPart _Workbook;
        private readonly Sheets _Sheets = new Sheets();
        private readonly List<string> _Strings = new List<string>();
        private uint _NextId = 1;

        /// <summary>Instantiate.</summary>
        /// <param name="doc">Document.</param>
        public XlsxWorkbookBuilder(SpreadsheetDocument doc)
        {
            _Doc = doc;
            _Workbook = doc.AddWorkbookPart();
            _Workbook.Workbook = new Workbook();
            WorkbookStylesPart styles = _Workbook.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = new Stylesheet(
                new Fonts(new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" })) { Count = 1 },
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
                new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
                new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
                new CellFormats(
                    new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                    new CellFormat { NumberFormatId = 14, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, ApplyNumberFormat = true })
                { Count = 2 });
        }

        /// <summary>Add a sheet.</summary>
        /// <param name="name">Sheet name.</param>
        /// <param name="rows">Rows of values (string, int, double, bool, XlsxDateCell, XlsxFormulaCell or null).</param>
        /// <param name="hidden">True for a hidden sheet.</param>
        /// <param name="merges">Merge references, or null.</param>
        /// <param name="image">PNG bytes to place in a drawing, or null.</param>
        public void AddSheet(string name, List<object?[]> rows, bool hidden, string[]? merges, byte[]? image = null)
        {
            WorksheetPart part = _Workbook.AddNewPart<WorksheetPart>();
            SheetData data = new SheetData();
            for (int r = 0; r < rows.Count; r++)
            {
                Row row = new Row { RowIndex = (uint)(r + 1) };
                for (int c = 0; c < rows[r].Length; c++)
                {
                    object? value = rows[r][c];
                    if (value == null) continue;
                    string reference = ColumnName(c) + (r + 1).ToString(CultureInfo.InvariantCulture);
                    Cell cell = new Cell { CellReference = reference };
                    if (value is string s)
                    {
                        cell.DataType = CellValues.SharedString;
                        cell.CellValue = new CellValue(StringIndex(s).ToString(CultureInfo.InvariantCulture));
                    }
                    else if (value is bool b)
                    {
                        cell.DataType = CellValues.Boolean;
                        cell.CellValue = new CellValue(b ? "1" : "0");
                    }
                    else if (value is int i)
                    {
                        cell.CellValue = new CellValue(i.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (value is double d)
                    {
                        cell.CellValue = new CellValue(d.ToString("R", CultureInfo.InvariantCulture));
                    }
                    else if (value is XlsxDateCell date)
                    {
                        cell.CellValue = new CellValue(date.Serial.ToString("R", CultureInfo.InvariantCulture));
                        cell.StyleIndex = 1;
                    }
                    else if (value is XlsxFormulaCell formula)
                    {
                        cell.CellFormula = new CellFormula(formula.Formula);
                        cell.CellValue = new CellValue(formula.Cached);
                    }

                    row.Append(cell);
                }

                data.Append(row);
            }

            Worksheet sheet = new Worksheet(data);
            if (merges != null && merges.Length > 0)
            {
                MergeCells mc = new MergeCells { Count = (uint)merges.Length };
                foreach (string m in merges) mc.Append(new MergeCell { Reference = m });
                sheet.Append(mc);
            }

            if (image != null)
            {
                DrawingsPart drawings = part.AddNewPart<DrawingsPart>();
                ImagePart imagePart = drawings.AddImagePart(ImagePartType.Png);
                using (MemoryStream ms = new MemoryStream(image)) imagePart.FeedData(ms);
                string embed = drawings.GetIdOfPart(imagePart);
                drawings.WorksheetDrawing = new Xdr.WorksheetDrawing(
                    new Xdr.TwoCellAnchor(
                        new Xdr.FromMarker(new Xdr.ColumnId("3"), new Xdr.ColumnOffset("0"), new Xdr.RowId("1"), new Xdr.RowOffset("0")),
                        new Xdr.ToMarker(new Xdr.ColumnId("5"), new Xdr.ColumnOffset("0"), new Xdr.RowId("5"), new Xdr.RowOffset("0")),
                        new Xdr.Picture(
                            new Xdr.NonVisualPictureProperties(
                                new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Picture 1", Description = ReferenceContent.ImageAlt },
                                new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
                            new Xdr.BlipFill(new A.Blip { Embed = embed }, new A.Stretch(new A.FillRectangle())),
                            new Xdr.ShapeProperties(new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = 152400, Cy = 152400 }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })),
                        new Xdr.ClientData()));
                sheet.Append(new Drawing { Id = part.GetIdOfPart(drawings) });
            }

            part.Worksheet = sheet;
            Sheet entry = new Sheet { Id = _Workbook.GetIdOfPart(part), SheetId = _NextId++, Name = name };
            if (hidden) entry.State = SheetStateValues.Hidden;
            _Sheets.Append(entry);
        }

        /// <summary>Write the workbook parts.</summary>
        public void Finish()
        {
            _Workbook.Workbook!.Append(_Sheets);
            if (_Strings.Count > 0)
            {
                SharedStringTablePart sst = _Workbook.AddNewPart<SharedStringTablePart>();
                SharedStringTable table = new SharedStringTable { Count = (uint)_Strings.Count, UniqueCount = (uint)_Strings.Count };
                foreach (string s in _Strings) table.Append(new SharedStringItem(new Text(s)));
                sst.SharedStringTable = table;
            }

            CoreFilePropertiesPart core = _Doc.AddCoreFilePropertiesPart();
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<dc:title>" + ReferenceContent.Title + "</dc:title><dc:creator>" + ReferenceContent.Author + "</dc:creator>"
                + "<dcterms:created xsi:type=\"dcterms:W3CDTF\">2024-01-02T03:04:05Z</dcterms:created></cp:coreProperties>";
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(xml))) core.FeedData(ms);
        }

        private int StringIndex(string s)
        {
            int index = _Strings.IndexOf(s);
            if (index >= 0) return index;
            _Strings.Add(s);
            return _Strings.Count - 1;
        }

        private static string ColumnName(int index)
        {
            StringBuilder sb = new StringBuilder();
            int n = index + 1;
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                n = (n - 1) / 26;
            }

            return sb.ToString();
        }
    }
}
