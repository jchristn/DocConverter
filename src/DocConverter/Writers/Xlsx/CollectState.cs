namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// What the XLSX writer noticed while collecting content: images it must omit or replace with placeholder rows, and
    /// formatting it must flatten.
    /// </summary>
    internal sealed class CollectState
    {
        internal Dictionary<string, BinaryResource> Resources { get; set; } = new Dictionary<string, BinaryResource>(StringComparer.Ordinal);

        internal int Images { get; set; } = 0;

        internal int Placeholders { get; set; } = 0;

        internal bool FormattingLost { get; set; } = false;
    }
}
