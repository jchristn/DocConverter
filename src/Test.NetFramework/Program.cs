namespace Test.NetFramework
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Results;

    /// <summary>
    /// Smoke harness for .NET Framework consumers of the netstandard2.0 build. Runs one conversion through every reader
    /// and every writer and returns 0 when all succeed, 1 otherwise. Touchstone targets net8.0 and net10.0 only, so this
    /// harness is deliberately self-contained.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Unused.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            Converter converter = new Converter();
            List<string> failures = new List<string>();
            Console.WriteLine("DocConverter .NET Framework smoke test on " + Environment.Version);

            DocumentModel reference = BuildReference();
            Dictionary<DocumentFormatEnum, byte[]> written = new Dictionary<DocumentFormatEnum, byte[]>();
            foreach (DocumentFormatEnum target in converter.GetOutputFormats())
            {
                try
                {
                    BytesConversionResult r = await converter.WriteToBytesAsync(reference, target).ConfigureAwait(false);
                    if (r.Output.Length == 0) throw new InvalidOperationException("empty output");
                    written[target] = r.Output;
                    Console.WriteLine("  write " + target + ": " + r.Output.Length + " bytes");
                }
                catch (Exception ex)
                {
                    failures.Add("write " + target + ": " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            foreach (KeyValuePair<DocumentFormatEnum, byte[]> pair in written)
            {
                try
                {
                    StringConversionResult r = await converter.ConvertToStringAsync(pair.Value, DocumentFormatEnum.Auto, DocumentFormatEnum.Markdown).ConfigureAwait(false);
                    if (r.Output.IndexOf("Grace Hopper", StringComparison.Ordinal) < 0) throw new InvalidOperationException("table content missing after round trip");
                    Console.WriteLine("  read " + pair.Key + " (detected " + r.SourceFormat + "): ok");
                }
                catch (Exception ex)
                {
                    failures.Add("read " + pair.Key + ": " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            try
            {
                string rtf = "{\\rtf1\\ansi{\\fonttbl{\\f0 Arial;}}\\f0\\fs24 Hello \\b RTF\\b0  world.\\par}";
                StringConversionResult r = await converter.ConvertToStringAsync(Encoding.ASCII.GetBytes(rtf), DocumentFormatEnum.Rtf, DocumentFormatEnum.Text).ConfigureAwait(false);
                if (r.Output.IndexOf("RTF", StringComparison.Ordinal) < 0) throw new InvalidOperationException("rtf text missing");
                Console.WriteLine("  read Rtf: ok");
            }
            catch (Exception ex)
            {
                failures.Add("read Rtf: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAF0lEQVR4nGOQ9+shCTGMahgNpZ5hmzQA9FT5AfAHft4AAAAASUVORK5CYII=");
                BytesConversionResult r = await converter.ConvertToBytesAsync(png, DocumentFormatEnum.Png, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                if (r.Output.Length < 100) throw new InvalidOperationException("pdf too small");
                Console.WriteLine("  read Png, write Pdf: ok");
            }
            catch (Exception ex)
            {
                failures.Add("read Png: " + ex.GetType().Name + ": " + ex.Message);
            }

            foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
            Console.WriteLine(failures.Count == 0 ? "PASS" : "FAIL (" + failures.Count + ")");
            return failures.Count == 0 ? 0 : 1;
        }

        private static DocumentModel BuildReference()
        {
            DocumentModel m = new DocumentModel();
            m.Metadata.Title = "Smoke";
            m.Blocks.Add(new HeadingBlock(1, "Smoke Test"));
            m.Blocks.Add(new ParagraphBlock("A paragraph with Grüße."));
            TableBlock t = new TableBlock { HeaderRowCount = 1 };
            t.Rows.Add(new TableRow(new[] { "Name", "Role" }));
            t.Rows.Add(new TableRow(new[] { "Grace Hopper", "Admiral" }));
            m.Blocks.Add(t);
            ListBlock list = new ListBlock(ListKindEnum.Ordered);
            list.Items.Add(new ListItemBlock("one"));
            list.Items.Add(new ListItemBlock("two"));
            m.Blocks.Add(list);
            return m;
        }
    }
}
