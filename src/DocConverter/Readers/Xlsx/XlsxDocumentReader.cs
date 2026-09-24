namespace DocConverter.Readers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;
    using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
    using A = DocumentFormat.OpenXml.Drawing;

    /// <summary>
    /// Reads XLSX workbooks into the document model: one sheet section per visible worksheet, each holding a table over
    /// the used range (with header row detection, merged cells as spans, dates as ISO 8601 and formulas as cached values)
    /// followed by the worksheet's images. Stateless and thread safe.
    /// </summary>
    public sealed class XlsxDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Xlsx };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            OfficeReadHelper.EnsureReadable(input, "XLSX", context);
            DocumentModel document = new DocumentModel();
            SpreadsheetDocument spreadsheet;
            try
            {
                spreadsheet = SpreadsheetDocument.Open(input, false);
            }
            catch (Exception ex) when (!(ex is DocConverterException) && !(ex is OperationCanceledException))
            {
                throw OfficeReadHelper.Wrap(ex, "XLSX");
            }

            using (spreadsheet)
            {
                WorkbookPart workbook = spreadsheet.WorkbookPart
                    ?? throw new DocumentReadException("The XLSX file has no workbook part.");
                if (workbook.Workbook == null) throw new DocumentReadException("The XLSX workbook part is empty.");

                OfficeReadHelper.ReadMetadata(spreadsheet, document);
                XlsxCellFormatter formatter = new XlsxCellFormatter(workbook);
                Dictionary<string, string> seenImages = new Dictionary<string, string>(StringComparer.Ordinal);

                Sheets? sheets = workbook.Workbook.Sheets;
                if (sheets == null) return Task.FromResult(document);

                foreach (Sheet sheet in sheets.Elements<Sheet>())
                {
                    token.ThrowIfCancellationRequested();
                    bool hidden = sheet.State != null && sheet.State.HasValue && sheet.State.Value != SheetStateValues.Visible;
                    if (hidden && !options.Xlsx.IncludeHiddenSheets) continue;
                    string name = sheet.Name?.Value ?? "Sheet";
                    string? relId = sheet.Id?.Value;
                    if (string.IsNullOrEmpty(relId)) continue;

                    WorksheetPart? part;
                    try
                    {
                        part = workbook.GetPartById(relId!) as WorksheetPart;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        part = null;
                    }

                    if (part == null || part.Worksheet == null) continue;

                    SectionBlock section = new SectionBlock(SectionKindEnum.Sheet, name);
                    section.SourceSheet = name;
                    ReadSheet(part, formatter, options, section, name, token);
                    ReadImages(part, document, section, name, seenImages);
                    document.Blocks.Add(section);
                }
            }

            return Task.FromResult(document);
        }

        private static void ReadSheet(WorksheetPart part, XlsxCellFormatter formatter, ConversionOptions options, SectionBlock section, string sheetName, CancellationToken token)
        {
            SheetData? data = part.Worksheet?.GetFirstChild<SheetData>();
            if (data == null) return;

            Dictionary<int, Dictionary<int, string>> grid = new Dictionary<int, Dictionary<int, string>>();
            int nextRow = 0;
            int batch = 0;
            foreach (Row row in data.Elements<Row>())
            {
                if (++batch % 256 == 0) token.ThrowIfCancellationRequested();
                int rowIndex = row.RowIndex != null && row.RowIndex.HasValue ? (int)row.RowIndex.Value - 1 : nextRow;
                nextRow = rowIndex + 1;
                int nextCol = 0;
                foreach (Cell cell in row.Elements<Cell>())
                {
                    int col = SpreadsheetCellReference.ColumnIndex(cell.CellReference?.Value);
                    if (col < 0) col = nextCol;
                    nextCol = col + 1;
                    string text = formatter.Text(cell);
                    if (text.Length == 0) continue;
                    if (!grid.TryGetValue(rowIndex, out Dictionary<int, string>? cells))
                    {
                        cells = new Dictionary<int, string>();
                        grid[rowIndex] = cells;
                    }

                    cells[col] = text;
                }
            }

            if (grid.Count == 0) return;

            int minRow = grid.Keys.Min();
            int maxRow = grid.Keys.Max();
            int minCol = grid.Values.SelectMany(r => r.Keys).Min();
            int maxCol = grid.Values.SelectMany(r => r.Keys).Max();

            List<MergeRange> merges = ReadMerges(part);
            foreach (MergeRange m in merges)
            {
                if (m.Top < minRow || m.Left < minCol) continue;
                if (m.Bottom > maxRow) maxRow = m.Bottom;
                if (m.Right > maxCol) maxCol = m.Right;
            }

            int width = maxCol - minCol + 1;
            List<List<string>> rows = new List<List<string>>();
            for (int r = minRow; r <= maxRow; r++)
            {
                List<string> values = new List<string>();
                grid.TryGetValue(r, out Dictionary<int, string>? cells);
                for (int c = minCol; c <= maxCol; c++)
                    values.Add(cells != null && cells.TryGetValue(c, out string? v) ? v : "");
                rows.Add(values);
            }

            int tableStart = 0;
            while (tableStart < rows.Count - 1 && NonEmpty(rows[tableStart]) == 1 && NonEmpty(rows[tableStart + 1]) >= 2 && width >= 2)
            {
                ParagraphBlock title = new ParagraphBlock(rows[tableStart].First(v => v.Length > 0));
                title.SourceSheet = sheetName;
                section.Blocks.Add(title);
                tableStart++;
            }

            List<List<string>> tableRows = rows.GetRange(tableStart, rows.Count - tableStart);
            bool header = options.Xlsx.DetectHeaderRow && XlsxHeaderRowDetector.IsHeaderRow(tableRows);

            HashSet<long> covered = new HashSet<long>();
            Dictionary<long, MergeRange> anchors = new Dictionary<long, MergeRange>();
            foreach (MergeRange m in merges)
            {
                anchors[Key(m.Top, m.Left)] = m;
                for (int r = m.Top; r <= m.Bottom; r++)
                    for (int c = m.Left; c <= m.Right; c++)
                        if (r != m.Top || c != m.Left) covered.Add(Key(r, c));
            }

            TableBlock table = new TableBlock();
            table.SourceSheet = sheetName;
            table.HeaderRowCount = header ? 1 : 0;
            for (int i = 0; i < tableRows.Count; i++)
            {
                int sheetRow = minRow + tableStart + i;
                TableRow row = new TableRow();
                for (int j = 0; j < width; j++)
                {
                    int sheetCol = minCol + j;
                    if (covered.Contains(Key(sheetRow, sheetCol))) continue;
                    TableCell cell = new TableCell(tableRows[i][j]);
                    cell.IsHeader = header && i == 0;
                    if (anchors.TryGetValue(Key(sheetRow, sheetCol), out MergeRange? m))
                    {
                        cell.ColumnSpan = Math.Min(m.Right, maxCol) - m.Left + 1;
                        cell.RowSpan = Math.Min(m.Bottom, maxRow) - m.Top + 1;
                    }

                    row.Cells.Add(cell);
                }

                table.Rows.Add(row);
            }

            if (table.Rows.Count > 0) section.Blocks.Add(table);
        }

        private static void ReadImages(WorksheetPart part, DocumentModel document, SectionBlock section, string sheetName, Dictionary<string, string> seen)
        {
            DrawingsPart? drawings = part.DrawingsPart;
            if (drawings == null || drawings.WorksheetDrawing == null) return;

            foreach (Xdr.Picture picture in drawings.WorksheetDrawing.Descendants<Xdr.Picture>())
            {
                A.Blip? blip = picture.BlipFill?.Blip;
                string? embed = blip?.Embed?.Value;
                if (string.IsNullOrEmpty(embed)) continue;
                OpenXmlPart? imagePart;
                try
                {
                    imagePart = drawings.GetPartById(embed!);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                string? id = OfficeReadHelper.AddImage(document, imagePart, seen);
                if (id == null) continue;
                Xdr.NonVisualDrawingProperties? nv = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
                string? alt = nv?.Description?.Value;
                if (string.IsNullOrEmpty(alt)) alt = nv?.Name?.Value;
                ImageBlock image = new ImageBlock(id, alt);
                image.SourceSheet = sheetName;
                section.Blocks.Add(image);
            }
        }

        private static List<MergeRange> ReadMerges(WorksheetPart part)
        {
            List<MergeRange> list = new List<MergeRange>();
            MergeCells? merges = part.Worksheet?.Elements<MergeCells>().FirstOrDefault();
            if (merges == null) return list;
            foreach (MergeCell mc in merges.Elements<MergeCell>())
            {
                string? reference = mc.Reference?.Value;
                if (string.IsNullOrEmpty(reference)) continue;
                string[] parts = reference!.Split(':');
                if (parts.Length != 2) continue;
                int top = SpreadsheetCellReference.RowIndex(parts[0]);
                int left = SpreadsheetCellReference.ColumnIndex(parts[0]);
                int bottom = SpreadsheetCellReference.RowIndex(parts[1]);
                int right = SpreadsheetCellReference.ColumnIndex(parts[1]);
                if (top < 0 || left < 0 || bottom < top || right < left) continue;
                if (top == bottom && left == right) continue;
                list.Add(new MergeRange(top, left, bottom, right));
            }

            return list;
        }

        private static int NonEmpty(List<string> row)
        {
            int n = 0;
            foreach (string v in row) if (v.Length > 0) n++;
            return n;
        }

        private static long Key(int row, int col)
        {
            return ((long)row << 20) | (uint)col;
        }
    }
}
