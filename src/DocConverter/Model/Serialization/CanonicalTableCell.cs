namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of a table cell.
    /// </summary>
    public class CanonicalTableCell
    {
        /// <summary>
        /// Cell content.
        /// </summary>
        [JsonPropertyName("blocks")]
        public List<CanonicalBlock> Blocks { get; set; } = new List<CanonicalBlock>();

        /// <summary>
        /// Column span when greater than 1.
        /// </summary>
        [JsonPropertyName("colSpan")]
        public int? ColumnSpan { get; set; } = null;

        /// <summary>
        /// Row span when greater than 1.
        /// </summary>
        [JsonPropertyName("rowSpan")]
        public int? RowSpan { get; set; } = null;

        /// <summary>
        /// True for header cells.
        /// </summary>
        [JsonPropertyName("header")]
        public bool? IsHeader { get; set; } = null;
    }
}
