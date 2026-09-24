namespace DocConverter.Enums
{
    /// <summary>
    /// Line ending used by text based writers.
    /// </summary>
    public enum LineEndingEnum
    {
        /// <summary>
        /// Line feed (\n). The default.
        /// </summary>
        Lf,

        /// <summary>
        /// Carriage return plus line feed (\r\n).
        /// </summary>
        CrLf,

        /// <summary>
        /// The line ending of the current platform (Environment.NewLine).
        /// </summary>
        Platform
    }
}
