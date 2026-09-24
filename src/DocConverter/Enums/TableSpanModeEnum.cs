namespace DocConverter.Enums
{
    /// <summary>
    /// How writers without merged-cell support render spanned cells.
    /// </summary>
    public enum TableSpanModeEnum
    {
        /// <summary>
        /// Repeat the spanned cell's text in every covered position.
        /// </summary>
        Repeat,

        /// <summary>
        /// Leave covered positions empty.
        /// </summary>
        Empty
    }
}
