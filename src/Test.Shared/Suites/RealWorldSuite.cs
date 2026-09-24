namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Results;
    using Test.Shared.Inspection;
    using Test.Shared.Matrix;
    using Touchstone.Core;

    /// <summary>
    /// Files produced by other software (Fixtures/RealWorld, see PROVENANCE.md) through every target: no exception, the
    /// output validates, and each file's manifest snippet survives where the target keeps text.
    /// </summary>
    public static class RealWorldSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("RealWorld", "Real-world files");
            Converter c = new Converter();

            // File, snippet that must survive (null for image files), true when the file is tabular.
            string[][] manifest = new string[][]
            {
                new[] { "DocumentAtom/sample.txt", "DocumentAtom SDK Test Sample", "false" },
                new[] { "DocumentAtom/sample.md", "DocumentAtom SDK Test Document", "false" },
                new[] { "DocumentAtom/sample.html", "DocumentAtom", "false" },
                new[] { "DocumentAtom/sample.csv", "John Doe", "true" },
                new[] { "DocumentAtom/sample.json", "DocumentAtom Technologies", "false" },
                new[] { "DocumentAtom/sample.xml", "DocumentAtom SDK Test Document", "false" },
                new[] { "DocumentAtom/sample.rtf", "DocumentAtom SDK Test Document", "false" },
                new[] { "DocumentAtom/sample.docx", "DocumentAtom SDK Test Document", "false" },
                new[] { "DocumentAtom/sample.xlsx", "John Doe", "true" },
                new[] { "DocumentAtom/sample.pptx", "DocumentAtom SDK Test Presentation", "false" },
                new[] { "DocumentAtom/sample.pdf", "DocumentAtom", "false" },
                new[] { "DocumentAtom/sample.png", "", "false" },
                new[] { "DocumentAtom/sample.jpg", "", "false" }
            };

            foreach (string[] entry in manifest)
            {
                string path = entry[0];
                string snippet = entry[1];
                bool tabular = entry[2] == "true";
                foreach (DocumentFormatEnum target in MatrixCatalog.Targets)
                {
                    DocumentFormatEnum to = target;
                    string id = path.Replace("DocumentAtom/sample.", "DocumentAtom_") + "_" + to;
                    s.Add(id, path + " to " + to, async ct =>
                    {
                        byte[] input = TestSupport.Fixture("RealWorld/" + path);
                        DocumentFormatEnum from = DocumentFormatParser.FromExtension(path)!.Value;
                        DetectionResult detected = await c.DetectFormatAsync(input, path, ct).ConfigureAwait(false);
                        TestSupport.AssertEqual<DocumentFormatEnum?>(from, detected.Format, "detection agrees with the extension");
                        BytesConversionResult r = await c.ConvertToBytesAsync(input, from, to, null, ct).ConfigureAwait(false);
                        ContentSnapshot snap = OutputInspector.Inspect(to, r.Output);
                        bool tablesOnlyTarget = to == DocumentFormatEnum.Csv || to == DocumentFormatEnum.Tsv;
                        if (snippet.Length > 0 && (!tablesOnlyTarget || tabular))
                            TestSupport.Assert(snap.ContainsText(snippet), path + " -> " + to + " keeps '" + snippet + "': " + TestSupport.Truncate(snap.AllText, 300));
                    });
                }
            }

            return s.Build();
        }
    }
}
