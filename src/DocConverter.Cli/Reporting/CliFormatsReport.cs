namespace DocConverter.Cli.Reporting
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Machine readable list of formats and conversions, written by formats --json. Contract version 1.
    /// </summary>
    public class CliFormatsReport
    {
        /// <summary>
        /// Report contract version. Always 1 in this release.
        /// </summary>
        [JsonPropertyName("contractVersion")]
        public int ContractVersion { get; set; } = 1;

        /// <summary>
        /// Readable formats.
        /// </summary>
        [JsonPropertyName("inputs")]
        public List<string> Inputs { get; set; } = new List<string>();

        /// <summary>
        /// Writable formats.
        /// </summary>
        [JsonPropertyName("outputs")]
        public List<string> Outputs { get; set; } = new List<string>();

        /// <summary>
        /// Every supported pair.
        /// </summary>
        [JsonPropertyName("conversions")]
        public List<CliFormatsConversion> Conversions { get; set; } = new List<CliFormatsConversion>();
    }
}
