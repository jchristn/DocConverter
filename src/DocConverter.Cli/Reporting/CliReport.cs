namespace DocConverter.Cli.Reporting
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Machine readable report of a convert run, written with --json. Contract version 1.
    /// </summary>
    public class CliReport
    {
        /// <summary>
        /// Report contract version. Always 1 in this release.
        /// </summary>
        [JsonPropertyName("contractVersion")]
        public int ContractVersion { get; set; } = 1;

        /// <summary>
        /// True when the conversion succeeded.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        /// <summary>
        /// Command name, "convert".
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = "convert";

        /// <summary>
        /// Input details. Null when the command line could not be parsed.
        /// </summary>
        [JsonPropertyName("input")]
        public CliReportInput? Input { get; set; } = null;

        /// <summary>
        /// Output details. Null when the command line could not be parsed.
        /// </summary>
        [JsonPropertyName("output")]
        public CliReportOutput? Output { get; set; } = null;

        /// <summary>
        /// Elapsed milliseconds.
        /// </summary>
        [JsonPropertyName("durationMs")]
        public long DurationMs { get; set; } = 0;

        /// <summary>
        /// Warnings raised, one per code.
        /// </summary>
        [JsonPropertyName("warnings")]
        public List<CliReportWarning> Warnings { get; set; } = new List<CliReportWarning>();

        /// <summary>
        /// Content statistics. Null when the conversion did not complete.
        /// </summary>
        [JsonPropertyName("statistics")]
        public CliReportStatistics? Statistics { get; set; } = null;

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
