namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of a list item.
    /// </summary>
    public class CanonicalListItem
    {
        /// <summary>
        /// Item content.
        /// </summary>
        [JsonPropertyName("blocks")]
        public List<CanonicalBlock> Blocks { get; set; } = new List<CanonicalBlock>();

        /// <summary>
        /// Task list checked state.
        /// </summary>
        [JsonPropertyName("checked")]
        public bool? Checked { get; set; } = null;
    }
}
