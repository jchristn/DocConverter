namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Canonical form of a block. Type is one of section, heading, paragraph, list, table, code, quote, image, thematicBreak, pageBreak; the other members are set only when meaningful for that type.
    /// </summary>
    public class CanonicalBlock
    {
        /// <summary>
        /// Block type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        /// <summary>
        /// Optional identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Source page number.
        /// </summary>
        [JsonPropertyName("sourcePage")]
        public int? SourcePage { get; set; } = null;

        /// <summary>
        /// Source worksheet name.
        /// </summary>
        [JsonPropertyName("sourceSheet")]
        public string? SourceSheet { get; set; } = null;

        /// <summary>
        /// Source slide number.
        /// </summary>
        [JsonPropertyName("sourceSlide")]
        public int? SourceSlide { get; set; } = null;

        /// <summary>
        /// Heading level.
        /// </summary>
        [JsonPropertyName("level")]
        public int? Level { get; set; } = null;

        /// <summary>
        /// Inline content of headings and paragraphs.
        /// </summary>
        [JsonPropertyName("inlines")]
        public List<CanonicalInline>? Inlines { get; set; } = null;

        /// <summary>
        /// Paragraph alignment (TextAlignmentEnum name).
        /// </summary>
        [JsonPropertyName("alignment")]
        public string? Alignment { get; set; } = null;

        /// <summary>
        /// Section kind (SectionKindEnum name) or list kind (ListKindEnum name).
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; } = null;

        /// <summary>
        /// Section title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Child blocks of sections and quotes.
        /// </summary>
        [JsonPropertyName("blocks")]
        public List<CanonicalBlock>? Blocks { get; set; } = null;

        /// <summary>
        /// First number of an ordered list.
        /// </summary>
        [JsonPropertyName("start")]
        public int? Start { get; set; } = null;

        /// <summary>
        /// List items.
        /// </summary>
        [JsonPropertyName("items")]
        public List<CanonicalListItem>? Items { get; set; } = null;

        /// <summary>
        /// Table rows.
        /// </summary>
        [JsonPropertyName("rows")]
        public List<CanonicalTableRow>? Rows { get; set; } = null;

        /// <summary>
        /// Number of header rows.
        /// </summary>
        [JsonPropertyName("headerRowCount")]
        public int? HeaderRowCount { get; set; } = null;

        /// <summary>
        /// Table or image caption.
        /// </summary>
        [JsonPropertyName("caption")]
        public string? Caption { get; set; } = null;

        /// <summary>
        /// Table column alignments (TextAlignmentEnum names).
        /// </summary>
        [JsonPropertyName("columnAlignments")]
        public List<string>? ColumnAlignments { get; set; } = null;

        /// <summary>
        /// Code language.
        /// </summary>
        [JsonPropertyName("language")]
        public string? Language { get; set; } = null;

        /// <summary>
        /// Code text.
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; } = null;

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

        /// <summary>
        /// Image display width in points.
        /// </summary>
        [JsonPropertyName("width")]
        public double? Width { get; set; } = null;

        /// <summary>
        /// Image display height in points.
        /// </summary>
        [JsonPropertyName("height")]
        public double? Height { get; set; } = null;
    }
}
