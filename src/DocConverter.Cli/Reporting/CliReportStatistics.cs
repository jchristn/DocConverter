namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Content statistics in a convert report.
    /// </summary>
    public class CliReportStatistics
    {
        /// <summary>
        /// Pages.
        /// </summary>
        [JsonPropertyName("pages")]
        public int Pages { get; set; } = 0;

        /// <summary>
        /// Slides.
        /// </summary>
        [JsonPropertyName("slides")]
        public int Slides { get; set; } = 0;

        /// <summary>
        /// Worksheets.
        /// </summary>
        [JsonPropertyName("sheets")]
        public int Sheets { get; set; } = 0;

        /// <summary>
        /// Headings.
        /// </summary>
        [JsonPropertyName("headings")]
        public int Headings { get; set; } = 0;

        /// <summary>
        /// Paragraphs.
        /// </summary>
        [JsonPropertyName("paragraphs")]
        public int Paragraphs { get; set; } = 0;

        /// <summary>
        /// Lists.
        /// </summary>
        [JsonPropertyName("lists")]
        public int Lists { get; set; } = 0;

        /// <summary>
        /// Tables.
        /// </summary>
        [JsonPropertyName("tables")]
        public int Tables { get; set; } = 0;

        /// <summary>
        /// Images.
        /// </summary>
        [JsonPropertyName("images")]
        public int Images { get; set; } = 0;

        /// <summary>
        /// Links.
        /// </summary>
        [JsonPropertyName("links")]
        public int Links { get; set; } = 0;
    }
}
