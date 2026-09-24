namespace DocConverter.Writers.Pptx
{
    /// <summary>
    /// Kind of content unit placed on a slide.
    /// </summary>
    internal enum PptxUnitKind
    {
        /// <summary>
        /// A text box holding one or more text blocks.
        /// </summary>
        Text,

        /// <summary>
        /// A table, or a chunk of one.
        /// </summary>
        Table,

        /// <summary>
        /// A picture.
        /// </summary>
        Picture
    }
}
