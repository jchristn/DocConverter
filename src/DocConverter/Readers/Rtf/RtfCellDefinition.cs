namespace DocConverter.Readers.Rtf
{
    /// <summary>
    /// A cell boundary from a table row definition (\cellx) with its merge flags.
    /// </summary>
    internal sealed class RtfCellDefinition
    {
        internal bool MergeFirst { get; set; }

        internal bool MergeContinue { get; set; }

        internal bool VerticalMergeFirst { get; set; }

        internal bool VerticalMergeContinue { get; set; }
    }
}
