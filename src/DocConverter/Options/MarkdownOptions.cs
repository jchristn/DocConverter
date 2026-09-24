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
        /// How images are written. Default Placeholder: each image becomes a short text placeholder naming its alt
        /// text, type and size, so Markdown stays small and readable for language models and indexing. Set DataUri to
        /// embed images as base64 data URIs, External to write them as side files, or Omit to drop them.
        /// </summary>
        public ImageModeEnum ImageMode { get; set; } = ImageModeEnum.Placeholder;

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
