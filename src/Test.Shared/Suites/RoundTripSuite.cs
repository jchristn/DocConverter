namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Test.Shared.Matrix;
    using Touchstone.Core;

    /// <summary>
    /// Round trips: through the canonical forms (lossless by contract), through rich formats (structure preserved),
    /// every input shape producing identical output, and deterministic output for every writer.
    /// </summary>
    public static class RoundTripSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("RoundTrip", "Round trips, input shapes and determinism");
            Converter c = new Converter();

            foreach (SourceSpec source in MatrixCatalog.Sources)
            {
                SourceSpec src = source;
                s.Add("ViaJson_" + src.Id, src.Id + ": the canonical mapping is lossless and JSON and XML round trips are idempotent", async ct =>
                {
                    DocumentModel direct = await c.ReadAsync(src.Bytes(), src.Format, null, ct).ConfigureAwait(false);
                    ModelComparer.AssertEqual(direct, DocConverter.Model.Serialization.CanonicalMapper.FromDto(DocConverter.Model.Serialization.CanonicalMapper.ToDto(direct)), "canonical mapping of " + src.Id);

                    // Writing applies the documented model normalization once; after that, round trips change nothing.
                    foreach (DocumentFormatEnum canonical in new[] { DocumentFormatEnum.Json, DocumentFormatEnum.Xml })
                    {
                        byte[] first = (await c.WriteToBytesAsync(direct, canonical, null, ct).ConfigureAwait(false)).Output;
                        DocumentModel once = await c.ReadAsync(first, canonical, null, ct).ConfigureAwait(false);
                        byte[] second = (await c.WriteToBytesAsync(once, canonical, null, ct).ConfigureAwait(false)).Output;
                        DocumentModel twice = await c.ReadAsync(second, canonical, null, ct).ConfigureAwait(false);
                        ModelComparer.AssertEqual(once, twice, canonical + " round trip of " + src.Id + " is idempotent");
                        TestSupport.Assert(first.SequenceEqual(second), canonical + " output of " + src.Id + " is stable across round trips");
                    }
                });

                s.Add("InputShapes_" + src.Id, src.Id + ": byte array, string, seekable stream at an offset, non-seekable and 1-byte-per-read streams give identical output", async ct =>
                {
                    ConversionOptions o = new ConversionOptions { Deterministic = true };
                    byte[] input = src.Bytes();
                    string baseline = (await c.ConvertToStringAsync(input, src.Format, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output;

                    string asString = DocConverter.Detection.DocumentFormatParser.IsTextBased(src.Format)
                        ? TextFixturesDecode(input)
                        : Convert.ToBase64String(input);
                    string viaString = (await c.ConvertToStringAsync(asString, src.Format, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output;
                    TestSupport.AssertEqual(baseline, viaString, "string input");

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(new byte[] { 9, 9, 9, 9 }, 0, 4);
                        ms.Write(input, 0, input.Length);
                        ms.Position = 4;
                        string viaSeek = (await c.ConvertToStringAsync(ms, src.Format, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output;
                        TestSupport.AssertEqual(baseline, viaSeek, "seekable stream at offset");
                    }

                    using (NonSeekableStream ns = new NonSeekableStream(input))
                        TestSupport.AssertEqual(baseline, (await c.ConvertToStringAsync(ns, src.Format, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output, "non-seekable stream");

                    using (NonSeekableStream slow = new NonSeekableStream(input, 1))
                        TestSupport.AssertEqual(baseline, (await c.ConvertToStringAsync(slow, src.Format, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output, "1 byte per read");

                    string viaAuto = (await c.ConvertToStringAsync(input, DocumentFormatEnum.Auto, DocumentFormatEnum.Json, o, ct).ConfigureAwait(false)).Output;
                    TestSupport.AssertEqual(baseline, viaAuto, "Auto detection reads the same document");
                });
            }

            foreach (DocumentFormatEnum target in MatrixCatalog.Targets)
            {
                DocumentFormatEnum to = target;
                s.Add("Deterministic_" + to, to + " output is byte-identical across runs with Deterministic", async ct =>
                {
                    ConversionOptions o = new ConversionOptions { Deterministic = true };
                    byte[] a = (await c.WriteToBytesAsync(ReferenceContent.ToModel(), to, o, ct).ConfigureAwait(false)).Output;
                    await Task.Delay(1100, ct).ConfigureAwait(false);
                    byte[] b = (await new Converter().WriteToBytesAsync(ReferenceContent.ToModel(), to, o, ct).ConfigureAwait(false)).Output;
                    TestSupport.Assert(a.SequenceEqual(b), to + " differs between runs (" + a.Length + " vs " + b.Length + " bytes)");
                });
            }

            DocumentFormatEnum[] rich = { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Docx };
            foreach (DocumentFormatEnum via in rich)
            {
                DocumentFormatEnum format = via;
                s.Add("Structure_" + format, "Reference model through " + format + " and back keeps headings, lists, table, code, quote, image and styles", async ct =>
                {
                    byte[] bytes = (await c.WriteToBytesAsync(ReferenceContent.ToModel(), format, null, ct).ConfigureAwait(false)).Output;
                    DocumentModel back = await c.ReadAsync(bytes, format, null, ct).ConfigureAwait(false);
                    TextReadersSuite.AssertRichReference(back, format != DocumentFormatEnum.Markdown, format != DocumentFormatEnum.Docx);
                });
            }

            s.Add("MarkdownDocxMarkdown", "Markdown to DOCX to Markdown keeps the reference structure", async ct =>
            {
                byte[] docx = (await c.ConvertToBytesAsync(TextFixtures.Reference(DocumentFormatEnum.Markdown), DocumentFormatEnum.Markdown, DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false)).Output;
                byte[] md = (await c.ConvertToBytesAsync(docx, DocumentFormatEnum.Docx, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false)).Output;
                DocumentModel back = await c.ReadAsync(md, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TextReadersSuite.AssertRichReference(back, false, false);
            });

            s.Add("HtmlDocxHtml", "HTML to DOCX to HTML keeps the reference structure", async ct =>
            {
                byte[] docx = (await c.ConvertToBytesAsync(TextFixtures.Reference(DocumentFormatEnum.Html), DocumentFormatEnum.Html, DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false)).Output;
                byte[] html = (await c.ConvertToBytesAsync(docx, DocumentFormatEnum.Docx, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false)).Output;
                DocumentModel back = await c.ReadAsync(html, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TextReadersSuite.AssertRichReference(back, true, false);
            });

            s.Add("CsvXlsxCsv", "CSV to XLSX to CSV is exact", async ct =>
            {
                byte[] csv = TextFixtures.Reference(DocumentFormatEnum.Csv);
                byte[] xlsx = (await c.ConvertToBytesAsync(csv, DocumentFormatEnum.Csv, DocumentFormatEnum.Xlsx, null, ct).ConfigureAwait(false)).Output;
                StringConversionResult back = await c.ConvertToStringAsync(xlsx, DocumentFormatEnum.Xlsx, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(TextFixturesDecode(csv), back.Output, "csv identical");
            });

            s.Add("MarkdownPdfMarkdown", "Markdown to PDF to Markdown keeps headings text and table cells", async ct =>
            {
                byte[] pdf = (await c.ConvertToBytesAsync(TextFixtures.Reference(DocumentFormatEnum.Markdown), DocumentFormatEnum.Markdown, DocumentFormatEnum.Pdf, null, ct).ConfigureAwait(false)).Output;
                DocumentModel back = await c.ReadAsync(pdf, DocumentFormatEnum.Pdf, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(back);
                foreach (string snippet in ReferenceContent.CoreTextSnippets) TestSupport.Assert(snap.ContainsText(snippet), "pdf keeps '" + snippet + "'");
                foreach (string[] row in ReferenceContent.TableRows) foreach (string cell in row) TestSupport.Assert(snap.ContainsText(cell), "pdf keeps cell '" + cell + "'");
            });

            s.Add("PptxRoundTrip", "Model to PPTX to model keeps slide titles, bullets and table cells", async ct =>
            {
                byte[] pptx = (await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Pptx, null, ct).ConfigureAwait(false)).Output;
                DocumentModel back = await c.ReadAsync(pptx, DocumentFormatEnum.Pptx, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(back);
                TestSupport.Assert(snap.Headings.Contains(ReferenceContent.Heading1), "title");
                foreach (string b in ReferenceContent.Bullets) TestSupport.Assert(snap.ListItems.Contains(b), "bullet " + b);
                foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "row " + string.Join("|", row));
            });

            return s.Build();
        }

        private static string TextFixturesDecode(byte[] bytes)
        {
            return Inspection.TextInspector.DecodeStrict(bytes);
        }
    }
}
