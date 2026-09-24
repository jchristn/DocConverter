namespace DocConverter.Cli
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Contents of a --options JSON file. Every member is optional; members that are present override the defaults, and
    /// command line flags override the file. Property names are camelCase and matched case-insensitively. Unknown
    /// properties are an error so typos do not pass silently.
    /// </summary>
    public class CliOptionsFile
    {
        /// <summary>
        /// ConversionOptions.IncludeImages.
        /// </summary>
        [JsonPropertyName("includeImages")]
        public bool? IncludeImages { get; set; } = null;

        /// <summary>
        /// ConversionOptions.IncludeMetadata.
        /// </summary>
        [JsonPropertyName("includeMetadata")]
        public bool? IncludeMetadata { get; set; } = null;

        /// <summary>
        /// ConversionOptions.Title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Input encoding name, for example "utf-8".
        /// </summary>
        [JsonPropertyName("inputEncoding")]
        public string? InputEncoding { get; set; } = null;

        /// <summary>
        /// Output encoding name, for example "utf-8".
        /// </summary>
        [JsonPropertyName("outputEncoding")]
        public string? OutputEncoding { get; set; } = null;

        /// <summary>
        /// Line ending: lf, crlf or platform.
        /// </summary>
        [JsonPropertyName("lineEnding")]
        public string? LineEnding { get; set; } = null;

        /// <summary>
        /// ConversionOptions.Deterministic.
        /// </summary>
        [JsonPropertyName("deterministic")]
        public bool? Deterministic { get; set; } = null;

        /// <summary>
        /// ConversionOptions.TreatWarningsAsErrors.
        /// </summary>
        [JsonPropertyName("treatWarningsAsErrors")]
        public bool? TreatWarningsAsErrors { get; set; } = null;

        /// <summary>
        /// Image mode for Markdown and HTML: embed, omit, placeholder or external.
        /// </summary>
        [JsonPropertyName("images")]
        public string? Images { get; set; } = null;

        /// <summary>
        /// HtmlOptions.Mode: document or fragment.
        /// </summary>
        [JsonPropertyName("htmlMode")]
        public string? HtmlMode { get; set; } = null;

        /// <summary>
        /// HtmlOptions.IncludeStylesheet.
        /// </summary>
        [JsonPropertyName("htmlIncludeStylesheet")]
        public bool? HtmlIncludeStylesheet { get; set; } = null;

        /// <summary>
        /// TextOptions.WrapColumn.
        /// </summary>
        [JsonPropertyName("textWrapColumn")]
        public int? TextWrapColumn { get; set; } = null;

        /// <summary>
        /// TextOptions.HeadingStyle: underline, uppercase or none.
        /// </summary>
        [JsonPropertyName("textHeadingStyle")]
        public string? TextHeadingStyle { get; set; } = null;

        /// <summary>
        /// TextOptions.TableStyle: aligned or tabs.
        /// </summary>
        [JsonPropertyName("textTableStyle")]
        public string? TextTableStyle { get; set; } = null;

        /// <summary>
        /// JsonOptions.Indented.
        /// </summary>
        [JsonPropertyName("jsonIndented")]
        public bool? JsonIndented { get; set; } = null;

        /// <summary>
        /// JsonOptions.IncludeBinary.
        /// </summary>
        [JsonPropertyName("jsonIncludeBinary")]
        public bool? JsonIncludeBinary { get; set; } = null;

        /// <summary>
        /// XmlOptions.Indented.
        /// </summary>
        [JsonPropertyName("xmlIndented")]
        public bool? XmlIndented { get; set; } = null;

        /// <summary>
        /// CSV delimiter: a single character or "tab".
        /// </summary>
        [JsonPropertyName("csvDelimiter")]
        public string? CsvDelimiter { get; set; } = null;

        /// <summary>
        /// CsvOptions.HasHeaderRow.
        /// </summary>
        [JsonPropertyName("csvHasHeaderRow")]
        public bool? CsvHasHeaderRow { get; set; } = null;

        /// <summary>
        /// Table selection: first, all, or a zero based index.
        /// </summary>
        [JsonPropertyName("table")]
        public string? Table { get; set; } = null;

        /// <summary>
        /// Page size for PDF and DOCX: a4, letter or legal.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public string? PageSize { get; set; } = null;

        /// <summary>
        /// Page margin in points for PDF and DOCX.
        /// </summary>
        [JsonPropertyName("marginPoints")]
        public double? MarginPoints { get; set; } = null;

        /// <summary>
        /// PptxOptions.SlideSplitHeadingLevel.
        /// </summary>
        [JsonPropertyName("slideSplitHeadingLevel")]
        public int? SlideSplitHeadingLevel { get; set; } = null;

        /// <summary>
        /// PptxOptions.IncludeNotes.
        /// </summary>
        [JsonPropertyName("includeNotes")]
        public bool? IncludeNotes { get; set; } = null;

        /// <summary>
        /// XlsxOptions.IncludeHiddenSheets.
        /// </summary>
        [JsonPropertyName("includeHiddenSheets")]
        public bool? IncludeHiddenSheets { get; set; } = null;

        /// <summary>
        /// PdfOptions.PreservePages.
        /// </summary>
        [JsonPropertyName("preservePages")]
        public bool? PreservePages { get; set; } = null;

        /// <summary>
        /// Largest accepted input in megabytes.
        /// </summary>
        [JsonPropertyName("maxInputMb")]
        public int? MaxInputMb { get; set; } = null;
    }
}
