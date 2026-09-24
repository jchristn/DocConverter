namespace DocConverter.Internal
{
    using System.Text;

    /// <summary>
    /// Decodes text bytes, honoring a byte order mark first and falling back to a preferred encoding or UTF-8.
    /// </summary>
    internal static class TextEncodingDetector
    {
        internal static string Decode(byte[] data, int offset, int count, Encoding? preferred)
        {
            if (count <= 0) return "";

            if (count >= 4 && data[offset] == 0x00 && data[offset + 1] == 0x00 && data[offset + 2] == 0xFE && data[offset + 3] == 0xFF)
                return new UTF32Encoding(true, false).GetString(data, offset + 4, count - 4);
            if (count >= 4 && data[offset] == 0xFF && data[offset + 1] == 0xFE && data[offset + 2] == 0x00 && data[offset + 3] == 0x00)
                return new UTF32Encoding(false, false).GetString(data, offset + 4, count - 4);
            if (count >= 3 && data[offset] == 0xEF && data[offset + 1] == 0xBB && data[offset + 2] == 0xBF)
                return Encoding.UTF8.GetString(data, offset + 3, count - 3);
            if (count >= 2 && data[offset] == 0xFE && data[offset + 1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(data, offset + 2, count - 2);
            if (count >= 2 && data[offset] == 0xFF && data[offset + 1] == 0xFE)
                return Encoding.Unicode.GetString(data, offset + 2, count - 2);

            Encoding encoding = preferred ?? Encoding.UTF8;
            return encoding.GetString(data, offset, count);
        }

        internal static bool HasUnicodeBom(byte[] data, int offset, int count)
        {
            if (count >= 3 && data[offset] == 0xEF && data[offset + 1] == 0xBB && data[offset + 2] == 0xBF) return true;
            if (count >= 2 && data[offset] == 0xFE && data[offset + 1] == 0xFF) return true;
            if (count >= 2 && data[offset] == 0xFF && data[offset + 1] == 0xFE) return true;
            return false;
        }
    }
}
