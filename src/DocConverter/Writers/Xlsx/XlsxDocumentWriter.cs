namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Readers.Xlsx;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// Writes the document model as an XLSX workbook: one worksheet per table (sheet sections name them), non-table
    /// content on a leading "Document" sheet, typed cells, bold frozen header rows and merged cells for spans.
    /// Images are omitted. Stateless and thread safe.
    /// </summary>
    public sealed class XlsxDocumentWriter : IDocumentWriter
    {
        #region Public-Members

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        #endregion

        #region Private-Members

        private const uint _StyleBold = 1;
        private const uint _StyleDate = 2;
        private const uint _StyleDateTime = 3;
        private const int _MaxCellText = 32767;

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Xlsx };

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            CollectState state = new CollectState();
            state.Resources = document.Resources;
            List<XlsxSheetPlan> tables = new List<XlsxSheetPlan>();
            XlsxSheetPlan documentSheet = new XlsxSheetPlan("Document", null);
            Collect(document.Blocks, null, tables, documentSheet.Lines, state, 0);

            List<XlsxSheetPlan> plans = new List<XlsxSheetPlan>();
            if (documentSheet.Lines.Count > 0)
            {
                if (options.Xlsx.IncludeNonTableContent) plans.Add(documentSheet);
                else context.AddWarning(WarningCodeEnum.NonTableContentDropped, documentSheet.Lines.Count + " non-table block(s) were dropped because IncludeNonTableContent is false.");
            }

            plans.AddRange(tables);
            if (state.Images > 0)
                context.AddWarning(WarningCodeEnum.ImagesOmitted, state.Images + " inline or in-table image(s) were omitted because XLSX output does not carry images.");
            if (state.Placeholders > 0 && options.Xlsx.IncludeNonTableContent)
                context.AddWarning(WarningCodeEnum.ImagePlaceholderEmitted, state.Placeholders + " image(s) were written as placeholder rows because XLSX output does not carry images. No text was extracted from them (no OCR).");
            if (state.FormattingLost && (options.Xlsx.IncludeNonTableContent || tables.Count > 0))
                context.AddWarning(WarningCodeEnum.FormattingLost, "Inline styles and links were flattened to plain cell text.");
            if (plans.Count == 0) plans.Add(new XlsxSheetPlan("Sheet1", null));

            using (MemoryStream package = new MemoryStream())
            {
                using (SpreadsheetDocument spreadsheet = SpreadsheetDocument.Create(package, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbookPart = spreadsheet.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();
                    WorkbookStylesPart stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>("rIdStyles");
                    stylesPart.Stylesheet = BuildStylesheet();

                    SharedStrings strings = new SharedStrings();
                    Sheets sheets = new Sheets();
                    HashSet<string> used = XlsxSheetNames.NewSet();
                    uint sheetId = 1;
                    int tableNumber = 0;
                    foreach (XlsxSheetPlan plan in plans)
                    {
                        token.ThrowIfCancellationRequested();
                        string fallback = plan.Table != null ? "Table " + (++tableNumber) : "Sheet" + sheetId;
                        string name = XlsxSheetNames.MakeUnique(plan.RequestedName, fallback, used);
                        WorksheetPart sheetPart = workbookPart.AddNewPart<WorksheetPart>("rIdSheet" + sheetId);
                        sheetPart.Worksheet = plan.Table != null
                            ? BuildTableSheet(plan.Table, strings, options, context, token)
                            : BuildLinesSheet(plan.Lines, strings, context);
                        sheets.Append(new Sheet { Id = "rIdSheet" + sheetId, SheetId = sheetId, Name = name });
                        sheetId++;
                    }

                    workbookPart.Workbook.Append(sheets);
                    if (strings.Count > 0)
                    {
                        SharedStringTablePart sstPart = workbookPart.AddNewPart<SharedStringTablePart>("rIdStrings");
                        sstPart.SharedStringTable = strings.Build();
                    }

                    if (options.IncludeMetadata || options.Deterministic)
                        OfficeWriteHelper.WriteCoreProperties(spreadsheet, document.Metadata, options);
                }

                OfficeWriteHelper.CopyPackage(package, output, options.Deterministic);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Private-Methods

        private static void Collect(List<Block> blocks, string? sheetTitle, List<XlsxSheetPlan> tables, List<string> lines, CollectState state, int depth)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case SectionBlock section:
                        if (section.Kind == SectionKindEnum.Sheet)
                        {
                            Collect(section.Blocks, section.Title ?? sheetTitle, tables, lines, state, depth);
                        }
                        else
                        {
                            if (SectionTitles.ShouldRender(section)) lines.Add(section.Title!);
                            Collect(section.Blocks, sheetTitle, tables, lines, state, depth);
                        }

                        break;
                    case TableBlock table:
                        tables.Add(new XlsxSheetPlan(sheetTitle, table));
                        if (HasFormatting(table)) state.FormattingLost = true;
                        CountTableImages(table, state);
                        break;
                    case QuoteBlock quote:
                        Collect(quote.Blocks, sheetTitle, tables, lines, state, depth);
                        break;
                    case ListBlock list:
                        int number = list.Start;
                        foreach (ListItemBlock item in list.Items)
                        {
                            string prefix = new string(' ', depth * 2) + (list.Kind == ListKindEnum.Ordered ? number + ". " : "- ");
                            number++;
                            StringBuilder text = new StringBuilder();
                            List<Block> nested = new List<Block>();
                            foreach (Block child in item.Blocks)
                            {
                                if (child is ParagraphBlock || child is HeadingBlock || child is CodeBlock)
                                {
                                    if (text.Length > 0) text.Append(' ');
                                    text.Append(ModelText.Block(child));
                                    if (child is ParagraphBlock p && HasFormatting(p.Inlines)) state.FormattingLost = true;
                                    if (child is ParagraphBlock pi) CountInlineImages(pi.Inlines, state);
                                }
                                else
                                {
                                    nested.Add(child);
                                }
                            }

                            lines.Add(prefix + text.ToString());
                            Collect(nested, sheetTitle, tables, lines, state, depth + 1);
                        }

                        break;
                    case ListItemBlock looseItem:
                        Collect(looseItem.Blocks, sheetTitle, tables, lines, state, depth);
                        break;
                    case HeadingBlock heading:
                        lines.Add(ModelText.Inlines(heading.Inlines));
                        if (HasFormatting(heading.Inlines)) state.FormattingLost = true;
                        CountInlineImages(heading.Inlines, state);
                        break;
                    case ParagraphBlock paragraph:
                        string paragraphText = ModelText.Inlines(paragraph.Inlines);
                        if (paragraphText.Length > 0) lines.Add(paragraphText);
                        if (HasFormatting(paragraph.Inlines)) state.FormattingLost = true;
                        CountInlineImages(paragraph.Inlines, state);
                        break;
                    case CodeBlock code:
                        lines.Add(code.Text);
                        break;
                    case ImageBlock image:
                        lines.Add(ImagePlaceholder.Describe(image.AltText, image.ResourceId, state.Resources));
                        state.Placeholders++;
                        break;
                }
            }
        }

        private static bool HasFormatting(List<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is LinkInline) return true;
                if (inline is TextInline t && t.Style != InlineStyleEnum.None) return true;
            }

            return false;
        }

        private static bool HasFormatting(TableBlock table)
        {
            foreach (TableRow row in table.Rows)
                foreach (TableCell cell in row.Cells)
                    foreach (Block b in cell.Blocks)
                        if ((b is ParagraphBlock p && HasFormatting(p.Inlines)) || (b is HeadingBlock h && HasFormatting(h.Inlines))) return true;
            return false;
        }

        private static void CountInlineImages(List<Inline> inlines, CollectState state)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is ImageInline) state.Images++;
                else if (inline is LinkInline link) CountInlineImages(link.Inlines, state);
            }
        }

        private static void CountTableImages(TableBlock table, CollectState state)
        {
            foreach (TableRow row in table.Rows)
            {
                foreach (TableCell cell in row.Cells)
                {
                    foreach (Block b in cell.Blocks)
                    {
                        if (b is ImageBlock) state.Images++;
                        else if (b is ParagraphBlock p) CountInlineImages(p.Inlines, state);
                    }
                }
            }
        }

        private static Worksheet BuildLinesSheet(List<string> lines, SharedStrings strings, ConversionContext context)
        {
            SheetData data = new SheetData();
            int maxLength = 8;
            for (int i = 0; i < lines.Count; i++)
            {
                Row row = new Row { RowIndex = (uint)(i + 1) };
                string text = Limit(lines[i], context);
                if (text.Length > 0)
                {
                    row.Append(new Cell
                    {
                        CellReference = SpreadsheetCellReference.Reference(i, 0),
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(strings.Index(text).ToString(CultureInfo.InvariantCulture))
                    });
                }

                if (text.Length > maxLength) maxLength = text.Length;
                data.Append(row);
            }

            Worksheet sheet = new Worksheet();
            sheet.Append(new Columns(new Column { Min = 1, Max = 1, Width = ColumnWidth(maxLength), CustomWidth = true }));
            sheet.Append(data);
            return sheet;
        }

        private static Worksheet BuildTableSheet(TableBlock table, SharedStrings strings, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            SheetData data = new SheetData();
            HashSet<long> occupied = new HashSet<long>();
            List<string> merges = new List<string>();
            Dictionary<int, int> widths = new Dictionary<int, int>();
            int headerRows = Math.Min(table.HeaderRowCount, table.Rows.Count);

            for (int r = 0; r < table.Rows.Count; r++)
            {
                if (r % 256 == 0) token.ThrowIfCancellationRequested();
                Row row = new Row { RowIndex = (uint)(r + 1) };
                int col = 0;
                bool header = r < headerRows;
                foreach (TableCell cell in table.Rows[r].Cells)
                {
                    while (occupied.Contains(Key(r, col))) col++;
                    string text = Limit(ModelText.Blocks(cell.Blocks, "\n"), context);
                    bool bold = header || cell.IsHeader;
                    if (text.Length > 0) row.Append(BuildCell(r, col, text, bold, options, strings));

                    int span = cell.ColumnSpan;
                    int rows = Math.Min(cell.RowSpan, table.Rows.Count - r);
                    if (span > 1 || rows > 1)
                    {
                        for (int rr = r; rr < r + rows; rr++)
                            for (int cc = col; cc < col + span; cc++)
                                occupied.Add(Key(rr, cc));
                        merges.Add(SpreadsheetCellReference.Reference(r, col) + ":" + SpreadsheetCellReference.Reference(r + rows - 1, col + span - 1));
                    }

                    int length = LongestLine(text);
                    if (span == 1 && (!widths.ContainsKey(col) || widths[col] < length)) widths[col] = length;
                    col += span;
                }

                data.Append(row);
            }

            Worksheet sheet = new Worksheet();
            if (headerRows > 0 && headerRows < table.Rows.Count)
            {
                Pane pane = new Pane
                {
                    VerticalSplit = headerRows,
                    TopLeftCell = SpreadsheetCellReference.Reference(headerRows, 0),
                    ActivePane = PaneValues.BottomLeft,
                    State = PaneStateValues.Frozen
                };
                Selection selection = new Selection { Pane = PaneValues.BottomLeft, ActiveCell = SpreadsheetCellReference.Reference(headerRows, 0), SequenceOfReferences = new ListValue<StringValue>(new StringValue[] { new StringValue(SpreadsheetCellReference.Reference(headerRows, 0)) }) };
                sheet.Append(new SheetViews(new SheetView(pane, selection) { WorkbookViewId = 0 }));
            }

            if (widths.Count > 0)
            {
                Columns columns = new Columns();
                List<int> keys = new List<int>(widths.Keys);
                keys.Sort();
                foreach (int key in keys)
                    columns.Append(new Column { Min = (uint)(key + 1), Max = (uint)(key + 1), Width = ColumnWidth(widths[key]), CustomWidth = true });
                sheet.Append(columns);
            }

            sheet.Append(data);
            if (merges.Count > 0)
            {
                MergeCells mergeCells = new MergeCells { Count = (uint)merges.Count };
                foreach (string reference in merges) mergeCells.Append(new MergeCell { Reference = reference });
                sheet.Append(mergeCells);
            }

            return sheet;
        }

        private static Cell BuildCell(int row, int col, string text, bool bold, ConversionOptions options, SharedStrings strings)
        {
            Cell cell = new Cell { CellReference = SpreadsheetCellReference.Reference(row, col) };
            if (!bold && options.Xlsx.InferCellTypes)
            {
                XlsxTypedValue typed = XlsxTypedValue.Infer(text);
                if (typed.IsBoolean)
                {
                    cell.DataType = CellValues.Boolean;
                    cell.CellValue = new CellValue(typed.Boolean ? "1" : "0");
                    return cell;
                }

                if (typed.IsNumber)
                {
                    cell.CellValue = new CellValue(typed.Number.ToString("R", CultureInfo.InvariantCulture));
                    return cell;
                }

                if (typed.IsDate || typed.IsDateTime)
                {
                    cell.CellValue = new CellValue(typed.Number.ToString("R", CultureInfo.InvariantCulture));
                    cell.StyleIndex = typed.IsDate ? _StyleDate : _StyleDateTime;
                    return cell;
                }
            }

            cell.DataType = CellValues.SharedString;
            cell.CellValue = new CellValue(strings.Index(text).ToString(CultureInfo.InvariantCulture));
            if (bold) cell.StyleIndex = _StyleBold;
            return cell;
        }

        private static Stylesheet BuildStylesheet()
        {
            return new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164, FormatCode = "yyyy-mm-dd" },
                    new NumberingFormat { NumberFormatId = 165, FormatCode = "yyyy-mm-dd hh:mm:ss" })
                { Count = 2 },
                new Fonts(
                    new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
                    new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }))
                { Count = 2 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
                { Count = 2 },
                new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
                new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
                new CellFormats(
                    new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                    new CellFormat { NumberFormatId = 0, FontId = 1, FillId = 0, BorderId = 0, FormatId = 0, ApplyFont = true },
                    new CellFormat { NumberFormatId = 164, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 165, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, ApplyNumberFormat = true })
                { Count = 4 },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
        }

        private static string Limit(string text, ConversionContext context)
        {
            string clean = OfficeWriteHelper.CleanText(text);
            if (clean.Length <= _MaxCellText) return clean;
            context.AddWarning(WarningCodeEnum.ContentTruncated, "Cell text longer than " + _MaxCellText + " characters was truncated to fit Excel's limit.");
            return clean.Substring(0, _MaxCellText);
        }

        private static int LongestLine(string text)
        {
            int max = 0;
            foreach (string line in text.Split('\n'))
                if (line.Length > max) max = line.Length;
            return max;
        }

        private static DoubleValue ColumnWidth(int length)
        {
            double width = length * 1.1 + 2;
            if (width < 8) width = 8;
            if (width > 60) width = 60;
            return Math.Round(width, 2);
        }

        private static long Key(int row, int col)
        {
            return ((long)row << 20) | (uint)col;
        }

        #endregion
    }
}
