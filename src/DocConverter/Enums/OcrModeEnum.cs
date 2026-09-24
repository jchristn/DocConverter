namespace DocConverter.Enums
{
    /// <summary>
    /// When optical character recognition runs. Reserved: OCR is not implemented in this release.
    /// </summary>
    public enum OcrModeEnum
    {
        /// <summary>
        /// Never run OCR. The default.
        /// </summary>
        Off,

        /// <summary>
        /// Run OCR on image sources and embedded images.
        /// </summary>
        ImagesOnly,

        /// <summary>
        /// Run OCR on PDF pages that have no text layer.
        /// </summary>
        PagesWithoutText,

        /// <summary>
        /// Run OCR on every image and every page without text.
        /// </summary>
        All
    }
}
