namespace DocConverter.Enums
{
    /// <summary>
    /// Document formats understood by DocConverter. The same enum names the source and the target of a conversion.
    /// </summary>
    public enum DocumentFormatEnum
    {
        /// <summary>
        /// Detect the format from the content (and a file name hint when one is available). Valid only as a source.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Plain text.
        /// </summary>
        Text,

        /// <summary>
        /// Markdown (CommonMark with GitHub flavored extensions).
        /// </summary>
        Markdown,

        /// <summary>
        /// HTML.
        /// </summary>
        Html,

        /// <summary>
        /// JSON. Either the canonical DocConverter document form or arbitrary JSON data.
        /// </summary>
        Json,

        /// <summary>
        /// XML. Either the canonical DocConverter document form or arbitrary XML data.
        /// </summary>
        Xml,

        /// <summary>
        /// Comma separated values.
        /// </summary>
        Csv,

        /// <summary>
        /// Tab separated values.
        /// </summary>
        Tsv,

        /// <summary>
        /// Rich Text Format. Supported as a source only.
        /// </summary>
        Rtf,

        /// <summary>
        /// Microsoft Word Open XML document (.docx).
        /// </summary>
        Docx,

        /// <summary>
        /// Microsoft Excel Open XML workbook (.xlsx).
        /// </summary>
        Xlsx,

        /// <summary>
        /// Microsoft PowerPoint Open XML presentation (.pptx).
        /// </summary>
        Pptx,

        /// <summary>
        /// Portable Document Format.
        /// </summary>
        Pdf,

        /// <summary>
        /// PNG image. Supported as a source only.
        /// </summary>
        Png,

        /// <summary>
        /// JPEG image. Supported as a source only.
        /// </summary>
        Jpeg,

        /// <summary>
        /// GIF image. Supported as a source only.
        /// </summary>
        Gif,

        /// <summary>
        /// BMP image. Supported as a source only.
        /// </summary>
        Bmp,

        /// <summary>
        /// TIFF image. Supported as a source only.
        /// </summary>
        Tiff,

        /// <summary>
        /// WebP image. Supported as a source only.
        /// </summary>
        WebP
    }
}
