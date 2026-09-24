namespace DocConverter.Writers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Rewrites a zip package with every entry stamped 1980-01-01 00:00:00 in its original order, and renumbers the
    /// package level relationship ids (which System.IO.Packaging generates randomly and nothing else references), so
    /// identical content produces identical bytes.
    /// </summary>
    internal static class DocxDeterministicZip
    {
        private static readonly DateTimeOffset _Stamp = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Regex _RelationshipId = new Regex("Id=\"[^\"]*\"", RegexOptions.CultureInvariant);

        internal static byte[] Normalize(byte[] package)
        {
            List<string> names = new List<string>();
            List<byte[]> contents = new List<byte[]>();
            using (MemoryStream input = new MemoryStream(package, false))
            using (ZipArchive source = new ZipArchive(input, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in source.Entries)
                {
                    names.Add(entry.FullName);
                    using (Stream stream = entry.Open())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();
                        if (entry.FullName == "_rels/.rels") bytes = RenumberPackageRelationships(bytes);
                        contents.Add(bytes);
                    }
                }
            }

            using (MemoryStream output = new MemoryStream())
            {
                using (ZipArchive target = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    for (int i = 0; i < names.Count; i++)
                    {
                        ZipArchiveEntry entry = target.CreateEntry(names[i], CompressionLevel.Optimal);
                        entry.LastWriteTime = _Stamp;
                        using (Stream stream = entry.Open())
                        {
                            stream.Write(contents[i], 0, contents[i].Length);
                        }
                    }
                }

                return output.ToArray();
            }
        }

        private static byte[] RenumberPackageRelationships(byte[] xml)
        {
            string text = new UTF8Encoding(false).GetString(xml);
            int counter = 0;
            string renumbered = _RelationshipId.Replace(text, m => "Id=\"rIdPkg" + (++counter) + "\"");
            return new UTF8Encoding(false).GetBytes(renumbered);
        }
    }
}
