namespace DocConverter.Readers.Pptx
{
    using DocumentFormat.OpenXml;

    /// <summary>
    /// A slide element with its position, used to order content top to bottom, then left to right.
    /// </summary>
    internal sealed class PptxShapeItem
    {
        internal OpenXmlElement Element { get; }

        internal long X { get; }

        internal long Y { get; }

        internal int Order { get; }

        internal PptxShapeItem(OpenXmlElement element, long x, long y, int order)
        {
            Element = element;
            X = x;
            Y = y;
            Order = order;
        }
    }
}
