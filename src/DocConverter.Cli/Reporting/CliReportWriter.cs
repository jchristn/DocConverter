namespace DocConverter.Cli.Reporting
{
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Serializes and deserializes docconv reports: camelCase, one line, nulls written explicitly.
    /// </summary>
    public static class CliReportWriter
    {
        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = false
        };

        /// <summary>
        /// Serialize a report to a single line of JSON.
        /// </summary>
        /// <typeparam name="T">Report type.</typeparam>
        /// <param name="report">Report.</param>
        /// <returns>JSON text without a trailing newline.</returns>
        public static string Serialize<T>(T report)
        {
            return JsonSerializer.Serialize(report, _Options);
        }

        /// <summary>
        /// Deserialize a report, for tests and tooling that consume docconv output.
        /// </summary>
        /// <typeparam name="T">Report type.</typeparam>
        /// <param name="json">JSON text.</param>
        /// <returns>Report, or null for the JSON literal null.</returns>
        public static T? Deserialize<T>(string json) where T : class
        {
            return JsonSerializer.Deserialize<T>(json, _Options);
        }
    }
}
