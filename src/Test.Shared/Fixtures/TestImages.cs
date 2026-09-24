namespace Test.Shared.Fixtures
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    /// <summary>
    /// Builds small images in memory without any imaging library, and loads the checked-in sample images.
    /// </summary>
    public static class TestImages
    {
        private static readonly uint[] _CrcTable = BuildCrcTable();

        /// <summary>
        /// A solid color RGB PNG.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="r">Red.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        /// <returns>PNG bytes.</returns>
        public static byte[] SolidPng(int width, int height, byte r, byte g, byte b)
        {
            byte[] raw = new byte[height * (1 + width * 3)];
            int p = 0;
            for (int y = 0; y < height; y++)
            {
                raw[p++] = 0;
                for (int x = 0; x < width; x++)
                {
                    raw[p++] = r;
                    raw[p++] = g;
                    raw[p++] = b;
                }
            }

            using (MemoryStream png = new MemoryStream())
            {
                png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
                byte[] ihdr = new byte[13];
                WriteBigEndian(ihdr, 0, (uint)width);
                WriteBigEndian(ihdr, 4, (uint)height);
                ihdr[8] = 8;
                ihdr[9] = 2;
                WriteChunk(png, "IHDR", ihdr);
                WriteChunk(png, "IDAT", Zlib(raw));
                WriteChunk(png, "IEND", new byte[0]);
                return png.ToArray();
            }
        }

        /// <summary>
        /// A checked-in sample image from Fixtures/Images, for example "sample.gif". All are 96 by 64 pixels.
        /// </summary>
        /// <param name="name">File name.</param>
        /// <returns>Image bytes.</returns>
        public static byte[] Sample(string name)
        {
            return TestSupport.Fixture("Images/" + name);
        }

        private static byte[] Zlib(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (DeflateStream deflate = new DeflateStream(ms, CompressionLevel.Optimal, true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                uint a = 1, b = 0;
                foreach (byte d in data)
                {
                    a = (a + d) % 65521;
                    b = (b + a) % 65521;
                }

                byte[] adler = new byte[4];
                WriteBigEndian(adler, 0, (b << 16) | a);
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            byte[] len = new byte[4];
            WriteBigEndian(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            foreach (byte x in typeBytes) crc = _CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            foreach (byte x in data) crc = _CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            byte[] crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFF);
            s.Write(crcBytes, 0, 4);
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }
    }
}
