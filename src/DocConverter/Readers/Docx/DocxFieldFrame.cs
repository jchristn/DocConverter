namespace DocConverter.Readers.Docx
{
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// One open complex field (fldChar begin ... separate ... end). HYPERLINK fields become links.
    /// </summary>
    internal sealed class DocxFieldFrame
    {
        internal StringBuilder Instruction { get; } = new StringBuilder();

        internal bool Separated { get; set; } = false;

        internal LinkInline? Link { get; set; } = null;
    }
}
