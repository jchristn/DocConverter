namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Machine readable report of a detect run, written with --json. Contract version 1.
    /// </summary>
    public class CliDetectReport
    {
        /// <summary>
        /// Report contract version. Always 1 in this release.
        /// </summary>
        [JsonPropertyName("contractVersion")]
        public int ContractVersion { get; set; } = 1;

        /// <summary>
        /// True when the input is a supported format.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        /// <summary>
        /// Command name, "detect".
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = "detect";

        /// <summary>
        /// Input details. Null when the input could not be read.
        /// </summary>
        [JsonPropertyName("input")]
        public CliDetectInput? Input { get; set; } = null;

        /// <summary>
        /// Detection details. Null when the input could not be read.
        /// </summary>
        [JsonPropertyName("detection")]
        public CliDetection? Detection { get; set; } = null;

        /// <summary>
        /// Error details. Null on success.
        /// </summary>
        [JsonPropertyName("error")]
        public CliReportError? Error { get; set; } = null;

        /// <summary>
        /// Process exit code.
        /// </summary>
        [JsonPropertyName("exitCode")]
        public int ExitCode { get; set; } = 0;
    }
}
