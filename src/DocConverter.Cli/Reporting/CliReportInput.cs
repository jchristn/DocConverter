namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Input section of a convert report.
    /// </summary>
    public class CliReportInput
    {
        /// <summary>
        /// Input path as given, or "stdin".
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        /// <summary>
        /// Source format name. Null when it could not be determined.
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; } = null;

        /// <summary>
        /// Input size in bytes.
        /// </summary>
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; } = 0;

        /// <summary>
        /// True when the source format was detected rather than given with --from.
        /// </summary>
        [JsonPropertyName("detected")]
        public bool Detected { get; set; } = false;
    }
}
