namespace Test.Shared.Fixtures
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    /// <summary>
    /// Builds malformed, legacy, hostile and edge-case inputs in code.
    /// </summary>
    public static class NegativeFixtures
    {
        /// <summary>
        /// A zip archive with the given entries, passed as alternating name and content strings. Entry times are fixed.
        /// </summary>
        /// <param name="nameContentPairs">Name, content, name, content, and so on.</param>
        /// <returns>Zip bytes.</returns>
        /// <exception cref="ArgumentException">Thrown when the number of strings is odd.</exception>
        public static byte[] Zip(params string[] nameContentPairs)
        {
            if (nameContentPairs.Length % 2 != 0) throw new ArgumentException("Pass name and content pairs.", nameof(nameContentPairs));
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    for (int i = 0; i < nameContentPairs.Length; i += 2)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(nameContentPairs[i]);
                        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        using (Stream s = entry.Open())
                        {
                            byte[] content = Encoding.UTF8.GetBytes(nameContentPairs[i + 1]);
                            s.Write(content, 0, content.Length);
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// A minimal OLE2 compound file header followed by a directory-like region naming one stream in UTF-16LE. Enough
        /// for signature and stream-name classification (legacy .doc, .xls, .ppt, encrypted OOXML, .msg).
        /// </summary>
        /// <param name="streamName">Stream name to embed, for example "WordDocument".</param>
        /// <returns>Bytes.</returns>
        public static byte[] Ole(string streamName)
        {
            byte[] data = new byte[2048];
            byte[] signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            Buffer.BlockCopy(signature, 0, data, 0, signature.Length);
            data[0x1A] = 0x3E;
            data[0x1C] = 0xFE;
            data[0x1D] = 0xFF;
            data[0x1E] = 0x09;
            byte[] name = Encoding.Unicode.GetBytes("Root Entry");
            Buffer.BlockCopy(name, 0, data, 512, name.Length);
            byte[] stream = Encoding.Unicode.GetBytes(streamName);
            Buffer.BlockCopy(stream, 0, data, 640, stream.Length);
            return data;
        }

        /// <summary>
        /// A zip that is cut off before its central directory.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] TruncatedZip()
        {
            byte[] zip = Zip("[Content_Types].xml", "<Types/>", "word/document.xml", new string('x', 4000));
            byte[] cut = new byte[zip.Length / 2];
            Buffer.BlockCopy(zip, 0, cut, 0, cut.Length);
            return cut;
        }

        /// <summary>
        /// Random bytes from a fixed seed.
        /// </summary>
        /// <param name="length">Length.</param>
        /// <returns>Bytes.</returns>
        public static byte[] Random(int length)
        {
            byte[] data = new byte[length];
            new System.Random(20260924).NextBytes(data);
            return data;
        }

        /// <summary>
        /// JSON nested the given number of levels deep.
        /// </summary>
        /// <param name="depth">Depth.</param>
        /// <returns>JSON text.</returns>
        public static string DeepJson(int depth)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < depth; i++) sb.Append("{\"a\":");
            sb.Append('1');
            for (int i = 0; i < depth; i++) sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// XML with a DTD that declares an external entity pointing at a file. A safe reader never resolves it.
        /// </summary>
        /// <param name="targetPath">File the entity points at.</param>
        /// <returns>XML text.</returns>
        public static string XxeXml(string targetPath)
        {
            string uri = new Uri(targetPath).AbsoluteUri;
            return "<?xml version=\"1.0\"?>\n<!DOCTYPE root [<!ENTITY xxe SYSTEM \"" + uri + "\">]>\n<root><secret>&xxe;</secret></root>";
        }

        /// <summary>
        /// XML with the billion laughs entity expansion pattern.
        /// </summary>
        /// <returns>XML text.</returns>
        public static string BillionLaughsXml()
        {
            StringBuilder sb = new StringBuilder("<?xml version=\"1.0\"?>\n<!DOCTYPE lolz [\n<!ENTITY lol \"lol\">\n");
            for (int i = 1; i <= 9; i++)
            {
                sb.Append("<!ENTITY lol").Append(i).Append(" \"");
                for (int j = 0; j < 10; j++) sb.Append("&lol").Append(i == 1 ? "" : (i - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                sb.Append("\">\n");
            }

            sb.Append("]>\n<lolz>&lol9;</lolz>");
            return sb.ToString();
        }
    }
}
