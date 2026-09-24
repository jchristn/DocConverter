namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for writing plain text.
    /// </summary>
    public class TextOptions
    {
        private int _WrapColumn = 0;

        /// <summary>
        /// How headings are marked. Default Underline.
        /// </summary>
        public TextHeadingStyleEnum HeadingStyle { get; set; } = TextHeadingStyleEnum.Underline;

        /// <summary>
        /// How tables are rendered. Default Aligned.
        /// </summary>
        public TextTableStyleEnum TableStyle { get; set; } = TextTableStyleEnum.Aligned;

        /// <summary>
        /// Hard wrap column for paragraphs. 0 disables wrapping. Default 0. Allowed: 0, or 20 through 1000.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int WrapColumn
        {
            get => _WrapColumn;
            set => _WrapColumn = (value == 0 || (value >= 20 && value <= 1000))
                ? value
                : throw new InvalidConversionOptionsException("WrapColumn must be 0, or 20 through 1000; got " + value + ".");
        }

        /// <summary>
        /// When true, images are written as a bracketed placeholder line; when false they are omitted. Default true.
        /// </summary>
        public bool IncludeImagePlaceholders { get; set; } = true;

        /// <summary>
        /// When true, link URLs are written in parentheses after the link text. Default true.
        /// </summary>
        public bool IncludeLinkUrls { get; set; } = true;

        /// <summary>
        /// How merged table cells are written. Default Repeat.
        /// </summary>
        public TableSpanModeEnum TableSpanMode { get; set; } = TableSpanModeEnum.Repeat;
    }
}
