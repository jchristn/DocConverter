namespace DocConverter.Readers.Xlsx
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Decides whether a number format displays a date or time, and renders serial date values as ISO 8601.
    /// </summary>
    internal static class XlsxDateFormats
    {
        internal static bool IsBuiltInDate(uint numberFormatId)
        {
            return (numberFormatId >= 14 && numberFormatId <= 22)
                || (numberFormatId >= 27 && numberFormatId <= 36)
                || (numberFormatId >= 45 && numberFormatId <= 47)
                || (numberFormatId >= 50 && numberFormatId <= 58);
        }

        internal static bool IsDateFormatCode(string? code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            string section = code!;
            int semicolon = section.IndexOf(';');
            if (semicolon >= 0) section = section.Substring(0, semicolon);

            StringBuilder sb = new StringBuilder();
            bool quoted = false;
            bool bracket = false;
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (quoted)
                {
                    if (c == '"') quoted = false;
                    continue;
                }

                if (bracket)
                {
                    if (c == ']') bracket = false;
                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                    continue;
                }

                if (c == '[')
                {
                    string rest = section.Substring(i + 1).ToLowerInvariant();
                    if (rest.StartsWith("h", StringComparison.Ordinal) || rest.StartsWith("m", StringComparison.Ordinal) || rest.StartsWith("s", StringComparison.Ordinal))
                        return true;
                    bracket = true;
                    continue;
                }

                if (c == '\\' || c == '_' || c == '*')
                {
                    i++;
                    continue;
                }

                sb.Append(char.ToLowerInvariant(c));
            }

            string stripped = sb.ToString();
            if (stripped == "general") return false;
            foreach (char c in stripped)
                if (c == 'y' || c == 'd' || c == 'h' || c == 's' || c == 'm') return true;
            return false;
        }

        internal static string FromSerial(double serial)
        {
            if (serial < 0 || serial > 2958465.99999) return serial.ToString("G15", CultureInfo.InvariantCulture);
            DateTime value = DateTime.FromOADate(serial);
            bool hasDate = serial >= 1.0;
            bool hasTime = value.TimeOfDay.TotalSeconds >= 0.5;
            if (!hasDate) return value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (!hasTime) return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
