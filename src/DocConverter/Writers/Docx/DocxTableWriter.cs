namespace DocConverter.Writers.Docx
{
    using System.Collections.Generic;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Writes tables: grid sized to the widest row, header rows repeated on each page, column spans as gridSpan and row
    /// spans as vMerge continuation cells, ragged rows padded with empty cells.
    /// </summary>
    internal sealed class DocxTableWriter
    {
        private readonly DocxWriteSession _Session;
        private readonly DocxBlockWriter _Blocks;

        internal DocxTableWriter(DocxWriteSession session, DocxBlockWriter blocks)
        {
            _Session = session;
            _Blocks = blocks;
        }

        internal W.Table Write(TableBlock table)
        {
            int columns = table.ColumnCount;
            if (columns < 1) columns = 1;
            int columnWidth = _Session.Page.ContentWidthTwips / columns;
            if (columnWidth < 1) columnWidth = 1;

            W.Table result = new W.Table();
            W.TableProperties tblPr = new W.TableProperties();
            tblPr.TableStyle = new W.TableStyle { Val = "TableGrid" };
            tblPr.TableWidth = new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct };
            tblPr.TableLook = new W.TableLook
            {
                Val = table.HeaderRowCount > 0 ? "04A0" : "0480",
                FirstRow = OnOffValue.FromBoolean(table.HeaderRowCount > 0),
                LastRow = OnOffValue.FromBoolean(false),
                FirstColumn = OnOffValue.FromBoolean(false),
                LastColumn = OnOffValue.FromBoolean(false),
                NoHorizontalBand = OnOffValue.FromBoolean(false),
                NoVerticalBand = OnOffValue.FromBoolean(true)
            };
            result.Append(tblPr);

            W.TableGrid grid = new W.TableGrid();
            for (int c = 0; c < columns; c++) grid.Append(new W.GridColumn { Width = columnWidth.ToString() });
            result.Append(grid);

            // For each grid column, the cell whose row span still covers rows below, and how many rows it still covers.
            TableCell?[] covering = new TableCell?[columns];
            int[] remaining = new int[columns];

            for (int r = 0; r < table.Rows.Count; r++)
            {
                _Session.Token.ThrowIfCancellationRequested();
                TableRow row = table.Rows[r];
                W.TableRow tr = new W.TableRow();
                if (r < table.HeaderRowCount)
                    tr.Append(new W.TableRowProperties(new W.TableHeader()));

                int nextCell = 0;
                int column = 0;
                while (column < columns)
                {
                    if (remaining[column] > 0 && covering[column] != null)
                    {
                        TableCell origin = covering[column]!;
                        int span = Clamp(origin.ColumnSpan, columns - column);
                        tr.Append(ContinuationCell(span, columnWidth));
                        for (int c = column; c < column + span; c++) remaining[c]--;
                        column += span;
                        continue;
                    }

                    if (nextCell < row.Cells.Count)
                    {
                        TableCell cell = row.Cells[nextCell++];
                        int span = Clamp(cell.ColumnSpan, columns - column);
                        tr.Append(Cell(cell, span, columnWidth));
                        if (cell.RowSpan > 1)
                        {
                            for (int c = column; c < column + span; c++)
                            {
                                covering[c] = cell;
                                remaining[c] = cell.RowSpan - 1;
                            }
                        }

                        column += span;
                        continue;
                    }

                    tr.Append(EmptyCell(1, columnWidth));
                    column++;
                }

                result.Append(tr);
            }

            return result;
        }

        private W.TableCell Cell(TableCell cell, int span, int columnWidth)
        {
            W.TableCell tc = new W.TableCell();
            W.TableCellProperties tcPr = new W.TableCellProperties();
            tcPr.TableCellWidth = new W.TableCellWidth { Width = (columnWidth * span).ToString(), Type = W.TableWidthUnitValues.Dxa };
            if (span > 1) tcPr.GridSpan = new W.GridSpan { Val = span };
            if (cell.RowSpan > 1) tcPr.VerticalMerge = new W.VerticalMerge { Val = W.MergedCellValues.Restart };
            tc.Append(tcPr);
            _Blocks.WriteBlocks(cell.Blocks, tc);
            EnsureEndsWithParagraph(tc);
            return tc;
        }

        private static W.TableCell ContinuationCell(int span, int columnWidth)
        {
            W.TableCell tc = new W.TableCell();
            W.TableCellProperties tcPr = new W.TableCellProperties();
            tcPr.TableCellWidth = new W.TableCellWidth { Width = (columnWidth * span).ToString(), Type = W.TableWidthUnitValues.Dxa };
            if (span > 1) tcPr.GridSpan = new W.GridSpan { Val = span };
            tcPr.VerticalMerge = new W.VerticalMerge();
            tc.Append(tcPr);
            tc.Append(new W.Paragraph());
            return tc;
        }

        private static W.TableCell EmptyCell(int span, int columnWidth)
        {
            W.TableCell tc = new W.TableCell();
            W.TableCellProperties tcPr = new W.TableCellProperties();
            tcPr.TableCellWidth = new W.TableCellWidth { Width = (columnWidth * span).ToString(), Type = W.TableWidthUnitValues.Dxa };
            tc.Append(tcPr);
            tc.Append(new W.Paragraph());
            return tc;
        }

        private static void EnsureEndsWithParagraph(W.TableCell tc)
        {
            OpenXmlElement? last = tc.LastChild;
            if (last == null || !(last is W.Paragraph)) tc.Append(new W.Paragraph());
        }

        private static int Clamp(int span, int available)
        {
            if (span < 1) return 1;
            return span > available ? (available < 1 ? 1 : available) : span;
        }
    }
}
