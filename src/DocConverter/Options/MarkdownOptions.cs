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
        /// When true (default), text that a Markdown renderer would read as raw HTML (a less-than sign opening a tag, an ampersand forming an entity) is escaped so it reads back as the same text. When false it passes through, so markup-like text is interpreted by renderers.
        /// </summary>
        public bool EscapeHtml { get; set; } = true;
    }
}
