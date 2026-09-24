namespace DocConverter.Writers.Pdf
{
    using System.Collections.Generic;

    /// <summary>
    /// Makes PDFsharp output reproducible by replacing its random parts with stable values of the same length, so the
    /// cross reference offsets stay valid: the six letter font subset prefixes ("/IDYIPM+Liberation...") become
    /// "/DCAAAA+", "/DCAAAB+", and so on, and every GUID written as text (XMP document and instance ids) becomes a
    /// stable GUID. Both are assigned in order of first appearance. Used only for deterministic output.
    /// </summary>
    internal static class PdfSubsetTagNormalizer
    {
        internal static void Normalize(byte[] pdf)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(System.StringComparer.Ordinal);
            for (int i = 0; i + 8 <= pdf.Length; i++)
            {
                if (pdf[i] != (byte)'/' || pdf[i + 7] != (byte)'+') continue;
                bool tag = true;
                for (int j = 1; j <= 6; j++)
                {
                    byte b = pdf[i + j];
                    if (b < (byte)'A' || b > (byte)'Z')
                    {
                        tag = false;
                        break;
                    }
                }

                if (!tag) continue;
                char[] chars = new char[6];
                for (int j = 0; j < 6; j++) chars[j] = (char)pdf[i + 1 + j];
                string original = new string(chars);
                if (!map.TryGetValue(original, out string? replacement))
                {
                    replacement = StableTag(map.Count);
                    map[original] = replacement;
                }

                for (int j = 0; j < 6; j++) pdf[i + 1 + j] = (byte)replacement[j];
                i += 7;
            }
        }

        internal static void NormalizeGuids(byte[] pdf)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(System.StringComparer.Ordinal);
            for (int i = 0; i + 36 <= pdf.Length; i++)
            {
                if (!IsGuidAt(pdf, i)) continue;
                if (i > 0 && IsHex(pdf[i - 1])) continue;
                if (i + 36 < pdf.Length && IsHex(pdf[i + 36])) continue;
                string original = System.Text.Encoding.ASCII.GetString(pdf, i, 36);
                if (!map.TryGetValue(original, out string? replacement))
                {
                    replacement = "00000000-0000-4000-8000-" + (map.Count + 1).ToString("x12", System.Globalization.CultureInfo.InvariantCulture);
                    map[original] = replacement;
                }

                for (int j = 0; j < 36; j++) pdf[i + j] = (byte)replacement[j];
                i += 35;
            }
        }

        private static bool IsGuidAt(byte[] d, int i)
        {
            for (int j = 0; j < 36; j++)
            {
                byte b = d[i + j];
                if (j == 8 || j == 13 || j == 18 || j == 23)
                {
                    if (b != (byte)'-') return false;
                }
                else if (!IsHex(b))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHex(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'f') || (b >= (byte)'A' && b <= (byte)'F');
        }

        private static string StableTag(int index)
        {
            char[] chars = new char[] { 'D', 'C', 'A', 'A', 'A', 'A' };
            int value = index;
            for (int position = 5; position >= 2 && value > 0; position--)
            {
                chars[position] = (char)('A' + value % 26);
                value /= 26;
            }

            return new string(chars);
        }
    }
}
