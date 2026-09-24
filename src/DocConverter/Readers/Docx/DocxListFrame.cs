namespace DocConverter.Readers.Docx
{
    using DocConverter.Model;

    /// <summary>
    /// One open list while reading nested numbered paragraphs.
    /// </summary>
    internal sealed class DocxListFrame
    {
        internal ListBlock List { get; }

        internal int Level { get; }

        internal int NumId { get; }

        internal DocxListFrame(ListBlock list, int level, int numId)
        {
            List = list;
            Level = level;
            NumId = numId;
        }
    }
}
