namespace DocConverter.Writers.Pdf
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Character coverage of a TrueType font, read from its cmap table (formats 4 and 12). Immutable after
    /// construction and therefore thread safe.
    /// </summary>
    internal sealed class TrueTypeCmap
    {
        private static readonly object _Lock = new object();
        private static TrueTypeCmap? _Sans = null;
        private static TrueTypeCmap? _Mono = null;

        private readonly BitArray _Bmp = new BitArray(65536);
        private readonly List<long[]> _Groups = new List<long[]>();

        internal static TrueTypeCmap Sans
        {
            get
            {
                lock (_Lock)
                {
                    if (_Sans == null) _Sans = new TrueTypeCmap(EmbeddedFonts.Load("LiberationSans-Regular"));
                    return _Sans;
                }
            }
        }

        internal static TrueTypeCmap Mono
        {
            get
            {
                lock (_Lock)
                {
                    if (_Mono == null) _Mono = new TrueTypeCmap(EmbeddedFonts.Load("LiberationMono-Regular"));
                    return _Mono;
                }
            }
        }

        internal TrueTypeCmap(byte[] font)
        {
            if (font == null) throw new ArgumentNullException(nameof(font));
            int numTables = U16(font, 4);
            int cmapOffset = -1;
            for (int i = 0; i < numTables; i++)
            {
                int record = 12 + i * 16;
                if (record + 16 > font.Length) break;
                if (font[record] == 'c' && font[record + 1] == 'm' && font[record + 2] == 'a' && font[record + 3] == 'p')
                {
                    cmapOffset = (int)U32(font, record + 8);
                    break;
                }
            }

            if (cmapOffset < 0) throw new InvalidOperationException("The font has no cmap table.");

            int subtables = U16(font, cmapOffset + 2);
            int format4 = -1;
            int format12 = -1;
            for (int i = 0; i < subtables; i++)
            {
                int record = cmapOffset + 4 + i * 8;
                int platform = U16(font, record);
                int encoding = U16(font, record + 2);
                int offset = cmapOffset + (int)U32(font, record + 4);
                int format = U16(font, offset);
                if (format == 12 && (platform == 3 || platform == 0)) format12 = offset;
                else if (format == 4 && ((platform == 3 && encoding == 1) || platform == 0) && format4 < 0) format4 = offset;
            }

            if (format4 >= 0) ReadFormat4(font, format4);
            if (format12 >= 0) ReadFormat12(font, format12);
        }

        internal bool Contains(int codePoint)
        {
            if (codePoint >= 0 && codePoint < 65536 && _Bmp[codePoint]) return true;
            foreach (long[] group in _Groups)
            {
                if (codePoint >= group[0] && codePoint <= group[1]) return true;
            }

            return false;
        }

        private void ReadFormat4(byte[] f, int offset)
        {
            int segCount = U16(f, offset + 6) / 2;
            int endCodes = offset + 14;
            int startCodes = endCodes + segCount * 2 + 2;
            int deltas = startCodes + segCount * 2;
            int rangeOffsets = deltas + segCount * 2;
            for (int s = 0; s < segCount; s++)
            {
                int end = U16(f, endCodes + s * 2);
                int start = U16(f, startCodes + s * 2);
                int delta = (short)U16(f, deltas + s * 2);
                int rangeOffsetAddress = rangeOffsets + s * 2;
                int rangeOffset = U16(f, rangeOffsetAddress);
                if (start > end) continue;
                for (int c = start; c <= end && c < 0xFFFF; c++)
                {
                    int glyph;
                    if (rangeOffset == 0)
                    {
                        glyph = (c + delta) & 0xFFFF;
                    }
                    else
                    {
                        int address = rangeOffsetAddress + rangeOffset + 2 * (c - start);
                        if (address + 2 > f.Length) continue;
                        glyph = U16(f, address);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }

                    if (glyph != 0) _Bmp[c] = true;
                }
            }
        }

        private void ReadFormat12(byte[] f, int offset)
        {
            long groups = U32(f, offset + 12);
            for (long g = 0; g < groups; g++)
            {
                int record = offset + 16 + (int)g * 12;
                if (record + 12 > f.Length) break;
                long start = U32(f, record);
                long end = U32(f, record + 4);
                long startGlyph = U32(f, record + 8);
                if (end < 65536)
                {
                    for (long c = start; c <= end; c++)
                        if (startGlyph + (c - start) != 0) _Bmp[(int)c] = true;
                }
                else
                {
                    _Groups.Add(new long[] { start, end });
                }
            }
        }

        private static int U16(byte[] d, int o)
        {
            if (o + 2 > d.Length) return 0;
            return (d[o] << 8) | d[o + 1];
        }

        private static long U32(byte[] d, int o)
        {
            if (o + 4 > d.Length) return 0;
            return ((long)d[o] << 24) | ((long)d[o + 1] << 16) | ((long)d[o + 2] << 8) | d[o + 3];
        }
    }
}
