namespace DocConverter.Readers.Docx
{
    using DocConverter.Model;

    /// <summary>
    /// An image found in a paragraph, with its display size in points when the drawing declared one.
    /// </summary>
    internal sealed class DocxImageInfo
    {
        internal ImageInline Inline { get; }

        internal double? Width { get; }

        internal double? Height { get; }

        internal DocxImageInfo(ImageInline inline, double? width, double? height)
        {
            Inline = inline;
            Width = width;
            Height = height;
        }
    }
}
