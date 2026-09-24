namespace DocConverter.Internal
{
    using System.Text;

    /// <summary>
    /// Converts between A1 style cell references and zero based row and column indexes.
    /// </summary>
    internal static class SpreadsheetCellReference
    {
        internal static int ColumnIndex(string? reference)
        {
            if (string.IsNullOrEmpty(reference)) return -1;
            int index = 0;
            bool any = false;
            foreach (char raw in reference!)
            {
                char c = char.ToUpperInvariant(raw);
                if (c < 'A' || c > 'Z') break;
                index = index * 26 + (c - 'A' + 1);
                any = true;
            }

            return any ? index - 1 : -1;
        }

        internal static int RowIndex(string? reference)
        {
            if (string.IsNullOrEmpty(reference)) return -1;
            int i = 0;
            while (i < reference!.Length && char.IsLetter(reference[i])) i++;
            int row = 0;
            bool any = false;
            for (; i < reference.Length; i++)
            {
                char c = reference[i];
                if (c < '0' || c > '9') break;
                row = row * 10 + (c - '0');
                any = true;
            }

            return any ? row - 1 : -1;
        }

        internal static string ColumnName(int columnIndex)
        {
            StringBuilder sb = new StringBuilder();
            int n = columnIndex + 1;
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                n = (n - 1) / 26;
            }

            return sb.ToString();
        }

        internal static string Reference(int rowIndex, int columnIndex)
        {
            return ColumnName(columnIndex) + (rowIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
