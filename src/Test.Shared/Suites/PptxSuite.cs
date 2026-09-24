namespace Test.Shared.Suites
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
    using Test.Shared.Inspection;
    using Test.Shared.Suites.Xlsx;
    using Touchstone.Core;

    /// <summary>
    /// PPTX reader and writer tests.
    /// </summary>
    public static class PptxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            AddReaderCases(cases);
            AddNegativeCases(cases);
            AddWriterCases(cases);
            return new TestSuiteDescriptor(suiteId: "Pptx", displayName: "PPTX reader and writer", cases: cases);
        }

        private static void AddReaderCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("ReadSlidesAsSections", "Each slide becomes a slide section titled by its title placeholder", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                List<SectionBlock> slides = doc.Blocks.OfType<SectionBlock>().ToList();
                TestSupport.AssertEqual(4, slides.Count, "slide count");
                TestSupport.Assert(slides.All(s => s.Kind == SectionKindEnum.Slide), "slide sections");
                string[] titles = new string[] { ReferenceContent.Heading1, ReferenceContent.HeadingLists, ReferenceContent.HeadingTable, PptxFixtureBuilder.MediaTitle };
                for (int i = 0; i < 4; i++)
                {
                    TestSupport.AssertEqual(titles[i], slides[i].Title, "title " + i);
                    TestSupport.AssertEqual(i + 1, slides[i].SourceSlide ?? 0, "source slide " + i);
                }
            }));

            cases.Add(Case("TitleAndSubtitle", "Title placeholders become level 1 headings and subtitles level 2", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                SectionBlock first = doc.Blocks.OfType<SectionBlock>().First();
                HeadingBlock h1 = (HeadingBlock)first.Blocks[0];
                HeadingBlock h2 = (HeadingBlock)first.Blocks[1];
                TestSupport.AssertEqual(1, h1.Level, "title level");
                TestSupport.AssertEqual(ReferenceContent.Heading1, OfficeSuiteSupport.Text(h1), "title text");
                TestSupport.AssertEqual(2, h2.Level, "subtitle level");
                TestSupport.AssertEqual(PptxFixtureBuilder.Subtitle, OfficeSuiteSupport.Text(h2), "subtitle text");
            }));

            cases.Add(Case("NotesOffByDefault", "Speaker notes are excluded unless IncludeNotes is set", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(OfficeSuiteSupport.AllText(doc), PptxFixtureBuilder.Notes, "notes excluded");
            }));

            cases.Add(Case("IncludeNotes", "IncludeNotes adds a Notes section and raises NotesIncluded", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Pptx.IncludeNotes = true;
                using (Converter converter = new Converter())
                {
                    BytesConversionResult result = await converter.ConvertToBytesAsync(PptxFixtureBuilder.BuildReference(), DocumentFormatEnum.Pptx, DocumentFormatEnum.Pptx, options, ct).ConfigureAwait(false);
                    TestSupport.Assert(PptxInspector.Inspect(result.Output).ContainsText(PptxFixtureBuilder.Notes), "notes carried into output");
                    TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.NotesIncluded), "NotesIncluded warning");
                }

                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), options, ct).ConfigureAwait(false);
                SectionBlock notes = doc.Blocks.OfType<SectionBlock>().First().Blocks.OfType<SectionBlock>().Single();
                TestSupport.AssertEqual("Notes", notes.Title, "notes section title");
                TestSupport.AssertContains(OfficeSuiteSupport.Text(notes), PptxFixtureBuilder.Notes, "notes text");
            }));

            cases.Add(Case("NestedBullets", "Bullet levels become nested unordered lists and auto numbering an ordered list", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                SectionBlock slide = doc.Blocks.OfType<SectionBlock>().ElementAt(1);
                List<ListBlock> lists = slide.Blocks.OfType<ListBlock>().ToList();
                TestSupport.AssertEqual(2, lists.Count, "two top level lists");
                ListBlock bullets = lists[0];
                TestSupport.AssertEqual(ListKindEnum.Unordered, bullets.Kind, "unordered");
                TestSupport.AssertEqual(3, bullets.Items.Count, "three top level bullets");
                ListBlock nested = bullets.Items[1].Blocks.OfType<ListBlock>().Single();
                TestSupport.AssertEqual(ReferenceContent.NestedBullet, OfficeSuiteSupport.Text(nested.Items[0].Blocks[0]), "nested bullet");
                ListBlock deep = nested.Items[0].Blocks.OfType<ListBlock>().Single();
                TestSupport.AssertEqual(ReferenceContent.DeepBullet, OfficeSuiteSupport.Text(deep.Items[0].Blocks[0]), "deep bullet");
                ListBlock steps = lists[1];
                TestSupport.AssertEqual(ListKindEnum.Ordered, steps.Kind, "ordered");
                TestSupport.AssertEqual(3, steps.Items.Count, "three steps");
                TestSupport.AssertEqual(ReferenceContent.Steps[1], OfficeSuiteSupport.Text(steps.Items[1].Blocks[0]), "step two");
            }));

            cases.Add(Case("Table", "Slide tables become tables with the first row as header", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                TableBlock table = OfficeSuiteSupport.Find<TableBlock>(doc).Single();
                TestSupport.AssertEqual(1, table.HeaderRowCount, "header row");
                List<List<string>> cells = OfficeSuiteSupport.Cells(table);
                for (int r = 0; r < ReferenceContent.TableRows.Length; r++)
                    for (int c = 0; c < 3; c++)
                        TestSupport.AssertEqual(ReferenceContent.TableRows[r][c], cells[r][c], "cell " + r + "," + c);
            }));

            cases.Add(Case("Picture", "Pictures become image blocks with alt text and pixel size", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                ImageBlock image = OfficeSuiteSupport.Find<ImageBlock>(doc).Single();
                TestSupport.AssertEqual(ReferenceContent.ImageAlt, image.AltText, "alt");
                TestSupport.AssertEqual(ReferenceContent.ImageSize, doc.Resources[image.ResourceId].PixelWidth ?? 0, "width");
                TestSupport.AssertEqual(4, image.SourceSlide ?? 0, "source slide");
            }));

            cases.Add(Case("RunStylesAndLinks", "Bold, italic, monospace runs and hyperlinks are preserved", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                ParagraphBlock styled = OfficeSuiteSupport.Find<ParagraphBlock>(doc).First(p => OfficeSuiteSupport.Text(p).StartsWith(ReferenceContent.StyledLead, StringComparison.Ordinal));
                List<TextInline> runs = styled.Inlines.OfType<TextInline>().ToList();
                TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.BoldText && (r.Style & InlineStyleEnum.Bold) != 0), "bold");
                TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.ItalicText && (r.Style & InlineStyleEnum.Italic) != 0), "italic");
                TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.InlineCode && (r.Style & InlineStyleEnum.Code) != 0), "code");
                LinkInline link = styled.Inlines.OfType<LinkInline>().Single();
                TestSupport.AssertEqual(ReferenceContent.LinkUrl, link.Url, "link url");
                TestSupport.AssertEqual(ReferenceContent.LinkText, ((TextInline)link.Inlines[0]).Text, "link text");
            }));

            cases.Add(Case("PositionOrdering", "Shapes are ordered top to bottom regardless of XML order", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                SectionBlock media = doc.Blocks.OfType<SectionBlock>().Last();
                List<Block> blocks = media.Blocks;
                TestSupport.Assert(blocks[0] is HeadingBlock, "title first");
                TestSupport.AssertEqual(ReferenceContent.Closing, OfficeSuiteSupport.Text(blocks[blocks.Count - 1]), "closing text last");
                int picture = blocks.FindIndex(b => b is ImageBlock);
                int quote = blocks.FindIndex(b => OfficeSuiteSupport.Text(b) == ReferenceContent.QuoteText);
                TestSupport.Assert(quote >= 0 && picture > quote, "picture after the text box above it");
            }));

            cases.Add(Case("Metadata", "Core properties become metadata", async ct =>
            {
                DocumentModel doc = await Read(PptxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(ReferenceContent.Title, doc.Metadata.Title, "title");
                TestSupport.AssertEqual(ReferenceContent.Author, doc.Metadata.Author, "author");
            }));

            cases.Add(Case("RealWorldSample", "DocumentAtom's generated sample.pptx reads and yields slides", async ct =>
            {
                DocumentModel doc = await Read(OfficeSuiteSupport.Fixture("RealWorld/DocumentAtom/sample.pptx"), null, ct).ConfigureAwait(false);
                TestSupport.Assert(doc.Blocks.OfType<SectionBlock>().Any(), "has slides");
                TestSupport.Assert(OfficeSuiteSupport.AllText(doc).Trim().Length > 0, "has text");
            }));
        }

        private static void AddNegativeCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("TruncatedZip", "A truncated PPTX throws DocumentReadException", async ct =>
            {
                byte[] full = PptxFixtureBuilder.BuildReference();
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(full.Take(full.Length / 2).ToArray(), null, ct), "truncated").ConfigureAwait(false);
            }));

            cases.Add(Case("MissingPresentation", "A zip without a presentation part throws DocumentReadException", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(PptxFixtureBuilder.BuildMissingPresentation(), null, ct), "missing").ConfigureAwait(false);
            }));

            cases.Add(Case("PasswordProtected", "An encrypted file throws DocumentReadException naming password protection", async ct =>
            {
                DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(XlsxFixtureBuilder.BuildEncrypted(), null, ct), "encrypted").ConfigureAwait(false);
                TestSupport.AssertContains(ex.Message, "password", "message");
            }));

            cases.Add(Case("ZipBombGuard", "Parts larger than MaxDecompressedBytes throw InputTooLargeException", async ct =>
            {
                using (Converter converter = new Converter(new ConverterSettings { MaxDecompressedBytes = 100 }))
                {
                    await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => converter.ReadAsync(PptxFixtureBuilder.BuildReference(), DocumentFormatEnum.Pptx, null, ct), "zip bomb").ConfigureAwait(false);
                }
            }));

            cases.Add(Case("PreCancelled", "A cancelled token throws OperationCanceledException", async ct =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource())
                using (Converter converter = new Converter())
                {
                    cts.Cancel();
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(PptxFixtureBuilder.BuildReference(), DocumentFormatEnum.Pptx, null, cts.Token), "read").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Pptx, null, cts.Token), "write").ConfigureAwait(false);
                }
            }));
        }

        private static void AddWriterCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("WriteReference", "The reference model writes a valid deck with titles, bullets, table, picture and link", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                foreach (string title in new string[] { ReferenceContent.Heading1, ReferenceContent.HeadingLists, ReferenceContent.HeadingTable })
                    TestSupport.Assert(snapshot.Headings.Contains(title), "slide title " + title + " in " + string.Join("|", snapshot.Headings));
                foreach (string item in new string[] { ReferenceContent.Bullets[0], ReferenceContent.NestedBullet, ReferenceContent.DeepBullet, ReferenceContent.Steps[2] })
                    TestSupport.Assert(snapshot.ListItems.Contains(item), "list item " + item);
                foreach (string[] row in ReferenceContent.TableRows)
                    TestSupport.Assert(snapshot.HasTableRow(row), "table row " + string.Join("|", row));
                foreach (string snippet in ReferenceContent.CoreTextSnippets)
                    TestSupport.Assert(snapshot.ContainsText(snippet), "text " + snippet);
                TestSupport.AssertEqual(1, snapshot.ImageCount, "picture");
                TestSupport.Assert(snapshot.LinkUrls.Contains(ReferenceContent.LinkUrl), "link");
                TestSupport.AssertEqual(ReferenceContent.Title, snapshot.Title, "title");
                TestSupport.Assert(snapshot.ContainsText(ReferenceContent.Special), "special characters");
            }));

            cases.Add(Case("SlideSplitLevel", "SlideSplitHeadingLevel 1 keeps level 2 headings inside the slide body", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Pptx.SlideSplitHeadingLevel = 1;
                options.Pptx.MaxBlocksPerSlide = 100;
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.Assert(!snapshot.Headings.Contains(ReferenceContent.HeadingLists), "Lists is not a slide title");
                TestSupport.Assert(snapshot.ContainsText(ReferenceContent.HeadingLists), "Lists text still present");
            }));

            cases.Add(Case("MaxBlocksPerSlide", "Content beyond MaxBlocksPerSlide continues on (continued) slides", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                doc.Blocks.Add(new HeadingBlock(1, "Many"));
                for (int i = 1; i <= 7; i++) doc.Blocks.Add(new ParagraphBlock("Paragraph " + i));
                ConversionOptions options = new ConversionOptions();
                options.Pptx.MaxBlocksPerSlide = 2;
                BytesConversionResult result = await Write(doc, options, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(4, PptxInspector.SlideCount(result.Output), "slides");
                TestSupport.AssertEqual("Many (continued)", snapshot.Headings[1], "continued title");
                TestSupport.Assert(snapshot.ContainsText("Paragraph 7"), "last paragraph kept");
            }));

            cases.Add(Case("TableSplitRepeatsHeader", "Tables longer than MaxTableRowsPerSlide split with the header row repeated", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                doc.Blocks.Add(new HeadingBlock(1, "Big table"));
                TableBlock table = new TableBlock { HeaderRowCount = 1 };
                table.Rows.Add(new TableRow(new string[] { "Id", "Value" }));
                for (int i = 1; i <= 20; i++) table.Rows.Add(new TableRow(new string[] { i.ToString(System.Globalization.CultureInfo.InvariantCulture), "v" + i }));
                doc.Blocks.Add(table);
                ConversionOptions options = new ConversionOptions();
                options.Pptx.MaxTableRowsPerSlide = 5;
                BytesConversionResult result = await Write(doc, options, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(5, PptxInspector.SlideCount(result.Output), "slides");
                TestSupport.AssertEqual(5, snapshot.TableRows.Count(r => r.Count > 0 && r[0] == "Id"), "header repeated on each chunk");
                TestSupport.Assert(snapshot.HasTableRow(new string[] { "20", "v20" }), "last row kept");
            }));

            cases.Add(Case("Deterministic", "Deterministic output is byte-identical across runs", async ct =>
            {
                ConversionOptions options = new ConversionOptions { Deterministic = true };
                BytesConversionResult a = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                await Task.Delay(1100, ct).ConfigureAwait(false);
                BytesConversionResult b = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                TestSupport.Assert(a.Output.SequenceEqual(b.Output), "identical bytes: " + OfficeSuiteSupport.DescribeZipDifference(a.Output, b.Output));
                PptxInspector.Inspect(a.Output);
            }));

            cases.Add(Case("EmptyDocument", "An empty document writes one valid slide", async ct =>
            {
                BytesConversionResult result = await Write(new DocumentModel(), null, ct).ConfigureAwait(false);
                PptxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(1, PptxInspector.SlideCount(result.Output), "one slide");
            }));

            cases.Add(Case("TitleOnlyDocument", "A lone level 1 heading becomes a title slide", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                doc.Blocks.Add(new HeadingBlock(1, "Only a title"));
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(1, PptxInspector.SlideCount(result.Output), "one slide");
                TestSupport.AssertEqual("Only a title", snapshot.Headings.Single(), "title");
            }));

            cases.Add(Case("UnsafeLinkRemoved", "javascript: links are dropped with LinkRemovedUnsafe and the text kept", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                ParagraphBlock p = new ParagraphBlock();
                p.Inlines.Add(new LinkInline("javascript:alert(1)", "click me"));
                doc.Blocks.Add(p);
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.LinkRemovedUnsafe), "warning");
                TestSupport.AssertEqual(0, snapshot.LinkUrls.Count, "no link relationship");
                TestSupport.Assert(snapshot.ContainsText("click me"), "text kept");
            }));

            cases.Add(Case("MissingImageResource", "An image with a missing resource is skipped with ImagesOmitted", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                doc.Blocks.Add(new ImageBlock("nope", "missing"));
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "warning");
                TestSupport.AssertEqual(0, snapshot.ImageCount, "no picture");
            }));

            cases.Add(Case("SpansInTables", "Table spans write valid merged cells", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                TableBlock table = new TableBlock { HeaderRowCount = 1 };
                TableRow header = new TableRow(new string[] { "Wide", "C" });
                header.Cells[0].ColumnSpan = 2;
                table.Rows.Add(header);
                TableRow r1 = new TableRow(new string[] { "Tall", "b1", "c1" });
                r1.Cells[0].RowSpan = 2;
                table.Rows.Add(r1);
                table.Rows.Add(new TableRow(new string[] { "b2", "c2" }));
                doc.Blocks.Add(table);
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                PptxInspector.Inspect(result.Output);
                DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                TableBlock read = OfficeSuiteSupport.Find<TableBlock>(back).Single();
                TestSupport.AssertEqual(2, read.Rows[0].Cells[0].ColumnSpan, "column span");
                TestSupport.AssertEqual(2, read.Rows[1].Cells[0].RowSpan, "row span");
                TestSupport.AssertEqual("b2", OfficeSuiteSupport.Cells(read)[2][0], "cell after covered position");
            }));

            cases.Add(Case("RoundTripModel", "Model to PPTX to model keeps slide titles and bullets", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                List<string> titles = back.Blocks.OfType<SectionBlock>().Select(s => s.Title ?? "").ToList();
                TestSupport.Assert(titles.Contains(ReferenceContent.HeadingLists) && titles.Contains(ReferenceContent.HeadingTable), "titles " + string.Join("|", titles));
                List<ListBlock> lists = OfficeSuiteSupport.Find<ListBlock>(back);
                TestSupport.Assert(lists.Any(l => l.Kind == ListKindEnum.Ordered && l.Items.Count == 3), "ordered list");
                string text = OfficeSuiteSupport.AllText(back);
                TestSupport.AssertContains(text, ReferenceContent.DeepBullet, "deep bullet");
                ListBlock bullets = lists.First(l => l.Kind == ListKindEnum.Unordered && l.Items.Count == 3);
                TestSupport.Assert(bullets.Items[1].Blocks.OfType<ListBlock>().Any(), "nesting kept");
            }));

            cases.Add(Case("RoundTripDeck", "PPTX to PPTX keeps every slide title and the title slide subtitle", async ct =>
            {
                using (Converter converter = new Converter())
                {
                    BytesConversionResult result = await converter.ConvertToBytesAsync(PptxFixtureBuilder.BuildReference(), DocumentFormatEnum.Pptx, DocumentFormatEnum.Pptx, null, ct).ConfigureAwait(false);
                    ContentSnapshot snapshot = PptxInspector.Inspect(result.Output);
                    TestSupport.AssertEqual(4, PptxInspector.SlideCount(result.Output), "four slides");
                    TestSupport.Assert(snapshot.Headings.Take(4).SequenceEqual(new string[] { ReferenceContent.Heading1, ReferenceContent.HeadingLists, ReferenceContent.HeadingTable, PptxFixtureBuilder.MediaTitle }), "titles " + string.Join("|", snapshot.Headings));
                    TestSupport.Assert(snapshot.ContainsText(PptxFixtureBuilder.Subtitle), "subtitle");
                    TestSupport.AssertEqual(1, snapshot.ImageCount, "picture");
                }
            }));
        }

        private static async Task<DocumentModel> Read(byte[] bytes, ConversionOptions? options, CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(bytes, DocumentFormatEnum.Pptx, options, token).ConfigureAwait(false);
            }
        }

        private static async Task<BytesConversionResult> Write(DocumentModel document, ConversionOptions? options, CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                return await converter.WriteToBytesAsync(document, DocumentFormatEnum.Pptx, options, token).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Pptx", id, name, executeAsync: body);
        }
    }
}
