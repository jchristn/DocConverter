namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of DocumentMetadata.
    /// </summary>
    public class CanonicalMetadata
    {
        /// <summary>
        /// Title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public string? Subject { get; set; } = null;

        /// <summary>
        /// Author.
        /// </summary>
        [JsonPropertyName("author")]
        public string? Author { get; set; } = null;

        /// <summary>
        /// Keywords.
        /// </summary>
        [JsonPropertyName("keywords")]
        public string? Keywords { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Language tag.
        /// </summary>
        [JsonPropertyName("language")]
        public string? Language { get; set; } = null;

        /// <summary>
        /// Creation time, UTC.
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime? Created { get; set; } = null;

        /// <summary>
        /// Modification time, UTC.
        /// </summary>
        [JsonPropertyName("modified")]
        public DateTime? Modified { get; set; } = null;
    }
}
