namespace Test.Shared.Matrix
{
    using System.Collections.Generic;
    using System.Linq;
    using DocConverter.Enums;
    using DocConverter.Results;
    using Test.Shared.Inspection;

    /// <summary>
    /// What must survive a conversion: the intersection of what the source carries and what the target can express
    /// (plan Section 10.3). Projection cells keep their documented subset.
    /// </summary>
    public static class MatrixExpectations
    {
        /// <summary>
        /// Assert the snapshot of a converted output meets the expectation for the pair.
        /// </summary>
        /// <param name="source">Source spec.</param>
        /// <param name="target">Target format.</param>
        /// <param name="snap">Snapshot of the output.</param>
        /// <param name="result">Conversion result.</param>
        public static void Check(SourceSpec source, DocumentFormatEnum target, ContentSnapshot snap, ConversionResult result)
        {
            string pair = source.Id + " -> " + target;
            bool tablesOnly = target == DocumentFormatEnum.Csv || target == DocumentFormatEnum.Tsv;

            if (source.ImageOnly)
            {
                CheckImageOnly(source, target, snap, result, pair);
                return;
            }

            // Text: every snippet in every target that keeps text; CSV and TSV keep text only when there are no tables.
            if (!tablesOnly || source.TableRows.Count == 0)
            {
                foreach (string snippet in source.Snippets)
                    TestSupport.Assert(snap.ContainsText(snippet), pair + ": missing text '" + snippet + "' in: " + TestSupport.Truncate(snap.AllText, 300));
            }

            // Tables. CSV and TSV keep only the first table by default.
            if (source.TableRows.Count > 0)
            {
                bool structuredRows = target != DocumentFormatEnum.Text && target != DocumentFormatEnum.Pdf;
                System.Collections.Generic.List<string[]> expectedRows = tablesOnly && source.FirstTableRows.Count > 0 ? source.FirstTableRows : source.TableRows;
                foreach (string[] row in expectedRows)
                {
                    if (structuredRows)
                        TestSupport.Assert(snap.HasTableRow(row), pair + ": missing table row [" + string.Join(" | ", row) + "]; rows seen: " + Rows(snap));
                    else
                        foreach (string cell in row) TestSupport.Assert(snap.ContainsText(cell), pair + ": missing table cell '" + cell + "'");
                }
            }

            // Headings, where the target has headings the inspector can see.
            bool headingTarget = target == DocumentFormatEnum.Markdown || target == DocumentFormatEnum.Html || target == DocumentFormatEnum.Docx
                || target == DocumentFormatEnum.Json || target == DocumentFormatEnum.Xml;
            if (headingTarget)
            {
                foreach (string heading in source.Headings)
                    TestSupport.Assert(snap.Headings.Any(h => h == heading), pair + ": missing heading '" + heading + "' in [" + string.Join(", ", snap.Headings) + "]");
            }

            // Images.
            if (source.HasImage)
            {
                if (target == DocumentFormatEnum.Markdown || target == DocumentFormatEnum.Html || target == DocumentFormatEnum.Json || target == DocumentFormatEnum.Xml
                    || target == DocumentFormatEnum.Docx || target == DocumentFormatEnum.Pptx)
                {
                    TestSupport.Assert(snap.ImageCount >= 1, pair + ": image missing");
                }
                else if (target == DocumentFormatEnum.Pdf)
                {
                    TestSupport.Assert(snap.ImageCount >= 1 || snap.ContainsText("[Image:"), pair + ": image or placeholder missing");
                }
                else if (target == DocumentFormatEnum.Text)
                {
                    TestSupport.Assert(snap.ContainsText("[Image:"), pair + ": image placeholder missing");
                }
            }
        }

        private static void CheckImageOnly(SourceSpec source, DocumentFormatEnum target, ContentSnapshot snap, ConversionResult result, string pair)
        {
            switch (target)
            {
                case DocumentFormatEnum.Markdown:
                case DocumentFormatEnum.Html:
                case DocumentFormatEnum.Json:
                case DocumentFormatEnum.Xml:
                case DocumentFormatEnum.Docx:
                case DocumentFormatEnum.Pptx:
                    TestSupport.AssertEqual(1, snap.ImageCount, pair + ": the image is embedded");
                    break;
                case DocumentFormatEnum.Pdf:
                    if (source.ImageEmbeddableInPdf)
                    {
                        TestSupport.AssertEqual(1, snap.ImageCount, pair + ": the image is embedded in the PDF");
                    }
                    else
                    {
                        TestSupport.Assert(snap.ContainsText("[Image:"), pair + ": placeholder text in the PDF");
                        TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImageFormatUnsupported), pair + ": ImageFormatUnsupported");
                    }

                    break;
                default:
                    TestSupport.Assert(snap.ContainsText("[Image:"), pair + ": placeholder text; got: " + TestSupport.Truncate(snap.AllText, 200));
                    TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), pair + ": ImagePlaceholderEmitted");
                    break;
            }
        }

        private static string Rows(ContentSnapshot snap)
        {
            List<string> rows = new List<string>();
            foreach (List<string> row in snap.TableRows.Take(12)) rows.Add("[" + string.Join(" | ", row) + "]");
            return string.Join(" ", rows);
        }
    }
}
