namespace DocConverter.Writers.Pptx
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// One positioned element of a slide page: a text box with its blocks, a table chunk, or a picture.
    /// </summary>
    internal sealed class PptxUnit
    {
        internal PptxUnitKind Kind { get; }

        internal List<Block> TextBlocks { get; } = new List<Block>();

        internal TableBlock? Table { get; set; }

        internal string? ResourceId { get; set; }

        internal string? AltText { get; set; }

        internal long Height { get; set; }

        internal PptxUnit(PptxUnitKind kind)
        {
            Kind = kind;
        }
    }
}
