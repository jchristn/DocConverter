namespace DocConverter.Readers.Docx
{
    /// <summary>
    /// A numbering reference: numbering instance id and list level.
    /// </summary>
    internal sealed class DocxNumberingRef
    {
        internal int NumId { get; }

        internal int Level { get; }

        internal DocxNumberingRef(int numId, int level)
        {
            NumId = numId;
            Level = level < 0 ? 0 : level;
        }
    }
}
