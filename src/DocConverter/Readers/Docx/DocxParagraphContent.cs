namespace DocConverter.Readers.Docx
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// Inline content read from one Word paragraph. Page breaks inside the paragraph split it into segments.
    /// </summary>
    internal sealed class DocxParagraphContent
    {
        internal List<List<Inline>> Segments { get; } = new List<List<Inline>>();

        internal List<DocxImageInfo> Images { get; } = new List<DocxImageInfo>();

        internal int MonospaceChars { get; set; } = 0;

        internal int ProportionalChars { get; set; } = 0;

        internal DocxParagraphContent()
        {
            Segments.Add(new List<Inline>());
        }

        internal List<Inline> Current
        {
            get => Segments[Segments.Count - 1];
        }

        internal bool IsAllMonospace
        {
            get => MonospaceChars > 0 && ProportionalChars == 0;
        }

        internal void BreakPage()
        {
            Segments.Add(new List<Inline>());
        }

        internal void CountText(string text, bool monospace)
        {
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (monospace) MonospaceChars++;
                else ProportionalChars++;
            }
        }

        internal DocxImageInfo? FindImage(ImageInline inline)
        {
            foreach (DocxImageInfo info in Images)
                if (ReferenceEquals(info.Inline, inline)) return info;
            return null;
        }
    }
}
