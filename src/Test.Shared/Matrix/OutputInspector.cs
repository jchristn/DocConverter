namespace Test.Shared.Matrix
{
    using System;
    using DocConverter.Enums;
    using Test.Shared.Inspection;

    /// <summary>
    /// Dispatches converted output to the independent inspector for its format.
    /// </summary>
    public static class OutputInspector
    {
        /// <summary>
        /// Validate and snapshot output of the given format.
        /// </summary>
        /// <param name="format">Output format.</param>
        /// <param name="bytes">Output bytes.</param>
        /// <returns>Snapshot.</returns>
        /// <exception cref="ArgumentException">Thrown for formats without an inspector.</exception>
        public static ContentSnapshot Inspect(DocumentFormatEnum format, byte[] bytes)
        {
            switch (format)
            {
                case DocumentFormatEnum.Markdown: return MarkdownInspector.Inspect(bytes);
                case DocumentFormatEnum.Html: return HtmlInspector.Inspect(bytes);
                case DocumentFormatEnum.Text: return TextInspector.Inspect(bytes);
                case DocumentFormatEnum.Json: return CanonicalInspector.InspectJson(bytes);
                case DocumentFormatEnum.Xml: return CanonicalInspector.InspectXml(bytes);
                case DocumentFormatEnum.Csv: return DelimitedInspector.Inspect(bytes, ',');
                case DocumentFormatEnum.Tsv: return DelimitedInspector.Inspect(bytes, '\t');
                case DocumentFormatEnum.Docx: return DocxInspector.Inspect(bytes);
                case DocumentFormatEnum.Xlsx: return XlsxInspector.Inspect(bytes);
                case DocumentFormatEnum.Pptx: return PptxInspector.Inspect(bytes);
                case DocumentFormatEnum.Pdf: return PdfInspector.Inspect(bytes);
                default: throw new ArgumentException("No inspector for " + format + ".", nameof(format));
            }
        }
    }
}
