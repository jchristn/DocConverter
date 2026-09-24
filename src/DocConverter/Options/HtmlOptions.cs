namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing HTML.
    /// </summary>
    public class HtmlOptions
    {
        /// <summary>
        /// Complete document or body fragment. Default Document.
        /// </summary>
        public HtmlOutputModeEnum Mode { get; set; } = HtmlOutputModeEnum.Document;

        /// <summary>
        /// When true and Mode is Document, a small embedded stylesheet is written. Default true.
        /// </summary>
        public bool IncludeStylesheet { get; set; } = true;

        /// <summary>
        /// How images are written. Default DataUri.
        /// </summary>
        public ImageModeEnum ImageMode { get; set; } = ImageModeEnum.DataUri;
    }
}
