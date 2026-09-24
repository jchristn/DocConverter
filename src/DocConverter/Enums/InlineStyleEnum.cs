namespace DocConverter.Enums
{
    using System;

    /// <summary>
    /// Inline text styles. Values combine as flags.
    /// </summary>
    [Flags]
    public enum InlineStyleEnum
    {
        /// <summary>
        /// No styling.
        /// </summary>
        None = 0,

        /// <summary>
        /// Bold.
        /// </summary>
        Bold = 1,

        /// <summary>
        /// Italic.
        /// </summary>
        Italic = 2,

        /// <summary>
        /// Underline.
        /// </summary>
        Underline = 4,

        /// <summary>
        /// Strikethrough.
        /// </summary>
        Strikethrough = 8,

        /// <summary>
        /// Inline code (monospace).
        /// </summary>
        Code = 16,

        /// <summary>
        /// Superscript.
        /// </summary>
        Superscript = 32,

        /// <summary>
        /// Subscript.
        /// </summary>
        Subscript = 64
    }
}
