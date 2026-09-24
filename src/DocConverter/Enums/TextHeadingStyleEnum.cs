namespace DocConverter.Enums
{
    /// <summary>
    /// How the plain text writer marks headings.
    /// </summary>
    public enum TextHeadingStyleEnum
    {
        /// <summary>
        /// Level 1 and 2 headings are underlined with = and -; deeper levels are written as is.
        /// </summary>
        Underline,

        /// <summary>
        /// Headings are written in upper case.
        /// </summary>
        Uppercase,

        /// <summary>
        /// Headings are written as ordinary lines.
        /// </summary>
        None
    }
}
