namespace DocConverter.Writers.Xlsx
{
    /// <summary>
    /// What the XLSX writer noticed while collecting content: images it must omit and formatting it must flatten.
    /// </summary>
    internal sealed class CollectState
    {
        internal int Images { get; set; } = 0;

        internal bool FormattingLost { get; set; } = false;
    }
}
