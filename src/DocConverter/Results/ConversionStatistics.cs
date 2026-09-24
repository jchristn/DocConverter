namespace DocConverter.Results
{
    /// <summary>
    /// Counts of the content that passed through a conversion, taken from the document model after reading.
    /// </summary>
    public class ConversionStatistics
    {
        /// <summary>
        /// Pages in a paged source (highest page number seen), or page sections. 0 when not applicable.
        /// </summary>
        public int Pages { get; set; } = 0;

        /// <summary>
        /// Slides. 0 when not applicable.
        /// </summary>
        public int Slides { get; set; } = 0;

        /// <summary>
        /// Worksheets. 0 when not applicable.
        /// </summary>
        public int Sheets { get; set; } = 0;

        /// <summary>
        /// Headings.
        /// </summary>
        public int Headings { get; set; } = 0;

        /// <summary>
        /// Paragraphs, including paragraphs inside list items and table cells.
        /// </summary>
        public int Paragraphs { get; set; } = 0;

        /// <summary>
        /// Lists, including nested lists.
        /// </summary>
        public int Lists { get; set; } = 0;

        /// <summary>
        /// List items.
        /// </summary>
        public int ListItems { get; set; } = 0;

        /// <summary>
        /// Tables.
        /// </summary>
        public int Tables { get; set; } = 0;

        /// <summary>
        /// Table rows.
        /// </summary>
        public int TableRows { get; set; } = 0;

        /// <summary>
        /// Table cells.
        /// </summary>
        public int TableCells { get; set; } = 0;

        /// <summary>
        /// Images (block and inline).
        /// </summary>
        public int Images { get; set; } = 0;

        /// <summary>
        /// Links.
        /// </summary>
        public int Links { get; set; } = 0;

        /// <summary>
        /// Characters of text.
        /// </summary>
        public long Characters { get; set; } = 0;
    }
}
