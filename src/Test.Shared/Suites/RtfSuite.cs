namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;
    using Test.Shared.Suites.Pdf;
    using Touchstone.Core;

    /// <summary>
    /// RTF reader tests.
    /// </summary>
    public static class RtfSuite
    {
        private static readonly Lazy<string> _Reference = new Lazy<string>(RtfFixtureBuilder.BuildReference);

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("Headings", "Headings come from outline levels and heading N style names", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    List<HeadingBlock> headings = ModelQuery.Of<HeadingBlock>(model);
                    TestSupport.AssertEqual(4, headings.Count, "heading count");
                    AssertHeading(headings[0], ReferenceContent.Heading1, 1);
                    AssertHeading(headings[1], ReferenceContent.HeadingLists, 2);
                    AssertHeading(headings[2], ReferenceContent.HeadingTable, 2);
                    AssertHeading(headings[3], ReferenceContent.HeadingCode, 3);
                }),
                Case("InlineStyles", "Bold, italic, underline, strike and monospace runs keep their styles", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    List<TextInline> runs = ModelQuery.TextInlines(model);
                    AssertRun(runs, ReferenceContent.BoldText, InlineStyleEnum.Bold);
                    AssertRun(runs, ReferenceContent.ItalicText, InlineStyleEnum.Italic);
                    AssertRun(runs, ReferenceContent.UnderlineText, InlineStyleEnum.Underline);
                    AssertRun(runs, ReferenceContent.StrikeText, InlineStyleEnum.Strikethrough);
                    AssertRun(runs, ReferenceContent.InlineCode, InlineStyleEnum.Code);
                }),
                Case("StyledParagraphText", "The styled paragraph reads back exactly with spaces preserved", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    ParagraphBlock p = ModelQuery.Of<ParagraphBlock>(model).First(x => ModelQuery.Text(x.Inlines).StartsWith(ReferenceContent.StyledLead.Trim(), StringComparison.Ordinal));
                    string expected = ReferenceContent.StyledLead + ReferenceContent.BoldText + ", " + ReferenceContent.ItalicText + ", " + ReferenceContent.UnderlineText
                        + ", " + ReferenceContent.StrikeText + ", " + ReferenceContent.InlineCode + " and a " + ReferenceContent.LinkText + ".";
                    TestSupport.AssertEqual(expected, ModelQuery.Text(p.Inlines), "styled paragraph");
                }),
                Case("Hyperlink", "The HYPERLINK field becomes a link with its URL and text", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    LinkInline link = ModelQuery.Links(model).Single();
                    TestSupport.AssertEqual(ReferenceContent.LinkUrl, link.Url, "url");
                    TestSupport.AssertEqual(ReferenceContent.LinkText, ModelQuery.Text(link.Inlines), "text");
                    TestSupport.Assert(((TextInline)link.Inlines[0]).Style == InlineStyleEnum.Underline, "link run underlined");
                }),
                Case("Lists", "Bullets nest by \\ilvl and the numbered list starts at 1", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    List<ListBlock> lists = model.Blocks.OfType<ListBlock>().ToList();
                    TestSupport.AssertEqual(2, lists.Count, "top level lists");
                    TestSupport.AssertEqual(ListKindEnum.Unordered, lists[0].Kind, "bullets");
                    TestSupport.AssertEqual(3, lists[0].Items.Count, "bullet items");
                    TestSupport.AssertEqual(ReferenceContent.Bullets[2], PdfReaderCases.ItemText(lists[0].Items[2]), "third bullet");
                    ListBlock nested = lists[0].Items[1].Blocks.OfType<ListBlock>().Single();
                    TestSupport.AssertEqual(ReferenceContent.NestedBullet, PdfReaderCases.ItemText(nested.Items[0]), "nested");
                    ListBlock deep = nested.Items[0].Blocks.OfType<ListBlock>().Single();
                    TestSupport.AssertEqual(ReferenceContent.DeepBullet, PdfReaderCases.ItemText(deep.Items[0]), "deep");
                    TestSupport.AssertEqual(ListKindEnum.Ordered, lists[1].Kind, "numbered");
                    TestSupport.AssertEqual(1, lists[1].Start, "start");
                    TestSupport.AssertEqual(3, lists[1].Items.Count, "steps");
                    TestSupport.AssertEqual(ReferenceContent.Steps[1], PdfReaderCases.ItemText(lists[1].Items[1]), "step two");
                }),
                Case("Table", "The table reads cell for cell with a \\trhdr header row", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    TableBlock table = ModelQuery.Of<TableBlock>(model).Single();
                    PdfReaderCases.AssertTable(table);
                    TestSupport.AssertEqual(1, table.HeaderRowCount, "header rows");
                    TestSupport.Assert(table.Rows[0].Cells.All(c => c.IsHeader), "header cells flagged");
                    TestSupport.Assert(!table.Rows[1].Cells.Any(c => c.IsHeader), "body cells not flagged");
                }),
                Case("Code", "Paragraphs in a monospace font become one code block", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    CodeBlock code = ModelQuery.Of<CodeBlock>(model).Single();
                    TestSupport.AssertEqual(ReferenceContent.CodeText, code.Text, "code");
                }),
                Case("Picture", "The PNG picture becomes an image block sized from \\picwgoal; the WMF fallback is ignored", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    ImageBlock image = ModelQuery.Of<ImageBlock>(model).Single();
                    BinaryResource resource = model.Resources[image.ResourceId];
                    TestSupport.AssertEqual("image/png", resource.MediaType, "media type");
                    TestSupport.Assert(resource.Data.SequenceEqual(ReferenceContent.ImagePng()), "PNG bytes intact");
                    TestSupport.AssertEqual((double?)24.0, image.Width, "width in points from twips");
                    TestSupport.AssertEqual(1, model.Resources.Count, "only one resource");
                }),
                Case("UnicodeText", "\\u escapes (including surrogate pairs) decode exactly", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.Of<ParagraphBlock>(model).Any(p => ModelQuery.Text(p.Inlines) == ReferenceContent.International), "international paragraph exact");
                }),
                Case("CodePageText", "\\'hh escapes decode through Windows-1252", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.Of<ParagraphBlock>(model).Any(p => ModelQuery.Text(p.Inlines) == RtfFixtureBuilder.CodePageText), "code page paragraph exact");
                }),
                Case("SpecialCharacters", "Markup-like characters come through as plain text", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    TestSupport.Assert(ModelQuery.Of<ParagraphBlock>(model).Any(p => ModelQuery.Text(p.Inlines) == ReferenceContent.Special), "special paragraph exact");
                }),
                Case("QuoteAndClosing", "The italic quote and closing paragraphs are present", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    List<TextInline> runs = ModelQuery.TextInlines(model);
                    AssertRun(runs, ReferenceContent.QuoteText, InlineStyleEnum.Italic);
                    TestSupport.AssertContains(ModelQuery.AllText(model), ReferenceContent.Closing, "closing");
                }),
                Case("Metadata", "\\info supplies title, author, subject and creation time", async ct =>
                {
                    DocumentModel model = await ReadReference().ConfigureAwait(false);
                    TestSupport.AssertEqual(ReferenceContent.Title, model.Metadata.Title, "title");
                    TestSupport.AssertEqual(ReferenceContent.Author, model.Metadata.Author, "author");
                    TestSupport.AssertEqual(ReferenceContent.Subject, model.Metadata.Subject, "subject");
                    TestSupport.AssertEqual((DateTime?)new DateTime(2024, 5, 6, 7, 8, 0, DateTimeKind.Utc), model.Metadata.CreatedUtc, "created");
                }),
                Case("ByteInputRaw8Bit", "Byte input with raw 8-bit characters decodes through the code page", async ct =>
                {
                    byte[] bytes = new byte[] { (byte)'{', (byte)'\\', (byte)'r', (byte)'t', (byte)'f', (byte)'1', (byte)'\\', (byte)'a', (byte)'n', (byte)'s', (byte)'i', (byte)' ', (byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)' ', 0x80, (byte)'5', (byte)'\\', (byte)'p', (byte)'a', (byte)'r', (byte)'}' };
                    using (Converter converter = new Converter())
                    {
                        DocumentModel model = await converter.ReadAsync(bytes, DocumentFormatEnum.Rtf).ConfigureAwait(false);
                        TestSupport.AssertEqual("Caf\u00E9 \u20AC5", ModelQuery.AllText(model), "decoded text");
                    }
                }),
                Case("ByteInputReference", "The reference RTF read from ASCII bytes equals the string read", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        DocumentModel fromBytes = await converter.ReadAsync(Encoding.ASCII.GetBytes(_Reference.Value), DocumentFormatEnum.Rtf).ConfigureAwait(false);
                        DocumentModel fromString = await ReadReference().ConfigureAwait(false);
                        TestSupport.AssertEqual(ModelQuery.AllText(fromString), ModelQuery.AllText(fromBytes), "bytes and string agree");
                    }
                }),
                Case("UnicodeSkipCount", "\\uc2 skips two fallback characters after each \\u", async ct =>
                {
                    DocumentModel model = await Read("{\\rtf1\\ansi\\uc2 A\\u8364\\'80\\'80B\\uc0\\u8364 C\\par}").ConfigureAwait(false);
                    TestSupport.AssertEqual("A\u20ACB\u20ACC", ModelQuery.AllText(model), "uc handling");
                }),
                Case("HiddenText", "\\v hidden text is dropped", async ct =>
                {
                    DocumentModel model = await Read("{\\rtf1\\ansi Visible {\\v secret }text\\par}").ConfigureAwait(false);
                    TestSupport.AssertEqual("Visible text", ModelQuery.AllText(model), "hidden dropped");
                }),
                Case("MergedCells", "\\clmgf and \\clmrg merge cells horizontally; \\clvmgf and \\clvmrg vertically", async ct =>
                {
                    string rtf = "{\\rtf1\\ansi "
                        + "\\trowd\\clmgf\\cellx2000\\clmrg\\cellx4000\\clvmgf\\cellx6000\\pard\\intbl Wide\\cell \\cell Tall\\cell\\row "
                        + "\\trowd\\cellx2000\\cellx4000\\clvmrg\\cellx6000\\pard\\intbl a\\cell b\\cell \\cell\\row "
                        + "\\pard After\\par}";
                    DocumentModel model = await Read(rtf).ConfigureAwait(false);
                    TableBlock table = ModelQuery.Of<TableBlock>(model).Single();
                    TestSupport.AssertEqual(2, table.Rows[0].Cells.Count, "first row cells");
                    TestSupport.AssertEqual(2, table.Rows[0].Cells[0].ColumnSpan, "column span");
                    TestSupport.AssertEqual(2, table.Rows[0].Cells[1].RowSpan, "row span");
                    TestSupport.AssertEqual(2, table.Rows[1].Cells.Count, "second row cells");
                    TestSupport.Assert(model.Blocks.Last() is ParagraphBlock, "paragraph after table");
                }),
                Case("UnbalancedBraces", "Extra closing braces are tolerated", async ct =>
                {
                    DocumentModel model = await Read("{\\rtf1\\ansi Hello {\\b world}\\par}}}}").ConfigureAwait(false);
                    TestSupport.AssertEqual("Hello world", ModelQuery.AllText(model), "text");
                }),
                Case("MissingClosingBrace", "A document missing its final brace still reads", async ct =>
                {
                    DocumentModel model = await Read("{\\rtf1\\ansi Unterminated document").ConfigureAwait(false);
                    TestSupport.AssertEqual("Unterminated document", ModelQuery.AllText(model), "text");
                }),
                Case("NotRtf", "Input without the {\\rtf header throws DocumentReadException", async ct =>
                {
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read("Just some text"), "not rtf").ConfigureAwait(false);
                }),
                Case("GarbageBytes", "Random bytes declared as RTF throw DocumentReadException", async ct =>
                {
                    byte[] junk = new byte[512];
                    new Random(11).NextBytes(junk);
                    junk[0] = (byte)'x';
                    using (Converter converter = new Converter())
                    {
                        await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => converter.ReadAsync(junk, DocumentFormatEnum.Rtf), "garbage").ConfigureAwait(false);
                    }
                }),
                Case("FootnoteSkipped", "Footnotes are skipped with a FormattingLost warning", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        BytesConversionResult result = await converter.ConvertToBytesAsync("{\\rtf1\\ansi Body{\\footnote Note text} end\\par}", DocumentFormatEnum.Rtf, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                        TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.FormattingLost), "FormattingLost");
                        DocumentModel model = await converter.ReadAsync("{\\rtf1\\ansi Body{\\footnote Note text} end\\par}", DocumentFormatEnum.Rtf).ConfigureAwait(false);
                        TestSupport.AssertEqual("Body end", ModelQuery.AllText(model), "footnote text excluded");
                    }
                }),
                Case("UnsupportedPicture", "A WMF-only picture is skipped with UnknownElementSkipped", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        BytesConversionResult result = await converter.ConvertToBytesAsync("{\\rtf1\\ansi {\\pict\\wmetafile8 0102}Text\\par}", DocumentFormatEnum.Rtf, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                        TestSupport.Assert(ModelQuery.HasWarning(result, WarningCodeEnum.UnknownElementSkipped), "UnknownElementSkipped");
                    }
                }),
                Case("RealWorldSample", "The DocumentAtom sample.rtf reads", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        DocumentModel model = await converter.ReadAsync(FormatFixtures.Load("RealWorld/DocumentAtom/sample.rtf"), DocumentFormatEnum.Rtf).ConfigureAwait(false);
                        TestSupport.Assert(ModelQuery.AllText(model).Length > 0, "sample.rtf text");
                    }
                }),
                Case("AutoDetect", "Auto detection routes RTF to the RTF reader", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        DocumentModel model = await converter.ReadAsync(_Reference.Value, DocumentFormatEnum.Auto).ConfigureAwait(false);
                        TestSupport.AssertEqual(1, ModelQuery.Of<TableBlock>(model).Count, "auto detected RTF");
                    }
                }),
                Case("ToPdf", "The reference RTF converts to PDF with its text", async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        BytesConversionResult result = await converter.ConvertToBytesAsync(_Reference.Value, DocumentFormatEnum.Rtf, DocumentFormatEnum.Pdf).ConfigureAwait(false);
                        Inspection.ContentSnapshot snapshot = Inspection.PdfInspector.Inspect(result.Output);
                        TestSupport.Assert(snapshot.ContainsText(ReferenceContent.Closing) && snapshot.ContainsText("Grace Hopper"), "RTF to PDF text");
                        TestSupport.AssertEqual(1, snapshot.ImageCount, "RTF to PDF image");
                    }
                }),
                Case("PreCancelled", "A cancelled token throws OperationCanceledException", async ct =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    using (Converter converter = new Converter())
                    {
                        cts.Cancel();
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(_Reference.Value, DocumentFormatEnum.Rtf, null, cts.Token), "cancelled").ConfigureAwait(false);
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: "Rtf",
                displayName: "RTF reader",
                cases: cases);
        }

        private static Task<DocumentModel> ReadReference()
        {
            return Read(_Reference.Value);
        }

        private static async Task<DocumentModel> Read(string rtf)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(rtf, DocumentFormatEnum.Rtf).ConfigureAwait(false);
            }
        }

        private static void AssertHeading(HeadingBlock heading, string text, int level)
        {
            TestSupport.AssertEqual(text, ModelQuery.Text(heading.Inlines), "heading text");
            TestSupport.AssertEqual(level, heading.Level, "level of '" + text + "'");
        }

        private static void AssertRun(List<TextInline> runs, string text, InlineStyleEnum style)
        {
            TextInline? run = runs.FirstOrDefault(r => r.Text.Contains(text));
            TestSupport.Assert(run != null, "run '" + text + "' found");
            TestSupport.Assert((run!.Style & style) == style, "run '" + text + "' has " + style + " (was " + run.Style + ")");
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Rtf", id, name, executeAsync: body);
        }
    }
}
