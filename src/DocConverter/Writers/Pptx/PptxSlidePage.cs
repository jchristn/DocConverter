namespace DocConverter.Writers.Pptx
{
    using System.Collections.Generic;

    /// <summary>
    /// One physical slide after pagination.
    /// </summary>
    internal sealed class PptxSlidePage
    {
        internal string? Title { get; }

        internal string? Subtitle { get; set; }

        internal List<PptxUnit> Units { get; } = new List<PptxUnit>();

        internal long UsedHeight { get; set; }

        internal int BlockCount { get; set; }

        internal PptxSlidePage(string? title)
        {
            Title = title;
        }
    }
}
