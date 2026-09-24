namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing Markdown.
    /// </summary>
    public class MarkdownOptions
    {
        /// <summary>
        /// How images are written. Default DataUri (base64 data URI).
        /// </summary>
        public ImageModeEnum ImageMode { get; set; } = ImageModeEnum.DataUri;

        /// <summary>
        /// How merged table cells are written, since Markdown tables cannot merge cells. Default Repeat.
        /// </summary>
        public TableSpanModeEnum TableSpanMode { get; set; } = TableSpanModeEnum.Repeat;

        /// <summary>
        /// When true, characters that would be read as raw HTML are escaped. Default true.
        /// </summary>
        public bool EscapeHtml { get; set; } = true;
    }
}
