namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Input section of a detect report.
    /// </summary>
    public class CliDetectInput
    {
        /// <summary>
        /// Input path as given, or "stdin".
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        /// <summary>
        /// Input size in bytes.
        /// </summary>
        [JsonPropertyName("bytes")]
        public long Bytes { get; set; } = 0;
    }
}
