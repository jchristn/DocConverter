namespace Test.Shared.Suites.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;
    using Touchstone.Core;

    /// <summary>
    /// PDF reader cases.
    /// </summary>
    public static class PdfReaderCases
    {
        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Build()
        {
            return new List<TestCaseDescriptor>
            {
                Case("ReadReferenceText", "Every reference text snippet is recovered from the builder PDF", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    string text = ModelQuery.AllText(model);
                    List<string> expected = new List<string>(ReferenceContent.CoreTextSnippets);
                    expected.AddRange(ReferenceContent.Bullets);
                    expected.AddRange(ReferenceContent.Steps);
                    expected.Add(ReferenceContent.DeepBullet);
                    expected.Add(ReferenceContent.Special);
                    expected.Add(PdfFixtureBuilder.SecondPageText);
                    foreach (string snippet in expected) TestSupport.AssertContains(text, snippet, "PDF text");
                }),
                Case("ReadHeadingLevels", "Headings are inferred from font size with levels 1, 2, 2, 3", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    List<HeadingBlock> headings = ModelQuery.Of<HeadingBlock>(model);
                    AssertHeading(headings, ReferenceContent.Heading1, 1);
                    AssertHeading(headings, ReferenceContent.HeadingLists, 2);
                    AssertHeading(headings, ReferenceContent.HeadingTable, 2);
                    AssertHeading(headings, ReferenceContent.HeadingCode, 3);
                    TestSupport.AssertEqual(4, headings.Count, "heading count");
                }),
                Case("HeadingsInferredWarning", "Reading a PDF with inferred headings raises HeadingsInferred", async ct =>
                {
                    ConversionResult result = await Convert(PdfFixtures.Reference, null).ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.HeadingsInferred), "HeadingsInferred warning expected");
                }),
                Case("ReadHeadingRatioOption", "A high HeadingSizeRatio suppresses heading inference", async ct =>
                {
                    ConversionOptions options = new ConversionOptions();
                    options.Pdf.HeadingSizeRatio = 3.0;
                    DocumentModel model = await Read(PdfFixtures.Reference, options).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, ModelQuery.Of<HeadingBlock>(model).Count, "headings with ratio 3.0");
                }),
                Case("ReadLists", "Bullets nest by indentation and the numbered list is ordered from 1", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    List<ListBlock> lists = model.Blocks.OfType<ListBlock>().ToList();
                    TestSupport.AssertEqual(2, lists.Count, "top level lists");
                    ListBlock bullets = lists[0];
                    TestSupport.AssertEqual(ListKindEnum.Unordered, bullets.Kind, "bullet list kind");
                    TestSupport.AssertEqual(3, bullets.Items.Count, "bullet items");
                    TestSupport.AssertEqual(ReferenceContent.Bullets[1], ItemText(bullets.Items[1]), "second bullet");
                    ListBlock nested = bullets.Items[1].Blocks.OfType<ListBlock>().Single();
                    TestSupport.AssertEqual(ReferenceContent.NestedBullet, ItemText(nested.Items[0]), "nested bullet");
                    ListBlock deep = nested.Items[0].Blocks.OfType<ListBlock>().Single();
                    TestSupport.AssertEqual(ReferenceContent.DeepBullet, ItemText(deep.Items[0]), "deep bullet");
                    ListBlock steps = lists[1];
                    TestSupport.AssertEqual(ListKindEnum.Ordered, steps.Kind, "ordered kind");
                    TestSupport.AssertEqual(1, steps.Start, "ordered start");
                    TestSupport.AssertEqual(3, steps.Items.Count, "ordered items");
                    TestSupport.AssertEqual(ReferenceContent.Steps[2], ItemText(steps.Items[2]), "third step");
                }),
                Case("ReadRuledTable", "The ruled table is extracted cell for cell with a header row", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    TableBlock table = ModelQuery.Of<TableBlock>(model).Single();
                    AssertTable(table);
                    TestSupport.AssertEqual(1, table.HeaderRowCount, "header rows (bold first row)");
                    string text = ModelQuery.AllText(new DocumentModel { Blocks = model.Blocks.Where(b => !(b is TableBlock)).ToList() });
                    TestSupport.AssertNotContains(text, "Grace Hopper", "table text must not repeat as paragraphs");
                }),
                Case("ReadTablesDisabled", "DetectTables false leaves table text as paragraphs", async ct =>
                {
                    ConversionOptions options = new ConversionOptions();
                    options.Pdf.DetectTables = false;
                    DocumentModel model = await Read(PdfFixtures.Reference, options).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, ModelQuery.Of<TableBlock>(model).Count, "tables");
                    TestSupport.AssertContains(ModelQuery.AllText(model), "Grace Hopper", "table text as paragraphs");
                }),
                Case("ReadInlineStyles", "Bold, italic and monospace runs keep their styles", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    List<TextInline> runs = ModelQuery.TextInlines(model);
                    AssertStyled(runs, ReferenceContent.BoldText, InlineStyleEnum.Bold);
                    AssertStyled(runs, ReferenceContent.ItalicText, InlineStyleEnum.Italic);
                    AssertStyled(runs, ReferenceContent.InlineCode, InlineStyleEnum.Code);
                }),
                Case("ReadLink", "A link annotation becomes a LinkInline with its URL and text", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    LinkInline link = ModelQuery.Links(model).Single();
                    TestSupport.AssertEqual(ReferenceContent.LinkUrl, link.Url, "link url");
                    TestSupport.AssertEqual(ReferenceContent.LinkText, ModelQuery.Text(link.Inlines), "link text");
                }),
                Case("ReadCode", "Monospace lines become one code block", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    CodeBlock code = ModelQuery.Of<CodeBlock>(model).Single();
                    TestSupport.AssertEqual(ReferenceContent.CodeText, code.Text, "code text");
                }),
                Case("ReadImage", "The embedded PNG is extracted with its pixel size", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    ImageBlock image = ModelQuery.Of<ImageBlock>(model).Single();
                    BinaryResource resource = model.Resources[image.ResourceId];
                    TestSupport.AssertEqual(ReferenceContent.ImageSize, resource.PixelWidth ?? 0, "image width");
                    TestSupport.AssertEqual(ReferenceContent.ImageSize, resource.PixelHeight ?? 0, "image height");
                    TestSupport.Assert(resource.MediaType == "image/png" || resource.MediaType == "image/jpeg", "image media type " + resource.MediaType);
                }),
                Case("ReadReadingOrder", "Blocks come out top to bottom: heading, styled paragraph, lists, table, code", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    List<Type> order = model.Blocks.Select(b => b.GetType()).ToList();
                    int h = order.IndexOf(typeof(HeadingBlock));
                    int l = order.IndexOf(typeof(ListBlock));
                    int t = order.IndexOf(typeof(TableBlock));
                    int c = order.IndexOf(typeof(CodeBlock));
                    TestSupport.Assert(h == 0 && h < l && l < t && t < c, "reading order " + string.Join(",", order.Select(x => x.Name)));
                }),
                Case("ReadSourcePages", "Blocks carry SourcePage; the second page's text is on page 2", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    ParagraphBlock closing = ModelQuery.Of<ParagraphBlock>(model).First(p => ModelQuery.Text(p.Inlines).Contains(ReferenceContent.Closing));
                    TestSupport.AssertEqual((int?)2, closing.SourcePage, "closing paragraph page");
                    TestSupport.AssertEqual((int?)1, model.Blocks[0].SourcePage, "first block page");
                }),
                Case("ReadPreservePages", "PreservePages wraps each page in a page section", async ct =>
                {
                    ConversionOptions options = new ConversionOptions();
                    options.Pdf.PreservePages = true;
                    DocumentModel model = await Read(PdfFixtures.Reference, options).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, model.Blocks.Count, "page sections");
                    foreach (Block block in model.Blocks)
                        TestSupport.AssertEqual(SectionKindEnum.Page, ((SectionBlock)block).Kind, "section kind");
                    TestSupport.AssertContains(ModelQuery.AllText(new DocumentModel { Blocks = ((SectionBlock)model.Blocks[1]).Blocks }), ReferenceContent.Closing, "page 2 content");
                }),
                Case("ReadMetadata", "Title, author and subject come from the document information", async ct =>
                {
                    DocumentModel model = await Read(PdfFixtures.Reference, null).ConfigureAwait(false);
                    TestSupport.AssertEqual(ReferenceContent.Title, model.Metadata.Title, "title");
                    TestSupport.AssertEqual(ReferenceContent.Author, model.Metadata.Author, "author");
                    TestSupport.AssertEqual(ReferenceContent.Subject, model.Metadata.Subject, "subject");
                    TestSupport.Assert(model.Metadata.CreatedUtc.HasValue, "creation date");
                }),
                Case("ReadImageOnlyNoTextLayer", "An image-only page yields its image and the NoTextLayer warning", async ct =>
                {
                    ConversionResult result = await Convert(PdfFixtures.ImageOnly, null).ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.NoTextLayer), "NoTextLayer warning");
                    DocumentModel model = await Read(PdfFixtures.ImageOnly, null).ConfigureAwait(false);
                    TestSupport.AssertEqual(1, ModelQuery.Of<ImageBlock>(model).Count, "image-only page image");
                }),
                Case("ReadEncryptedFails", "A password protected PDF throws DocumentReadException naming the password", async ct =>
                {
                    DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(PdfFixtures.Encrypted, null), "encrypted PDF").ConfigureAwait(false);
                    TestSupport.Assert(ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0, "message mentions password: " + ex.Message);
                }),
                Case("ReadCorruptFails", "A truncated PDF throws DocumentReadException", async ct =>
                {
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(PdfFixtures.Corrupt, null), "corrupt PDF").ConfigureAwait(false);
                }),
                Case("ReadEmptyFails", "Empty input declared as PDF throws DocumentReadException", async ct =>
                {
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(new byte[0], null), "empty PDF").ConfigureAwait(false);
                }),
                Case("ReadGarbageFails", "Random bytes declared as PDF throw DocumentReadException", async ct =>
                {
                    byte[] junk = new byte[4096];
                    new Random(7).NextBytes(junk);
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(junk, null), "garbage PDF").ConfigureAwait(false);
                }),
                Case("ReadFlateWrappedJpeg", "A scanned page whose JPEG is wrapped in FlateDecode yields the JPEG and NoTextLayer", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        BytesConversionResult r = await converter.ConvertToBytesAsync(PdfFixtureBuilder.BuildFlateWrappedJpeg(), DocumentFormatEnum.Pdf, DocumentFormatEnum.Json).ConfigureAwait(false);
                        DocumentModel model = await converter.ReadAsync(r.Output, DocumentFormatEnum.Json).ConfigureAwait(false);
                        BinaryResource image = model.Resources.Values.Single();
                        TestSupport.AssertEqual("image/jpeg", image.MediaType, "extracted as JPEG");
                        TestSupport.Assert(image.Data.SequenceEqual(TestImages.Sample("sample.jpg")), "the JPEG bytes are exactly the original");
                        TestSupport.AssertEqual<int?>(96, image.PixelWidth, "width");
                        TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.NoTextLayer), "NoTextLayer");
                        TestSupport.Assert(!r.Warnings.Any(w => w.Code == WarningCodeEnum.UnknownElementSkipped), "nothing skipped");
                    }
                }),
                Case("ReadRealWorldSample", "The DocumentAtom sample.pdf reads", async ct =>
                {
                    DocumentModel model = await Read(FormatFixtures.Load("RealWorld/DocumentAtom/sample.pdf"), null).ConfigureAwait(false);
                    TestSupport.Assert(model.Blocks.Count > 0, "sample.pdf produced blocks");
                }),
                Case("ReadAutoDetect", "Auto detection routes PDF bytes to the PDF reader", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        DocumentModel model = await converter.ReadAsync(PdfFixtures.Reference, DocumentFormatEnum.Auto).ConfigureAwait(false);
                        TestSupport.Assert(ModelQuery.Of<TableBlock>(model).Count == 1, "auto detected PDF table");
                    }
                }),
                Case("ReadPreCancelled", "A cancelled token throws OperationCanceledException", async ct =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    using (Converter converter = new Converter())
                    {
                        cts.Cancel();
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(PdfFixtures.Reference, DocumentFormatEnum.Pdf, null, cts.Token), "pre-cancelled read").ConfigureAwait(false);
                    }
                })
            };
        }

        /// <summary>
        /// Assert the table equals the reference rows exactly.
        /// </summary>
        /// <param name="table">Table.</param>
        public static void AssertTable(TableBlock table)
        {
            List<List<string>> cells = ModelQuery.Cells(table);
            TestSupport.AssertEqual(ReferenceContent.TableRows.Length, cells.Count, "table rows");
            for (int r = 0; r < cells.Count; r++)
            {
                TestSupport.AssertEqual(ReferenceContent.TableRows[r].Length, cells[r].Count, "cells in row " + r);
                for (int c = 0; c < cells[r].Count; c++)
                    TestSupport.AssertEqual(ReferenceContent.TableRows[r][c], cells[r][c], "cell " + r + "," + c);
            }
        }

        /// <summary>
        /// Plain text of a list item's first paragraph.
        /// </summary>
        /// <param name="item">Item.</param>
        /// <returns>Text.</returns>
        public static string ItemText(ListItemBlock item)
        {
            ParagraphBlock? p = item.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
            return p == null ? "" : ModelQuery.Text(p.Inlines);
        }

        private static void AssertHeading(List<HeadingBlock> headings, string text, int level)
        {
            HeadingBlock? heading = headings.FirstOrDefault(h => ModelQuery.Text(h.Inlines) == text);
            TestSupport.Assert(heading != null, "heading '" + text + "' found among: " + string.Join(" | ", headings.Select(h => h.Level + ":" + ModelQuery.Text(h.Inlines))));
            TestSupport.AssertEqual(level, heading!.Level, "level of heading '" + text + "'");
        }

        private static void AssertStyled(List<TextInline> runs, string text, InlineStyleEnum style)
        {
            TextInline? run = runs.FirstOrDefault(r => r.Text.Contains(text));
            TestSupport.Assert(run != null, "run '" + text + "' found");
            TestSupport.Assert((run!.Style & style) == style, "run '" + text + "' has style " + style + " (was " + run.Style + ")");
        }

        private static async Task<DocumentModel> Read(byte[] pdf, ConversionOptions? options)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(pdf, DocumentFormatEnum.Pdf, options).ConfigureAwait(false);
            }
        }

        private static async Task<ConversionResult> Convert(byte[] pdf, ConversionOptions? options)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ConvertToBytesAsync(pdf, DocumentFormatEnum.Pdf, DocumentFormatEnum.Pdf, options).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Pdf", id, name, executeAsync: body);
        }
    }
}
