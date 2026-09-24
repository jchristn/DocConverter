namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// Inspects XLSX output with the OpenXml SDK (never DocConverter): validates it, then reports sheet names as headings,
    /// every sheet's rows as table rows and all cell text.
    /// </summary>
    public static class XlsxInspector
    {
        /// <summary>
        /// Inspect XLSX bytes.
        /// </summary>
        /// <param name="bytes">XLSX bytes.</param>
        /// <returns>Snapshot.</returns>
        /// <exception cref="TestAssertionException">Thrown when the package fails validation.</exception>
        public static ContentSnapshot Inspect(byte[] bytes)
        {
            ContentSnapshot snapshot = new ContentSnapshot();
            using (MemoryStream ms = new MemoryStream(bytes))
            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(ms, false))
            {
                OpenXmlInspection.Validate(doc, "XLSX");
                snapshot.Title = OpenXmlInspection.Title(doc);
                WorkbookPart workbook = doc.WorkbookPart ?? throw new TestAssertionException("XLSX output has no workbook part.");
                List<string> strings = new List<string>();
                if (workbook.SharedStringTablePart?.SharedStringTable != null)
                    foreach (SharedStringItem item in workbook.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>()) strings.Add(item.InnerText);

                foreach (Sheet sheet in workbook.Workbook!.Sheets!.Elements<Sheet>())
                {
                    snapshot.Headings.Add(sheet.Name?.Value ?? "");
                    WorksheetPart part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
                    SheetData? data = part.Worksheet?.GetFirstChild<SheetData>();
                    if (data == null) continue;
                    foreach (Row row in data.Elements<Row>())
                    {
                        List<string> cells = new List<string>();
                        foreach (Cell cell in row.Elements<Cell>())
                        {
                            int col = Column(cell.CellReference?.Value);
                            while (cells.Count < col) cells.Add("");
                            string text = Text(cell, strings);
                            cells.Add(text);
                            OpenXmlInspection.AppendText(snapshot, text);
                        }

                        snapshot.TableRows.Add(cells);
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Raw cell element for a sheet and reference, for type assertions. Null when missing.
        /// </summary>
        /// <param name="bytes">XLSX bytes.</param>
        /// <param name="sheetName">Sheet name.</param>
        /// <param name="reference">Cell reference, for example "C2".</param>
        /// <returns>Cell data type name ("s", "b", "n") and value joined by a colon, or null.</returns>
        public static string? CellTypeAndValue(byte[] bytes, string sheetName, string reference)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(ms, false))
            {
                WorkbookPart workbook = doc.WorkbookPart!;
                Sheet? sheet = workbook.Workbook!.Sheets!.Elements<Sheet>().FirstOrDefault(s => s.Name?.Value == sheetName);
                if (sheet == null) return null;
                WorksheetPart part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
                Cell? cell = part.Worksheet!.Descendants<Cell>().FirstOrDefault(c => c.CellReference?.Value == reference);
                if (cell == null) return null;
                string type = "n";
                if (cell.DataType != null && cell.DataType.HasValue)
                {
                    if (cell.DataType.Value == CellValues.SharedString) type = "s";
                    else if (cell.DataType.Value == CellValues.Boolean) type = "b";
                    else if (cell.DataType.Value == CellValues.InlineString) type = "inlineStr";
                    else type = cell.DataType.Value.ToString();
                }

                string style = cell.StyleIndex != null ? cell.StyleIndex.Value.ToString(CultureInfo.InvariantCulture) : "0";
                return type + ":" + (cell.CellValue?.Text ?? "") + ":style" + style;
            }
        }

        private static string Text(Cell cell, List<string> strings)
        {
            string raw = cell.CellValue?.Text ?? "";
            if (cell.DataType != null && cell.DataType.HasValue)
            {
                if (cell.DataType.Value == CellValues.SharedString)
                {
                    int index = int.Parse(raw, CultureInfo.InvariantCulture);
                    return index >= 0 && index < strings.Count ? strings[index] : "";
                }

                if (cell.DataType.Value == CellValues.Boolean) return raw == "1" ? "TRUE" : "FALSE";
                if (cell.DataType.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
            }

            return raw;
        }

        private static int Column(string? reference)
        {
            if (string.IsNullOrEmpty(reference)) return 0;
            int index = 0;
            foreach (char c in reference!)
            {
                if (c < 'A' || c > 'Z') break;
                index = index * 26 + (c - 'A' + 1);
            }

            return Math.Max(0, index - 1);
        }
    }
}
