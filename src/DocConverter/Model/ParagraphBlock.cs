namespace DocConverter.Model
{
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// A paragraph of inline content.
    /// </summary>
    public class ParagraphBlock : Block
    {
        private List<Inline> _Inlines = new List<Inline>();

        /// <summary>
        /// Horizontal alignment. Default Default (the target's own default).
        /// </summary>
        public TextAlignmentEnum Alignment { get; set; } = TextAlignmentEnum.Default;

        /// <summary>
        /// Paragraph content. Never null.
        /// </summary>
        public List<Inline> Inlines
        {
            get => _Inlines;
            set => _Inlines = value ?? new List<Inline>();
        }

        /// <summary>
        /// Instantiate an empty paragraph.
        /// </summary>
        public ParagraphBlock()
        {
        }

        /// <summary>
        /// Instantiate a paragraph with plain text.
        /// </summary>
        /// <param name="text">Paragraph text. Null is treated as empty.</param>
        public ParagraphBlock(string? text)
        {
            if (!string.IsNullOrEmpty(text)) _Inlines.Add(new TextInline(text!));
        }

        /// <summary>
        /// Instantiate a paragraph with the given inlines.
        /// </summary>
        /// <param name="inlines">Inline content. Null is treated as empty.</param>
        public ParagraphBlock(IEnumerable<Inline>? inlines)
        {
            if (inlines != null) _Inlines.AddRange(inlines);
        }
    }
}
