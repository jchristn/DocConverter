namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Options;
    using Test.Shared.Matrix;
    using Touchstone.Core;

    /// <summary>
    /// Byte-exact regression tests: every matrix source converted to every text based target, with Deterministic set,
    /// compared with the checked-in golden files in Fixtures/Golden (line endings normalized). Regenerate with
    /// dotnet run --project src/Test.Automated -- --update-golden src/Test.Shared/Fixtures/Golden and review the diff.
    /// </summary>
    public static class GoldenSuite
    {
        private static readonly DocumentFormatEnum[] _TextTargets = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json,
            DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv
        };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Golden", "Golden files");
            Converter c = new Converter();
            foreach (SourceSpec source in MatrixCatalog.Sources)
            {
                foreach (DocumentFormatEnum target in _TextTargets)
                {
                    SourceSpec src = source;
                    DocumentFormatEnum to = target;
                    string name = FileName(src, to);
                    s.Add(src.Id + "_" + to, src.Id + " to " + to + " matches " + name, async ct =>
                    {
                        string actual = await Render(c, src, to, ct).ConfigureAwait(false);
                        TestSupport.Assert(TestSupport.FixtureExists("Golden/" + name), "golden file missing: " + name + " (regenerate with --update-golden)");
                        string expected = TestSupport.FixtureText("Golden/" + name).Replace("\r\n", "\n");
                        if (expected != actual)
                        {
                            int i = 0;
                            while (i < expected.Length && i < actual.Length && expected[i] == actual[i]) i++;
                            throw new TestAssertionException(name + " differs at character " + i + ": expected '" + Excerpt(expected, i) + "', got '" + Excerpt(actual, i) + "'");
                        }
                    });
                }
            }

            return s.Build();
        }

        /// <summary>
        /// Produce every golden file in memory, keyed by file name. Test.Automated writes these when asked to update.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>File name to UTF-8 content.</returns>
        public static async Task<Dictionary<string, byte[]>> GenerateAsync(CancellationToken token)
        {
            Converter c = new Converter();
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            foreach (SourceSpec source in MatrixCatalog.Sources)
                foreach (DocumentFormatEnum target in _TextTargets)
                    files[FileName(source, target)] = new UTF8Encoding(false).GetBytes(await Render(c, source, target, token).ConfigureAwait(false));
            return files;
        }

        private static async Task<string> Render(Converter c, SourceSpec source, DocumentFormatEnum target, CancellationToken token)
        {
            ConversionOptions options = new ConversionOptions { Deterministic = true };
            string output = (await c.ConvertToStringAsync(source.Bytes(), source.Format, target, options, token).ConfigureAwait(false)).Output;
            return NormalizeImages(output.Replace("\r\n", "\n"));
        }

        // Golden files pin text and structure. Image payloads are replaced because readers that re-encode images (PdfPig
        // re-deflates PNG data) produce different bytes on .NET 8 and .NET 10; the matrix suite checks image presence.
        private static string NormalizeImages(string text)
        {
            text = Regex.Replace(text, "base64,[A-Za-z0-9+/=]+", "base64,<image>");
            text = Regex.Replace(text, "\"data\": \"[A-Za-z0-9+/=]+\"", "\"data\": \"<image>\"");
            text = Regex.Replace(text, "\"size\": [0-9]+", "\"size\": <n>");
            text = Regex.Replace(text, "size=\"[0-9]+\">[A-Za-z0-9+/=]+</resource>", "size=\"<n>\"><image></resource>");
            return text;
        }

        private static string FileName(SourceSpec source, DocumentFormatEnum target)
        {
            return source.Id + "-to-" + target + "." + DocumentFormatParser.GetDefaultExtension(target);
        }

        private static string Excerpt(string text, int index)
        {
            int start = System.Math.Max(0, index - 20);
            int length = System.Math.Min(60, text.Length - start);
            return length <= 0 ? "<end>" : text.Substring(start, length).Replace("\n", "\\n");
        }
    }
}
