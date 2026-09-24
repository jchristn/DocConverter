namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Detection section of a detect report.
    /// </summary>
    public class CliDetection
    {
        /// <summary>
        /// Detected format name, or null when unsupported.
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; } = null;

        /// <summary>
        /// Media type.
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = "";

        /// <summary>
        /// Usual extension without the dot.
        /// </summary>
        [JsonPropertyName("extension")]
        public string Extension { get; set; } = "";

        /// <summary>
        /// Human readable description, including unsupported formats.
        /// </summary>
        [JsonPropertyName("recognizedAs")]
        public string RecognizedAs { get; set; } = "";

        /// <summary>
        /// How the format was determined (DetectionConfidenceEnum name).
        /// </summary>
        [JsonPropertyName("confidence")]
        public string Confidence { get; set; } = "";

        /// <summary>
        /// True when DocConverter can read the input.
        /// </summary>
        [JsonPropertyName("supported")]
        public bool Supported { get; set; } = false;
    }
}
