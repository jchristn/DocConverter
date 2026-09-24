namespace DocConverter.Enums
{
    /// <summary>
    /// Which tables a CSV or TSV writer emits.
    /// </summary>
    public enum TableSelectionEnum
    {
        /// <summary>
        /// The first table only.
        /// </summary>
        First,

        /// <summary>
        /// Every table, separated by one empty record.
        /// </summary>
        All,

        /// <summary>
        /// The table at CsvOptions.TableIndex.
        /// </summary>
        Index
    }
}
