namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing CSV and TSV.
    /// </summary>
    public class CsvOptions
    {
        private int _TableIndex = 0;

        /// <summary>
        /// Field delimiter. Null (default) means comma for CSV and tab for TSV.
        /// </summary>
        public char? Delimiter { get; set; } = null;

        /// <summary>
        /// When reading, treat the first record as the header row. Default true.
        /// </summary>
        public bool HasHeaderRow { get; set; } = true;

        /// <summary>
        /// Which tables to write. Default First.
        /// </summary>
        public TableSelectionEnum TableSelection { get; set; } = TableSelectionEnum.First;

        /// <summary>
        /// Zero based table index used when TableSelection is Index. Default 0. Allowed: 0 through 100000.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int TableIndex
        {
            get => _TableIndex;
            set => _TableIndex = value >= 0 && value <= 100000
                ? value
                : throw new InvalidConversionOptionsException("TableIndex must be 0 through 100000; got " + value + ".");
        }

        /// <summary>
        /// What to do when the document has no tables. Default ParagraphsAsRows.
        /// </summary>
        public NoTableBehaviorEnum NoTableBehavior { get; set; } = NoTableBehaviorEnum.ParagraphsAsRows;

        /// <summary>
        /// How merged table cells are written. Default Repeat.
        /// </summary>
        public TableSpanModeEnum TableSpanMode { get; set; } = TableSpanModeEnum.Repeat;
    }
}
