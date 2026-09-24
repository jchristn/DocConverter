namespace DocConverter.Detection
{
    using System.Text;

    /// <summary>
    /// Classifies OLE2 compound files (legacy Office binaries, password protected Office Open XML, Outlook messages) by
    /// looking for their well known stream names, which the compound file directory stores in UTF-16LE.
    /// </summary>
    internal static class OleDirectoryInspector
    {
        internal static bool IsOle(byte[] d, int offset, int length)
        {
            return length >= 8
                && d[offset] == 0xD0 && d[offset + 1] == 0xCF && d[offset + 2] == 0x11 && d[offset + 3] == 0xE0
                && d[offset + 4] == 0xA1 && d[offset + 5] == 0xB1 && d[offset + 6] == 0x1A && d[offset + 7] == 0xE1;
        }

        internal static string Describe(byte[] d, int offset, int length)
        {
            if (Contains(d, offset, length, "EncryptedPackage")) return "password protected Office document (encrypted Office Open XML)";
            if (Contains(d, offset, length, "WordDocument")) return "legacy Word 97-2003 document (.doc)";
            if (Contains(d, offset, length, "PowerPoint Document")) return "legacy PowerPoint 97-2003 presentation (.ppt)";
            if (Contains(d, offset, length, "Workbook") || Contains(d, offset, length, "Book")) return "legacy Excel 97-2003 workbook (.xls)";
            if (Contains(d, offset, length, "__substg1.0_")) return "Outlook message (.msg)";
            return "OLE2 compound file";
        }

        internal static bool IsEncryptedOoxml(byte[] d, int offset, int length)
        {
            return IsOle(d, offset, length) && Contains(d, offset, length, "EncryptedPackage");
        }

        private static bool Contains(byte[] d, int offset, int length, string name)
        {
            byte[] pattern = Encoding.Unicode.GetBytes(name);
            int end = offset + length - pattern.Length;
            for (int i = offset; i <= end; i++)
            {
                if (d[i] != pattern[0]) continue;
                bool match = true;
                for (int j = 1; j < pattern.Length; j++)
                {
                    if (d[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return true;
            }

            return false;
        }
    }
}
