namespace DocConverter.Readers.Pdf
{
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Recognizes list markers at the start of a PDF line: bullets ("\u2022", "-", "*", "\u25E6", "\u25AA", "\u2023", "\u2043", "\u2013") and
    /// numbering ("1.", "2)", "a.", "(iv)").
    /// </summary>
    internal static class PdfListMarker
    {
        private static readonly Regex _Numbered = new Regex(@"^(\d{1,4})[.)]$", RegexOptions.Compiled);
        private static readonly Regex _Lettered = new Regex(@"^[a-zA-Z][.)]$", RegexOptions.Compiled);
        private static readonly Regex _Parenthesized = new Regex(@"^\(([0-9a-zA-Z]{1,4})\)$", RegexOptions.Compiled);

        internal static bool IsBullet(string text)
        {
            switch (text)
            {
                case "\u2022":
                case "\u25E6":
                case "\u25AA":
                case "\u2023":
                case "\u2043":
                case "\u2013":
                case "\u00B7":
                case "-":
                case "*":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True when the text is a marker. Ordered is set for numbered or lettered markers; number is the parsed number
        /// for numeric markers and 1 otherwise.
        /// </summary>
        internal static bool TryParse(string text, out bool ordered, out int number)
        {
            ordered = false;
            number = 1;
            if (string.IsNullOrEmpty(text)) return false;
            if (IsBullet(text)) return true;

            Match m = _Numbered.Match(text);
            if (m.Success)
            {
                ordered = true;
                number = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return true;
            }

            if (_Lettered.IsMatch(text) || _Parenthesized.IsMatch(text))
            {
                ordered = true;
                return true;
            }

            return false;
        }
    }
}
