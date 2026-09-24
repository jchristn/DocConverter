namespace DocConverter.Readers.Pdf
{
    using DocConverter.Model;

    /// <summary>
    /// A block recovered from a PDF page with the vertical position used to order it (PDF coordinates, larger is higher).
    /// </summary>
    internal sealed class PdfPageElement
    {
        internal double Top { get; }

        internal Block Block { get; }

        internal PdfPageElement(double top, Block block)
        {
            Top = top;
            Block = block;
        }
    }
}
