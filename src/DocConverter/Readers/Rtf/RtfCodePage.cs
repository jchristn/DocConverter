namespace DocConverter.Readers.Rtf
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Decodes 8-bit RTF text for a code page. Windows-1252 (the RTF default) is built in, because netstandard2.0 and
    /// .NET without the code pages provider do not include it. UTF-8 and ISO-8859-1 are supported; any other code page is
    /// tried through Encoding.GetEncoding and otherwise decoded as Windows-1252.
    /// </summary>
    internal static class RtfCodePage
    {
        private static readonly char[] _Windows1252High = new char[]
        {
            '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
            '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
            '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
            '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178'
        };

        internal static string Decode(List<byte> bytes, int codePage)
        {
            if (bytes.Count == 0) return "";
            byte[] array = bytes.ToArray();
            if (codePage == 65001) return new UTF8Encoding(false).GetString(array);
            if (codePage == 28591) return Latin1(array);
            if (codePage == 1252 || codePage <= 0) return Windows1252(array);

            try
            {
                Encoding encoding = Encoding.GetEncoding(codePage);
                return encoding.GetString(array);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return Windows1252(array);
            }
        }

        private static string Windows1252(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length);
            foreach (byte b in bytes)
            {
                if (b >= 0x80 && b <= 0x9F) sb.Append(_Windows1252High[b - 0x80]);
                else sb.Append((char)b);
            }

            return sb.ToString();
        }

        private static string Latin1(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length);
            foreach (byte b in bytes) sb.Append((char)b);
            return sb.ToString();
        }
    }
}
