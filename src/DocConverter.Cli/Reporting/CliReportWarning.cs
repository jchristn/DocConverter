namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One warning in a convert report.
    /// </summary>
    public class CliReportWarning
    {
        /// <summary>
        /// Warning code, a WarningCodeEnum name.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        /// <summary>
        /// Message describing the first occurrence.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        /// <summary>
        /// Number of occurrences.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 1;
    }
}
