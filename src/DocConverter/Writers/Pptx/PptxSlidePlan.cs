namespace DocConverter.Writers.Pptx
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// A logical slide before pagination: a title, an optional subtitle, and body blocks.
    /// </summary>
    internal sealed class PptxSlidePlan
    {
        internal string? Title { get; set; }

        internal string? Subtitle { get; set; }

        internal List<Block> Body { get; } = new List<Block>();

        internal PptxSlidePlan(string? title)
        {
            Title = title;
        }
    }
}
