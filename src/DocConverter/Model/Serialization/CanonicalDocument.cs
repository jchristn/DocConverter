namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical serialized form of a DocumentModel (version 1). JSON and XML output of a document use this shape, and JSON or XML input carrying the "docconverter" marker is read back losslessly.
    /// </summary>
    public class CanonicalDocument
    {
        /// <summary>
        /// Format marker and version. Always "1" for this release.
        /// </summary>
        [JsonPropertyName("docconverter")]
        public string DocConverter { get; set; } = "1";

        /// <summary>
        /// Metadata, or null when metadata is excluded.
        /// </summary>
        [JsonPropertyName("metadata")]
        public CanonicalMetadata? Metadata { get; set; } = null;

        /// <summary>
        /// Top level blocks.
        /// </summary>
        [JsonPropertyName("blocks")]
        public List<CanonicalBlock> Blocks { get; set; } = new List<CanonicalBlock>();

        /// <summary>
        /// Binary resources.
        /// </summary>
        [JsonPropertyName("resources")]
        public List<CanonicalResource> Resources { get; set; } = new List<CanonicalResource>();
    }
}
