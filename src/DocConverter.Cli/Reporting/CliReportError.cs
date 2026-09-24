namespace DocConverter.Cli.Reporting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error section of a report. Codes: Usage, InvalidOptions, UnsupportedFormat, ConversionNotSupported, DocumentRead,
    /// DocumentWrite, NotImplemented, IO, Warnings, InputTooLarge, Cancelled, Internal.
    /// </summary>
    public class CliReportError
    {
        /// <summary>
        /// Error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        /// <summary>
        /// Human readable message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}
