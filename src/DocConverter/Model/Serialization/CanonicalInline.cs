namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of an inline. Type is one of text, link, image, lineBreak.
    /// </summary>
    public class CanonicalInline
    {
        /// <summary>
        /// Inline type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        /// <summary>
        /// Text of a text inline.
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; } = null;

        /// <summary>
        /// Style flags of a text inline (InlineStyleEnum names).
        /// </summary>
        [JsonPropertyName("style")]
        public List<string>? Style { get; set; } = null;

        /// <summary>
        /// Link URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; } = null;

        /// <summary>
        /// Link title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Link content.
        /// </summary>
        [JsonPropertyName("inlines")]
        public List<CanonicalInline>? Inlines { get; set; } = null;

        /// <summary>
        /// Image resource id.
        /// </summary>
        [JsonPropertyName("resourceId")]
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Image alternative text.
        /// </summary>
        [JsonPropertyName("altText")]
        public string? AltText { get; set; } = null;
    }
}
