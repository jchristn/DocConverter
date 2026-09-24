namespace DocConverter.Readers.Rtf
{
    using System;
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// An RTF field ({\field{\*\fldinst ...}{\fldrslt ...}}). HYPERLINK fields become links.
    /// </summary>
    internal sealed class RtfField
    {
        internal int Depth { get; }

        internal StringBuilder Instruction { get; } = new StringBuilder();

        internal LinkInline? Link { get; set; }

        internal RtfField(int depth)
        {
            Depth = depth;
        }

        /// <summary>
        /// The URL of a HYPERLINK field, or null for other fields. Local anchors (\l "name") become "#name".
        /// </summary>
        internal string? HyperlinkUrl
        {
            get
            {
                string instruction = Instruction.ToString().Trim();
                if (!instruction.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase)) return null;
                string rest = instruction.Substring(9).Trim();
                bool local = false;
                if (rest.StartsWith("\\l", StringComparison.Ordinal))
                {
                    local = true;
                    rest = rest.Substring(2).Trim();
                }

                string url;
                if (rest.StartsWith("\"", StringComparison.Ordinal))
                {
                    int end = rest.IndexOf('"', 1);
                    url = end > 0 ? rest.Substring(1, end - 1) : rest.Substring(1);
                }
                else
                {
                    int space = rest.IndexOf(' ');
                    url = space > 0 ? rest.Substring(0, space) : rest;
                }

                if (url.Length == 0) return null;
                return local ? "#" + url : url;
            }
        }
    }
}
