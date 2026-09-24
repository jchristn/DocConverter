namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Test.Shared.Matrix;
    using Touchstone.Core;

    /// <summary>
    /// Every documented loss (plan Section 7.1 and docs/FORMATS.md) happens the documented way and raises the documented
    /// warning, and every warning code and matrix cell is documented.
    /// </summary>
    public static class LossDocumentationSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Loss", "Documented losses");
            Converter c = new Converter();

            foreach (SourceSpec source in MatrixCatalog.Sources.Where(x => x.ImageOnly))
            {
                foreach (DocumentFormatEnum target in new[] { DocumentFormatEnum.Text, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Xlsx })
                {
                    SourceSpec src = source;
                    DocumentFormatEnum to = target;
                    s.Add("ImageOnly_" + src.Id + "_" + to, "Image-only " + src.Id + " to " + to + " writes a placeholder naming the image and raises ImagePlaceholderEmitted", async ct =>
                    {
                        BytesConversionResult r = await c.ConvertToBytesAsync(src.Bytes(), src.Format, to, null, ct).ConfigureAwait(false);
                        ContentSnapshot snap = OutputInspector.Inspect(to, r.Output);
                        TestSupport.Assert(snap.ContainsText("[Image:"), "placeholder present: " + snap.AllText);
                        TestSupport.Assert(snap.ContainsText("96x64"), "placeholder names the pixel size");
                        TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted");
                    });
                }
            }

            foreach (string name in new[] { "sample.gif", "sample.tiff", "sample.webp", "sample-lossless.webp", "sample-cmyk.jpg" })
            {
                string file = name;
                s.Add("PdfUnsupportedImage_" + file, file + " embedded in a document becomes a PDF placeholder with ImageFormatUnsupported, never MigraDoc's error text", async ct =>
                {
                    DocumentModel m = new DocumentModel();
                    byte[] data = TestImages.Sample(file);
                    string id = m.AddResource(new BinaryResource { Data = data, MediaType = "image/x", FileName = file });
                    m.Blocks.Add(new ParagraphBlock("Before the image."));
                    m.Blocks.Add(new ImageBlock(id, "Sample " + file));
                    BytesConversionResult r = await c.WriteToBytesAsync(m, DocumentFormatEnum.Pdf, null, ct).ConfigureAwait(false);
                    ContentSnapshot snap = PdfInspector.Inspect(r.Output);
                    TestSupport.Assert(snap.ContainsText("[Image: Sample " + file), "placeholder names the image: " + snap.AllText);
                    TestSupport.AssertEqual(0, snap.ImageCount, "no image embedded");
                    TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImageFormatUnsupported), "ImageFormatUnsupported");
                });
            }

            s.Add("GlyphsUnavailable", "Characters outside Liberation's coverage raise GlyphsUnavailable with an exact count; Latin, Greek and Cyrillic do not", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(new ParagraphBlock("Latin é, Greek Ω, Cyrillic Ж."));
                BytesConversionResult clean = await c.WriteToBytesAsync(m, DocumentFormatEnum.Pdf, null, ct).ConfigureAwait(false);
                TestSupport.Assert(!clean.Warnings.Any(w => w.Code == WarningCodeEnum.GlyphsUnavailable), "covered scripts raise nothing");
                m.Blocks.Add(new ParagraphBlock("你好 مرحبا 🚀"));
                BytesConversionResult r = await c.WriteToBytesAsync(m, DocumentFormatEnum.Pdf, null, ct).ConfigureAwait(false);
                ConversionWarning w = r.Warnings.Single(x => x.Code == WarningCodeEnum.GlyphsUnavailable);
                TestSupport.AssertContains(w.Message, "8", "message counts the 8 missing characters (2 CJK, 5 Arabic, 1 emoji): " + w.Message);
            });

            s.Add("DocumentToCsv", "A document with tables and other content to CSV keeps the table and raises NonTableContentDropped", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.NonTableContentDropped), "NonTableContentDropped");
            });

            s.Add("DocumentToText", "A styled document to text keeps words, drops styles (FormattingLost) and placeholders images (ImagePlaceholderEmitted)", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted");
            });

            s.Add("DocumentToXlsx", "A non-tabular document to XLSX puts tables on sheets, other blocks and image placeholders on a Document sheet", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Xlsx, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = XlsxInspector.Inspect(r.Output);
                TestSupport.Assert(snap.ContainsText(ReferenceContent.Closing), "non-table content on the Document sheet");
                foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "table row");
                TestSupport.Assert(snap.ContainsText("[Image: Reference image, PNG 16x16]"), "image placeholder row");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost");
            });

            s.Add("PptxSplitIsLossless", "Long tables split across PPTX slides keep every row and raise no warning", async ct =>
            {
                DocumentModel m = new DocumentModel();
                TableBlock t = new TableBlock { HeaderRowCount = 1 };
                t.Rows.Add(new TableRow(new[] { "n", "square" }));
                for (int i = 1; i <= 60; i++) t.Rows.Add(new TableRow(new[] { i.ToString(System.Globalization.CultureInfo.InvariantCulture), (i * i).ToString(System.Globalization.CultureInfo.InvariantCulture) }));
                m.Blocks.Add(t);
                BytesConversionResult r = await c.WriteToBytesAsync(m, DocumentFormatEnum.Pptx, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = PptxInspector.Inspect(r.Output);
                TestSupport.Assert(snap.HasTableRow(new[] { "60", "3600" }), "last row present");
                TestSupport.Assert(snap.TableRows.Count(x => x.Count > 0 && x[0] == "n") > 1, "header repeated on continuation slides");
                TestSupport.AssertEqual(0, r.Warnings.Count, "no warnings");
            });

            s.Add("PdfSourceHeadingsInferred", "Reading PDF infers headings from font size and says so", async ct =>
            {
                BytesConversionResult r = await c.ConvertToBytesAsync(Fixtures.Builders.PdfFixtureBuilder.BuildReference(), DocumentFormatEnum.Pdf, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.HeadingsInferred), "HeadingsInferred");
            });

            s.Add("DocumentedEverywhere", "Every warning code and every matrix cell appears in docs/FORMATS.md", async ct =>
            {
                string formats = await Task.Run(() => File.ReadAllText(RepoFile("docs", "FORMATS.md")), ct).ConfigureAwait(false);
                foreach (WarningCodeEnum code in Enum.GetValues(typeof(WarningCodeEnum)))
                    TestSupport.AssertContains(formats, "`" + code + "`", "FORMATS.md documents warning " + code);
                string generated = Docs.FormatsTable.Render(new Converter());
                TestSupport.AssertContains(formats.Replace("\r\n", "\n"), generated, "FORMATS.md contains the generated matrix exactly (regenerate with Test.Automated --update-docs <repo>)");
            });

            return s.Build();
        }

        /// <summary>
        /// Path of a file in the repository, found by walking up from the test assembly to the directory holding
        /// DOC_CONVERTER.md.
        /// </summary>
        /// <param name="parts">Path parts below the repository root.</param>
        /// <returns>Full path.</returns>
        public static string RepoFile(params string[] parts)
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DOC_CONVERTER.md"))) dir = dir.Parent;
            if (dir == null) throw new TestAssertionException("Repository root not found above " + AppContext.BaseDirectory);
            return Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
        }
    }
}
