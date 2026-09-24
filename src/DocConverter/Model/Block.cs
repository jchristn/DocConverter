namespace DocConverter.Model
{
    /// <summary>
    /// Base type for every block level element in the document model.
    /// </summary>
    public abstract class Block
    {
        /// <summary>
        /// Optional identifier, for example an HTML id or a bookmark name. Null when absent.
        /// </summary>
        public string? Id { get; set; } = null;

        /// <summary>
        /// One based page number in a paged source (PDF). Null when not applicable.
        /// </summary>
        public int? SourcePage { get; set; } = null;

        /// <summary>
        /// Worksheet name in a spreadsheet source. Null when not applicable.
        /// </summary>
        public string? SourceSheet { get; set; } = null;

        /// <summary>
        /// One based slide number in a presentation source. Null when not applicable.
        /// </summary>
        public int? SourceSlide { get; set; } = null;
    }
}
