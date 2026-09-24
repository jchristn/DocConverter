namespace DocConverter.Readers.Docx
{
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// A text box found while reading, with the part that owns its relationships.
    /// </summary>
    internal sealed class DocxTextBox
    {
        internal W.TextBoxContent Content { get; }

        internal OpenXmlPart Owner { get; }

        internal DocxTextBox(W.TextBoxContent content, OpenXmlPart owner)
        {
            Content = content;
            Owner = owner;
        }
    }
}
