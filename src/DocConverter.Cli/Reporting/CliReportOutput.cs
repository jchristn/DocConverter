namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Output section of a convert report.
    /// </summary>
    public class CliReportOutput
    {
        /// <summary>
        /// Output path as given, or "stdout".
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        /// <summary>
        /// Target format name. Null when it could not be determined.
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; } = null;

        /// <summary>
        /// Output size in bytes. 0 when nothing was written.
        /// </summary>
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; } = 0;
    }
}
