namespace DocConverter.Enums
{
    /// <summary>
    /// Kind of section block.
    /// </summary>
    public enum SectionKindEnum
    {
        /// <summary>
        /// A generic grouping, for example nested JSON or XML data.
        /// </summary>
        Generic,

        /// <summary>
        /// A page of a paged source such as PDF.
        /// </summary>
        Page,

        /// <summary>
        /// A presentation slide.
        /// </summary>
        Slide,

        /// <summary>
        /// A spreadsheet worksheet.
        /// </summary>
        Sheet
    }
}
