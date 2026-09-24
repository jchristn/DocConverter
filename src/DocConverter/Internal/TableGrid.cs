namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;

    /// <summary>
    /// A rectangular text grid made from a table, with column and row spans expanded for formats that cannot merge cells.
    /// </summary>
    internal sealed class TableGrid
    {
        internal List<List<string>> Rows { get; } = new List<List<string>>();

        internal int ColumnCount { get; private set; }

        internal bool HadSpans { get; private set; }

        internal static TableGrid Build(TableBlock table, TableSpanModeEnum spanMode, string cellSeparator)
        {
            TableGrid grid = new TableGrid();
            int columns = table.ColumnCount;
            grid.ColumnCount = columns;

            // Tracks cells still covered by a row span from above: column -> (remaining rows, text).
            int[] remaining = new int[columns];
            string[] carried = new string[columns];

            foreach (TableRow row in table.Rows)
            {
                List<string> output = new List<string>();
                int cellIndex = 0;
                int col = 0;
                while (col < columns)
                {
                    if (remaining[col] > 0)
                    {
                        output.Add(spanMode == TableSpanModeEnum.Repeat ? carried[col] : "");
                        remaining[col]--;
                        col++;
                        continue;
                    }

                    if (cellIndex >= row.Cells.Count)
                    {
                        output.Add("");
                        col++;
                        continue;
                    }

                    TableCell cell = row.Cells[cellIndex++];
                    string text = ModelText.Blocks(cell.Blocks, cellSeparator);
                    int span = cell.ColumnSpan;
                    if (span > 1 || cell.RowSpan > 1) grid.HadSpans = true;
                    for (int s = 0; s < span && col < columns; s++)
                    {
                        output.Add(s == 0 || spanMode == TableSpanModeEnum.Repeat ? text : "");
                        if (cell.RowSpan > 1)
                        {
                            remaining[col] = cell.RowSpan - 1;
                            carried[col] = text;
                        }

                        col++;
                    }
                }

                grid.Rows.Add(output);
            }

            return grid;
        }
    }
}
