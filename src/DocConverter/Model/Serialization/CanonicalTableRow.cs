namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of a table row.
    /// </summary>
    public class CanonicalTableRow
    {
        /// <summary>
        /// Cells.
        /// </summary>
        [JsonPropertyName("cells")]
        public List<CanonicalTableCell> Cells { get; set; } = new List<CanonicalTableCell>();
    }
}
