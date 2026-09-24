namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// Deduplicating shared string table builder.
    /// </summary>
    internal sealed class SharedStrings
    {
        private readonly Dictionary<string, int> _Index = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _Values = new List<string>();

        internal int Count
        {
            get => _Values.Count;
        }

        internal int Index(string text)
        {
            if (_Index.TryGetValue(text, out int index)) return index;
            index = _Values.Count;
            _Values.Add(text);
            _Index[text] = index;
            return index;
        }

        internal SharedStringTable Build()
        {
            SharedStringTable table = new SharedStringTable { Count = (uint)_Values.Count, UniqueCount = (uint)_Values.Count };
            foreach (string value in _Values)
            {
                Text text = new Text(value);
                if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]) || value.IndexOf('\n') >= 0))
                    text.Space = SpaceProcessingModeValues.Preserve;
                table.Append(new SharedStringItem(text));
            }

            return table;
        }
    }
}
