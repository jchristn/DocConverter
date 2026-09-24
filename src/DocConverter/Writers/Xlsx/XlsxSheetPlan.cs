namespace DocConverter.Writers.Xlsx
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// One worksheet to write: either a table or the rows of the leading "Document" sheet.
    /// </summary>
    internal sealed class XlsxSheetPlan
    {
        internal string? RequestedName { get; }

        internal TableBlock? Table { get; }

        internal List<string> Lines { get; } = new List<string>();

        internal XlsxSheetPlan(string? requestedName, TableBlock? table)
        {
            RequestedName = requestedName;
            Table = table;
        }
    }
}
