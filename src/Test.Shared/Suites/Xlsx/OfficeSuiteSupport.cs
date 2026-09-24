namespace Test.Shared.Suites.Xlsx
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using DocConverter.Internal;
    using DocConverter.Model;

    /// <summary>
    /// Model traversal helpers for the XLSX and PPTX suites.
    /// </summary>
    public static class OfficeSuiteSupport
    {
        /// <summary>
        /// Every block in the document, depth first, including blocks inside sections, lists, quotes and table cells.
        /// </summary>
        /// <param name="blocks">Root blocks.</param>
        /// <returns>All blocks.</returns>
        public static List<Block> Flatten(IEnumerable<Block> blocks)
        {
            List<Block> all = new List<Block>();
            Walk(blocks, all);
            return all;
        }

        /// <summary>
        /// Every block of a type.
        /// </summary>
        /// <typeparam name="T">Block type.</typeparam>
        /// <param name="document">Document.</param>
        /// <returns>Blocks.</returns>
        public static List<T> Find<T>(DocumentModel document) where T : Block
        {
            List<T> found = new List<T>();
            foreach (Block b in Flatten(document.Blocks))
                if (b is T t) found.Add(t);
            return found;
        }

        /// <summary>
        /// Plain text of a block.
        /// </summary>
        /// <param name="block">Block.</param>
        /// <returns>Text.</returns>
        public static string Text(Block block)
        {
            return ModelText.Block(block);
        }

        /// <summary>
        /// Cell texts of a table.
        /// </summary>
        /// <param name="table">Table.</param>
        /// <returns>Rows of cell text.</returns>
        public static List<List<string>> Cells(TableBlock table)
        {
            List<List<string>> rows = new List<List<string>>();
            foreach (TableRow row in table.Rows)
            {
                List<string> cells = new List<string>();
                foreach (TableCell cell in row.Cells) cells.Add(ModelText.Blocks(cell.Blocks, " "));
                rows.Add(cells);
            }

            return rows;
        }

        /// <summary>
        /// All text of a document.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Text.</returns>
        public static string AllText(DocumentModel document)
        {
            List<string> parts = new List<string>();
            foreach (Block b in document.Blocks) parts.Add(ModelText.Block(b));
            return string.Join("\n", parts);
        }

        /// <summary>
        /// Read an embedded fixture by its path under Fixtures/, tolerating resource names built with backslashes.
        /// </summary>
        /// <param name="relativePath">Path relative to the Fixtures folder, with forward slashes.</param>
        /// <returns>Fixture bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the fixture does not exist.</exception>
        public static byte[] Fixture(string relativePath)
        {
            string wanted = "Fixtures/" + relativePath.Replace('\\', '/');
            Assembly assembly = typeof(TestSupport).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.Replace('\\', '/') != wanted) continue;
                using (Stream? stream = assembly.GetManifestResourceStream(name))
                using (MemoryStream ms = new MemoryStream())
                {
                    if (stream == null) break;
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            throw new FileNotFoundException("Embedded fixture not found: " + wanted);
        }

        /// <summary>
        /// Describe how two zip packages differ: missing entries, changed content, or header-only differences.
        /// </summary>
        /// <param name="a">First package.</param>
        /// <param name="b">Second package.</param>
        /// <returns>Description, empty when the entries match.</returns>
        public static string DescribeZipDifference(byte[] a, byte[] b)
        {
            List<string> diffs = new List<string>();
            using (MemoryStream msa = new MemoryStream(a))
            using (MemoryStream msb = new MemoryStream(b))
            using (System.IO.Compression.ZipArchive za = new System.IO.Compression.ZipArchive(msa))
            using (System.IO.Compression.ZipArchive zb = new System.IO.Compression.ZipArchive(msb))
            {
                for (int i = 0; i < za.Entries.Count; i++)
                {
                    System.IO.Compression.ZipArchiveEntry ea = za.Entries[i];
                    System.IO.Compression.ZipArchiveEntry? eb = i < zb.Entries.Count ? zb.Entries[i] : null;
                    if (eb == null || eb.FullName != ea.FullName)
                    {
                        diffs.Add("entry " + i + ": " + ea.FullName + " vs " + (eb?.FullName ?? "(none)"));
                        continue;
                    }

                    string ta;
                    string tb;
                    using (StreamReader ra = new StreamReader(ea.Open())) ta = ra.ReadToEnd();
                    using (StreamReader rb = new StreamReader(eb.Open())) tb = rb.ReadToEnd();
                    if (ta != tb) diffs.Add(ea.FullName + " content: " + TestSupport.Truncate(ta, 300) + " /// " + TestSupport.Truncate(tb, 300));
                    else if (ea.LastWriteTime != eb.LastWriteTime) diffs.Add(ea.FullName + " time");
                }
            }

            return string.Join(" | ", diffs);
        }

        private static void Walk(IEnumerable<Block> blocks, List<Block> all)
        {
            foreach (Block block in blocks)
            {
                all.Add(block);
                switch (block)
                {
                    case SectionBlock s:
                        Walk(s.Blocks, all);
                        break;
                    case QuoteBlock q:
                        Walk(q.Blocks, all);
                        break;
                    case ListBlock l:
                        foreach (ListItemBlock item in l.Items)
                        {
                            all.Add(item);
                            Walk(item.Blocks, all);
                        }

                        break;
                    case TableBlock t:
                        foreach (TableRow row in t.Rows)
                            foreach (TableCell cell in row.Cells) Walk(cell.Blocks, all);
                        break;
                }
            }
        }
    }
}
