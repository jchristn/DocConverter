namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One supported conversion pair in a formats report.
    /// </summary>
    public class CliFormatsConversion
    {
        /// <summary>
        /// Source format.
        /// </summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = "";

        /// <summary>
        /// Target format.
        /// </summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = "";

        /// <summary>
        /// Fidelity, Full or Projection.
        /// </summary>
        [JsonPropertyName("fidelity")]
        public string Fidelity { get; set; } = "";

        /// <summary>
        /// What is lost, for Projection pairs.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";
    }
}
