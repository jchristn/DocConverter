namespace Test.Shared.Suites.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;
    using DocConverter.Writers.Pdf;
    using PdfSharp.Fonts;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Touchstone.Core;

    /// <summary>
    /// PDF writer cases.
    /// </summary>
    public static class PdfWriterCases
    {
        private static readonly string[] _EmbeddableImages = new string[] { "sample.png", "sample-alpha.png", "sample-palette.png", "sample.jpg", "sample-progressive.jpg", "sample.bmp" };
        private static readonly string[] _PlaceholderImages = new string[] { "sample.gif", "sample.tiff", "sample.webp", "sample-lossless.webp", "sample-cmyk.jpg" };

        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("WriteReference", "The reference model renders with every Latin snippet, one image, the title and the link", async ct =>
                {
                    BytesConversionResult result = await Write(ReferenceContent.ToModel(), null).ConfigureAwait(false);
                    ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                    foreach (string snippet in ReferenceContent.CoreTextSnippets) TestSupport.Assert(snapshot.ContainsText(snippet), "PDF text contains '" + snippet + "': " + TestSupport.Truncate(snapshot.AllText, 300));
                    TestSupport.Assert(snapshot.ContainsText(ReferenceContent.Special), "special characters paragraph");
                    TestSupport.Assert(snapshot.ContainsText("int total = 40 + 2;"), "code text");
                    TestSupport.AssertEqual(1, snapshot.ImageCount, "image count");
                    TestSupport.AssertEqual(ReferenceContent.Title, snapshot.Title, "title");
                    TestSupport.Assert(snapshot.LinkUrls.Contains(ReferenceContent.LinkUrl), "link annotation to " + ReferenceContent.LinkUrl);
                    TestSupport.AssertEqual(DocumentFormatEnum.Pdf, result.TargetFormat, "target format");
                    TestSupport.AssertEqual((long)result.Output.Length, result.BytesWritten, "bytes written");
                }),
                Case("WriteReferenceWarnings", "The reference model raises GlyphsUnavailable (CJK, Arabic, emoji) and FormattingLost (strikethrough)", async ct =>
                {
                    BytesConversionResult result = await Write(ReferenceContent.ToModel(), null).ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.GlyphsUnavailable), "GlyphsUnavailable");
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.FormattingLost), "FormattingLost for strikethrough");
                    TestSupport.Assert(!ModelQuery.HasWarning(result, WarningCodeEnum.ImageFormatUnsupported), "PNG embeds without a warning");
                }),
                Case("WriteRoundTripStructure", "Reading the written reference back recovers headings, the table and the lists", async ct =>
                {
                    BytesConversionResult result = await Write(ReferenceContent.ToModel(), null).ConfigureAwait(false);
                    using (Converter converter = new Converter())
                    {
                        DocumentModel back = await converter.ReadAsync(result.Output, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                        List<HeadingBlock> headings = ModelQuery.Of<HeadingBlock>(back);
                        TestSupport.Assert(headings.Any(h => ModelQuery.Text(h.Inlines) == ReferenceContent.Heading1 && h.Level == 1), "H1 recovered");
                        TestSupport.Assert(headings.Any(h => ModelQuery.Text(h.Inlines) == ReferenceContent.HeadingTable && h.Level == 2), "H2 recovered");
                        PdfReaderCases.AssertTable(ModelQuery.Of<TableBlock>(back).Single());
                        TestSupport.Assert(ModelQuery.Of<ListBlock>(back).Count >= 2, "lists recovered");
                        TestSupport.AssertEqual(1, ModelQuery.Of<ImageBlock>(back).Count, "image recovered");
                    }
                }),
                Case("GlyphCountsCjk", "Four CJK characters count exactly four missing glyphs", async ct =>
                {
                    await AssertGlyphs("\u4F60\u597D\u4E16\u754C", 4).ConfigureAwait(false);
                }),
                Case("GlyphCountsArabic", "Five Arabic letters count exactly five missing glyphs", async ct =>
                {
                    await AssertGlyphs("\u0645\u0631\u062D\u0628\u0627", 5).ConfigureAwait(false);
                }),
                Case("GlyphCountsEmoji", "Two emoji (one a surrogate pair) count exactly two missing glyphs", async ct =>
                {
                    await AssertGlyphs("Launch \uD83D\uDE80 done \u2705", 2).ConfigureAwait(false);
                }),
                Case("GlyphsCoveredNoWarning", "Latin, Greek and Cyrillic text raises no GlyphsUnavailable", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    model.Blocks.Add(new ParagraphBlock("Gr\u00FC\u00DFe aus Z\u00FCrich. \u0395\u03BB\u03BB\u03B7\u03BD\u03B9\u03BA\u03AC. \u041A\u0438\u0440\u0438\u043B\u043B\u0438\u0446\u0430."));
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    TestSupport.Assert(!ModelQuery.HasWarning(result, WarningCodeEnum.GlyphsUnavailable), "no GlyphsUnavailable");
                    TestSupport.Assert(PdfInspector.Inspect(result.Output).ContainsText("\u041A\u0438\u0440\u0438\u043B\u043B\u0438\u0446\u0430"), "Cyrillic text present");
                }),
                Case("PageSizeLetter", "Letter pages are 612 x 792 points", async ct => await AssertPageSize(PdfPageSizeEnum.Letter, 612, 792).ConfigureAwait(false)),
                Case("PageSizeA4", "A4 pages are 595 x 842 points", async ct => await AssertPageSize(PdfPageSizeEnum.A4, 595.28, 841.89).ConfigureAwait(false)),
                Case("PageSizeLegal", "Legal pages are 612 x 1008 points", async ct => await AssertPageSize(PdfPageSizeEnum.Legal, 612, 1008).ConfigureAwait(false)),
                Case("MarginOption", "MarginPoints moves the left text edge", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    model.Blocks.Add(new ParagraphBlock("Margin probe"));
                    foreach (double margin in new double[] { 36, 72, 144 })
                    {
                        ConversionOptions options = new ConversionOptions();
                        options.Pdf.MarginPoints = margin;
                        BytesConversionResult result = await Write(model, options).ConfigureAwait(false);
                        double left = PdfInspector.FirstPageTextLeft(result.Output);
                        TestSupport.Assert(Math.Abs(left - margin) < 3, "text left edge " + left + " for margin " + margin);
                    }
                }),
                Case("BaseFontSizeOption", "BaseFontSize sets the body text size", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    model.Blocks.Add(new ParagraphBlock("Body text sized by the BaseFontSize option for this probe."));
                    ConversionOptions options = new ConversionOptions();
                    options.Pdf.BaseFontSize = 14;
                    BytesConversionResult result = await Write(model, options).ConfigureAwait(false);
                    TestSupport.AssertEqual(14.0, PdfInspector.DominantFontSize(result.Output), "dominant font size");
                }),
                Case("Deterministic", "Deterministic output is byte-identical across runs", async ct =>
                {
                    ConversionOptions options = new ConversionOptions();
                    options.Deterministic = true;
                    BytesConversionResult a = await Write(ReferenceContent.ToModel(), options).ConfigureAwait(false);
                    BytesConversionResult b = await Write(ReferenceContent.ToModel(), options).ConfigureAwait(false);
                    TestSupport.AssertEqual(a.Output.Length, b.Output.Length, "deterministic length");
                    TestSupport.Assert(a.Output.SequenceEqual(b.Output), "deterministic bytes identical");
                }),
                Case("ConcurrentWrites", "Eight concurrent deterministic PDF writes all succeed with identical bytes", async ct =>
                {
                    ConversionOptions options = new ConversionOptions();
                    options.Deterministic = true;
                    List<Task<BytesConversionResult>> tasks = new List<Task<BytesConversionResult>>();
                    using (Converter converter = new Converter())
                    {
                        for (int i = 0; i < 8; i++)
                            tasks.Add(Task.Run(() => converter.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Pdf, options)));
                        BytesConversionResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                        foreach (BytesConversionResult r in results)
                        {
                            TestSupport.Assert(r.Output.SequenceEqual(results[0].Output), "concurrent outputs identical");
                            TestSupport.Assert(PdfInspector.Inspect(r.Output).ContainsText(ReferenceContent.Closing), "concurrent output content");
                        }
                    }
                }),
                Case("WritePreCancelled", "A cancelled token throws OperationCanceledException", async ct =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    using (Converter converter = new Converter())
                    {
                        cts.Cancel();
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Pdf, null, cts.Token), "pre-cancelled write").ConfigureAwait(false);
                    }
                }),
                Case("FontResolverInstalled", "The embedded resolver is installed once and stays installed", async ct =>
                {
                    await Write(ReferenceContent.ToModel(), null).ConfigureAwait(false);
                    IFontResolver? first = GlobalFontSettings.FontResolver;
                    PdfFontInstaller.EnsureInstalled();
                    TestSupport.Assert(ReferenceEquals(first, GlobalFontSettings.FontResolver), "resolver instance unchanged");
                    TestSupport.Assert(PdfFontInstaller.EmbeddedResolverActive, "embedded resolver active");
                    DocConverterFontResolver resolver = new DocConverterFontResolver();
                    TestSupport.AssertEqual("LiberationMono-Regular", resolver.ResolveTypeface("Courier New", false, false)!.FaceName, "Courier New maps to mono");
                    TestSupport.AssertEqual("LiberationMono-BoldItalic", resolver.ResolveTypeface("Consolas", true, true)!.FaceName, "Consolas bold italic");
                    TestSupport.AssertEqual("LiberationSans-Bold", resolver.ResolveTypeface("Arial", true, false)!.FaceName, "Arial bold maps to sans");
                    TestSupport.AssertEqual("LiberationSans-Regular", resolver.ResolveTypeface("Some Unknown Font", false, false)!.FaceName, "unknown family maps to sans");
                    foreach (string face in new string[] { "LiberationSans-Regular", "LiberationSans-Bold", "LiberationSans-Italic", "LiberationSans-BoldItalic", "LiberationMono-Regular", "LiberationMono-Bold", "LiberationMono-Italic", "LiberationMono-BoldItalic" })
                        TestSupport.Assert((resolver.GetFont(face) ?? new byte[0]).Length > 10000, "embedded face " + face);
                }),
                Case("UnsafeLinkRemoved", "A javascript: link is written as plain text with LinkRemovedUnsafe", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    ParagraphBlock p = new ParagraphBlock("Click ");
                    p.Inlines.Add(new LinkInline("javascript:alert(1)", "here"));
                    model.Blocks.Add(p);
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.LinkRemovedUnsafe), "LinkRemovedUnsafe");
                    TestSupport.AssertEqual(0, snapshot.LinkUrls.Count, "no link annotations");
                    TestSupport.Assert(snapshot.ContainsText("Click here"), "link text kept");
                }),
                Case("EmptyDocument", "An empty model renders a valid one page PDF", async ct =>
                {
                    BytesConversionResult result = await Write(new DocumentModel(), null).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, PdfInspector.PageSizes(result.Output).Count, "one page");
                }),
                Case("TableSpans", "Merged cells render and keep their text", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    TableBlock table = new TableBlock();
                    table.HeaderRowCount = 1;
                    TableRow header = new TableRow();
                    TableCell wide = new TableCell("Spans two columns");
                    wide.ColumnSpan = 2;
                    header.Cells.Add(wide);
                    header.Cells.Add(new TableCell("C"));
                    table.Rows.Add(header);
                    TableRow row = new TableRow();
                    TableCell tall = new TableCell("Spans two rows");
                    tall.RowSpan = 2;
                    row.Cells.Add(tall);
                    row.Cells.Add(new TableCell("b1"));
                    row.Cells.Add(new TableCell("c1"));
                    table.Rows.Add(row);
                    table.Rows.Add(new TableRow(new string[] { "b2", "c2" }));
                    model.Blocks.Add(table);
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                    foreach (string t in new string[] { "Spans two columns", "Spans two rows", "b1", "c2" })
                        TestSupport.Assert(snapshot.ContainsText(t), "span table text " + t);
                    TestSupport.Assert(!ModelQuery.HasWarning(result, WarningCodeEnum.TableSpansFlattened), "spans fit the grid");
                }),
                Case("NestedTableFlattened", "A table inside a table cell is written as text rows with TablesFlattened", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    TableBlock outer = new TableBlock();
                    TableRow row = new TableRow();
                    TableCell cell = new TableCell();
                    TableBlock inner = new TableBlock();
                    inner.Rows.Add(new TableRow(new string[] { "inner a", "inner b" }));
                    cell.Blocks.Add(inner);
                    row.Cells.Add(cell);
                    row.Cells.Add(new TableCell("outer"));
                    outer.Rows.Add(row);
                    model.Blocks.Add(outer);
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.TablesFlattened), "TablesFlattened");
                    TestSupport.Assert(PdfInspector.Inspect(result.Output).ContainsText("inner a | inner b"), "flattened row text");
                }),
                Case("SectionsAndBreaks", "Page and slide sections start new pages; page breaks break", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    SectionBlock s1 = new SectionBlock(SectionKindEnum.Slide, "Slide one");
                    s1.Blocks.Add(new ParagraphBlock("first"));
                    SectionBlock s2 = new SectionBlock(SectionKindEnum.Slide, "Slide two");
                    s2.Blocks.Add(new ParagraphBlock("second"));
                    model.Blocks.Add(s1);
                    model.Blocks.Add(s2);
                    model.Blocks.Add(new PageBreakBlock());
                    model.Blocks.Add(new ParagraphBlock("third"));
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    TestSupport.AssertEqual(6, PdfInspector.PageSizes(result.Output).Count, "three pages");
                    TestSupport.Assert(PdfInspector.Inspect(result.Output).ContainsText("Slide two"), "section title");
                }),
                Case("MetadataKeywords", "Title, author, subject and keywords go into the PDF information", async ct =>
                {
                    DocumentModel model = ReferenceContent.ToModel();
                    model.Metadata.Keywords = "alpha, beta";
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    using (UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(result.Output))
                    {
                        TestSupport.AssertEqual(ReferenceContent.Author, pdf.Information.Author, "author");
                        TestSupport.AssertEqual(ReferenceContent.Subject, pdf.Information.Subject, "subject");
                        TestSupport.AssertEqual("alpha, beta", pdf.Information.Keywords, "keywords");
                    }
                }),
                Case("MissingResourcePlaceholder", "An image referencing a missing resource becomes a placeholder with UnknownElementSkipped", async ct =>
                {
                    DocumentModel model = new DocumentModel();
                    model.Blocks.Add(new ImageBlock("nope", "Ghost"));
                    BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.UnknownElementSkipped), "UnknownElementSkipped");
                    TestSupport.Assert(PdfInspector.Inspect(result.Output).ContainsText("[Image: Ghost"), "placeholder text");
                })
            };

            foreach (string name in _EmbeddableImages)
            {
                string file = name;
                cases.Add(Case("EmbedImage_" + Id(file), "Embeds " + file + " natively", async ct =>
                {
                    BytesConversionResult result = await Write(ImageModel(file), null).ConfigureAwait(false);
                    ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                    TestSupport.AssertEqual(1, snapshot.ImageCount, file + " embedded");
                    TestSupport.Assert(!ModelQuery.HasWarning(result, WarningCodeEnum.ImageFormatUnsupported), file + " no warning");
                }));
            }

            foreach (string name in _PlaceholderImages)
            {
                string file = name;
                cases.Add(Case("PlaceholderImage_" + Id(file), "Writes a placeholder for " + file, async ct =>
                {
                    BytesConversionResult result = await Write(ImageModel(file), null).ConfigureAwait(false);
                    ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                    TestSupport.AssertEqual(0, snapshot.ImageCount, file + " not embedded");
                    TestSupport.Assert(snapshot.ContainsText("[Image: " + file), file + " placeholder names the image: " + snapshot.AllText);
                    TestSupport.Assert(snapshot.ContainsText("96x64, not embeddable in PDF]"), file + " placeholder size");
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.ImageFormatUnsupported), file + " ImageFormatUnsupported");
                }));
            }

            string[][] conversions = new string[][]
            {
                new string[] { "sample.png", "Png", "1" }, new string[] { "sample.jpg", "Jpeg", "1" }, new string[] { "sample.bmp", "Bmp", "1" },
                new string[] { "sample.gif", "Gif", "0" }, new string[] { "sample.tiff", "Tiff", "0" }, new string[] { "sample.webp", "WebP", "0" },
                new string[] { "sample-cmyk.jpg", "Jpeg", "0" }
            };
            foreach (string[] c in conversions)
            {
                string file = c[0];
                DocumentFormatEnum format = (DocumentFormatEnum)Enum.Parse(typeof(DocumentFormatEnum), c[1]);
                int images = int.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture);
                cases.Add(Case("Convert_" + Id(file) + "_ToPdf", "Converts " + file + " (" + format + ") to PDF", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        BytesConversionResult result = await converter.ConvertToBytesAsync(FormatFixtures.Load("Images/" + file), format, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                        ContentSnapshot snapshot = PdfInspector.Inspect(result.Output);
                        TestSupport.AssertEqual(images, snapshot.ImageCount, file + " image count");
                        TestSupport.AssertEqual(images == 0, ModelQuery.HasWarning(result, WarningCodeEnum.ImageFormatUnsupported), file + " warning presence");
                    }
                }));
            }

            return cases;
        }

        private static string Id(string file)
        {
            return file.Replace('.', '_').Replace('-', '_');
        }

        private static DocumentModel ImageModel(string file)
        {
            byte[] data = FormatFixtures.Load("Images/" + file);
            DocumentModel model = new DocumentModel();
            model.AddResource(new BinaryResource { Id = "img1", Data = data, FileName = file, MediaType = "application/octet-stream" });
            model.Blocks.Add(new ImageBlock("img1", null));
            return model;
        }

        private static async Task AssertGlyphs(string text, int expected)
        {
            DocumentModel model = new DocumentModel();
            model.Blocks.Add(new ParagraphBlock(text));
            BytesConversionResult result = await Write(model, null).ConfigureAwait(false);
            ConversionWarning? warning = ModelQuery.Warning(result, WarningCodeEnum.GlyphsUnavailable);
            TestSupport.Assert(warning != null, "GlyphsUnavailable expected");
            TestSupport.Assert(warning!.Message.StartsWith(expected + " character(s)", StringComparison.Ordinal), "count in message: " + warning.Message);
        }

        private static async Task AssertPageSize(PdfPageSizeEnum size, double width, double height)
        {
            DocumentModel model = new DocumentModel();
            model.Blocks.Add(new ParagraphBlock("Page size probe"));
            ConversionOptions options = new ConversionOptions();
            options.Pdf.PageSize = size;
            BytesConversionResult result = await Write(model, options).ConfigureAwait(false);
            List<double> sizes = PdfInspector.PageSizes(result.Output);
            TestSupport.Assert(Math.Abs(sizes[0] - width) < 1 && Math.Abs(sizes[1] - height) < 1, size + " page is " + sizes[0] + " x " + sizes[1]);
        }

        private static async Task<BytesConversionResult> Write(DocumentModel model, ConversionOptions? options)
        {
            using (Converter converter = new Converter())
            {
                return await converter.WriteToBytesAsync(model, DocumentFormatEnum.Pdf, options).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Pdf", id, name, executeAsync: body);
        }
    }
}
