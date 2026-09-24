namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A table cell holding blocks.
    /// </summary>
    public class TableCell
    {
        private int _ColumnSpan = 1;
        private int _RowSpan = 1;
        private List<Block> _Blocks = new List<Block>();

        /// <summary>
        /// Cell content. Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Number of columns the cell spans. Default 1. Values below 1 are stored as 1.
        /// </summary>
        public int ColumnSpan
        {
            get => _ColumnSpan;
            set => _ColumnSpan = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Number of rows the cell spans. Default 1. Values below 1 are stored as 1.
        /// </summary>
        public int RowSpan
        {
            get => _RowSpan;
            set => _RowSpan = value < 1 ? 1 : value;
        }

        /// <summary>
        /// True when the cell is a header cell. Default false.
        /// </summary>
        public bool IsHeader { get; set; } = false;

        /// <summary>
        /// Instantiate an empty cell.
        /// </summary>
        public TableCell()
        {
        }

        /// <summary>
        /// Instantiate a cell holding one paragraph of plain text. Null or empty text gives an empty cell.
        /// </summary>
        /// <param name="text">Cell text.</param>
        public TableCell(string? text)
        {
            if (!string.IsNullOrEmpty(text)) _Blocks.Add(new ParagraphBlock(text));
        }
    }
}
