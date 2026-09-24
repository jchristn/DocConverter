namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A table row.
    /// </summary>
    public class TableRow
    {
        private List<TableCell> _Cells = new List<TableCell>();

        /// <summary>
        /// Cells in column order. Never null.
        /// </summary>
        public List<TableCell> Cells
        {
            get => _Cells;
            set => _Cells = value ?? new List<TableCell>();
        }

        /// <summary>
        /// Instantiate an empty row.
        /// </summary>
        public TableRow()
        {
        }

        /// <summary>
        /// Instantiate a row of plain text cells.
        /// </summary>
        /// <param name="texts">Cell texts. Null entries become empty cells.</param>
        public TableRow(IEnumerable<string?> texts)
        {
            if (texts == null) return;
            foreach (string? text in texts) _Cells.Add(new TableCell(text));
        }
    }
}
