namespace DocConverter.Model
{
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// A table. Rows may be ragged; writers pad them to the widest row.
    /// </summary>
    public class TableBlock : Block
    {
        private int _HeaderRowCount = 0;
        private List<TableRow> _Rows = new List<TableRow>();
        private List<TextAlignmentEnum> _ColumnAlignments = new List<TextAlignmentEnum>();

        /// <summary>
        /// Rows, header rows first. Never null.
        /// </summary>
        public List<TableRow> Rows
        {
            get => _Rows;
            set => _Rows = value ?? new List<TableRow>();
        }

        /// <summary>
        /// Number of leading rows that are header rows. Default 0. Values below 0 are stored as 0.
        /// </summary>
        public int HeaderRowCount
        {
            get => _HeaderRowCount;
            set => _HeaderRowCount = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Caption. Null when absent.
        /// </summary>
        public string? Caption { get; set; } = null;

        /// <summary>
        /// Per column alignment. May be shorter than the column count; missing entries mean Default. Never null.
        /// </summary>
        public List<TextAlignmentEnum> ColumnAlignments
        {
            get => _ColumnAlignments;
            set => _ColumnAlignments = value ?? new List<TextAlignmentEnum>();
        }

        /// <summary>
        /// Number of columns: the widest row, counting column spans.
        /// </summary>
        public int ColumnCount
        {
            get
            {
                int max = 0;
                foreach (TableRow row in _Rows)
                {
                    int width = 0;
                    foreach (TableCell cell in row.Cells) width += cell.ColumnSpan;
                    if (width > max) max = width;
                }

                return max;
            }
        }

        /// <summary>
        /// Instantiate an empty table.
        /// </summary>
        public TableBlock()
        {
        }
    }
}
