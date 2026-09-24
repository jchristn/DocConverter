namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The inferred type of a cell's text: number, boolean, date, date and time, or plain text.
    /// </summary>
    internal sealed class XlsxTypedValue
    {
        private static readonly Regex _Number = new Regex(@"^[-+]?(\d+(\.\d*)?|\.\d+)([eE][-+]?\d+)?$", RegexOptions.Compiled);
        private static readonly Regex _Date = new Regex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
        private static readonly Regex _DateTime = new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$", RegexOptions.Compiled);

        internal bool IsNumber { get; private set; }

        internal bool IsBoolean { get; private set; }

        internal bool IsDate { get; private set; }

        internal bool IsDateTime { get; private set; }

        internal double Number { get; private set; }

        internal bool Boolean { get; private set; }

        internal static XlsxTypedValue Infer(string text)
        {
            XlsxTypedValue value = new XlsxTypedValue();
            if (string.IsNullOrEmpty(text) || text.Trim() != text) return value;

            if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                value.IsBoolean = true;
                value.Boolean = string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase);
                return value;
            }

            if (_Number.IsMatch(text))
            {
                string unsigned = text.TrimStart('-', '+');
                bool leadingZero = unsigned.Length > 1 && unsigned[0] == '0' && unsigned[1] != '.';
                int digits = 0;
                foreach (char c in unsigned)
                {
                    if (c == 'e' || c == 'E') break;
                    if (c >= '0' && c <= '9') digits++;
                }

                if (!leadingZero && digits <= 15 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsInfinity(number))
                {
                    value.IsNumber = true;
                    value.Number = number;
                }

                return value;
            }

            if (_Date.IsMatch(text) && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) && date.Year >= 1900)
            {
                value.IsDate = true;
                value.Number = date.ToOADate();
                return value;
            }

            if (_DateTime.IsMatch(text)
                && DateTime.TryParseExact(text, new string[] { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime)
                && dateTime.Year >= 1900)
            {
                value.IsDateTime = true;
                value.Number = dateTime.ToOADate();
            }

            return value;
        }
    }
}
