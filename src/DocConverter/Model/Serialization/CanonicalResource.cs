namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of a binary resource.
    /// </summary>
    public class CanonicalResource
    {
        /// <summary>
        /// Resource id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// Media type.
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = "application/octet-stream";

        /// <summary>
        /// File name.
        /// </summary>
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; } = null;

        /// <summary>
        /// Width in pixels.
        /// </summary>
        [JsonPropertyName("pixelWidth")]
        public int? PixelWidth { get; set; } = null;

        /// <summary>
        /// Height in pixels.
        /// </summary>
        [JsonPropertyName("pixelHeight")]
        public int? PixelHeight { get; set; } = null;

        /// <summary>
        /// Size of the data in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; } = 0;

        /// <summary>
        /// Base64 data, or null when binary data was excluded.
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } = null;
    }
}
