namespace DocConverter.Enums
{
    /// <summary>
    /// What a CSV or TSV writer does when the document contains no tables.
    /// </summary>
    public enum NoTableBehaviorEnum
    {
        /// <summary>
        /// Write one row per block in a single column and raise a warning.
        /// </summary>
        ParagraphsAsRows,

        /// <summary>
        /// Throw a DocumentWriteException.
        /// </summary>
        Error
    }
}
