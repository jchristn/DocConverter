namespace Test.Shared.Suites.Docx
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
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;

    /// <summary>
    /// DOCX reader cases against fixtures built with the raw OpenXml SDK.
    /// </summary>
    public static class DocxReaderCases
    {
        private static readonly Lazy<byte[]> _Reference = new Lazy<byte[]>(DocxFixtureBuilder.BuildReference);

        /// <summary>
        /// The reference fixture bytes (built once).
        /// </summary>
        public static byte[] Reference
        {
            get => _Reference.Value;
        }

        /// <summary>
        /// Read DOCX bytes with default options.
        /// </summary>
        /// <param name="docx">DOCX bytes.</param>
        /// <param name="options">Options, or null.</param>
        /// <returns>Model.</returns>
        public static async Task<DocumentModel> ReadAsync(byte[] docx, ConversionOptions? options = null)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(docx, DocumentFormatEnum.Docx, options).ConfigureAwait(false);
            }
        }

        /// <summary>Headings keep their levels and text.</summary>
        public static async Task Headings(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            List<HeadingBlock> headings = ModelQuery.All<HeadingBlock>(doc.Blocks);
            TestSupport.AssertEqual(4, headings.Count, "heading count");
            TestSupport.AssertEqual(ReferenceContent.Heading1, ModelQuery.Text(headings[0].Inlines), "h1 text");
            TestSupport.AssertEqual(1, headings[0].Level, "h1 level");
            TestSupport.AssertEqual(2, headings[1].Level, "lists heading level");
            TestSupport.AssertEqual(2, headings[2].Level, "table heading level");
            TestSupport.AssertEqual(3, headings[3].Level, "code heading level");
            TestSupport.AssertEqual(ReferenceContent.HeadingCode, ModelQuery.Text(headings[3].Inlines), "h3 text");
        }

        /// <summary>Bold, italic, underline, strike and monospace runs map to inline styles.</summary>
        public static async Task InlineStyles(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            TestSupport.Assert(ModelQuery.StyledTexts(doc, InlineStyleEnum.Bold).Contains(ReferenceContent.BoldText), "bold run");
            TestSupport.Assert(ModelQuery.StyledTexts(doc, InlineStyleEnum.Italic).Contains(ReferenceContent.ItalicText), "italic run");
            TestSupport.Assert(ModelQuery.StyledTexts(doc, InlineStyleEnum.Underline).Contains(ReferenceContent.UnderlineText), "underline run");
            TestSupport.Assert(ModelQuery.StyledTexts(doc, InlineStyleEnum.Strikethrough).Contains(ReferenceContent.StrikeText), "strike run");
            TestSupport.Assert(ModelQuery.StyledTexts(doc, InlineStyleEnum.Code).Contains(ReferenceContent.InlineCode), "code run");
            TestSupport.Assert(!ModelQuery.StyledTexts(doc, InlineStyleEnum.Underline).Contains(ReferenceContent.LinkText), "hyperlink character style must not read as underline");
        }

        /// <summary>A relationship hyperlink becomes a link with its URL and text.</summary>
        public static async Task Hyperlink(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            List<LinkInline> links = ModelQuery.Links(doc);
            TestSupport.AssertEqual(1, links.Count, "link count");
            TestSupport.AssertEqual(ReferenceContent.LinkUrl, links[0].Url, "link url");
            TestSupport.AssertEqual(ReferenceContent.LinkText, ModelQuery.Text(links[0].Inlines), "link text");
        }

        /// <summary>Bulleted paragraphs at levels 0, 1 and 2 become a three deep nested unordered list.</summary>
        public static async Task NestedBullets(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            ListBlock bullets = doc.Blocks.OfType<ListBlock>().First();
            TestSupport.AssertEqual(ListKindEnum.Unordered, bullets.Kind, "bullet kind");
            TestSupport.AssertEqual(3, bullets.Items.Count, "top level bullet count");
            TestSupport.AssertEqual(ReferenceContent.Bullets[2], ModelQuery.Text(bullets.Items[2]), "third bullet");
            ListBlock? nested = bullets.Items[1].Blocks.OfType<ListBlock>().FirstOrDefault();
            TestSupport.Assert(nested != null, "second bullet holds a nested list");
            TestSupport.AssertEqual(ReferenceContent.NestedBullet, ModelQuery.Text(nested!.Items[0]), "nested bullet");
            ListBlock? deep = nested.Items[0].Blocks.OfType<ListBlock>().FirstOrDefault();
            TestSupport.Assert(deep != null, "nested bullet holds a deeper list");
            TestSupport.AssertEqual(ReferenceContent.DeepBullet, ModelQuery.Text(deep!.Items[0]), "deep bullet");
        }

        /// <summary>Decimal numbering becomes an ordered list starting at 1.</summary>
        public static async Task OrderedList(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            List<ListBlock> lists = doc.Blocks.OfType<ListBlock>().ToList();
            TestSupport.AssertEqual(2, lists.Count, "top level list count");
            ListBlock steps = lists[1];
            TestSupport.AssertEqual(ListKindEnum.Ordered, steps.Kind, "ordered kind");
            TestSupport.AssertEqual(1, steps.Start, "start");
            TestSupport.AssertEqual(3, steps.Items.Count, "step count");
            TestSupport.AssertEqual(ReferenceContent.Steps[1], ModelQuery.Text(steps.Items[1]), "second step");
        }

        /// <summary>The table keeps all cells and its repeating header row.</summary>
        public static async Task Table(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            TableBlock table = doc.Blocks.OfType<TableBlock>().Single();
            TestSupport.AssertEqual(1, table.HeaderRowCount, "header rows");
            TestSupport.AssertEqual(4, table.Rows.Count, "rows");
            TestSupport.Assert(table.Rows[0].Cells.All(c => c.IsHeader), "header cells flagged");
            for (int r = 0; r < ReferenceContent.TableRows.Length; r++)
                for (int c = 0; c < 3; c++)
                    TestSupport.AssertEqual(ReferenceContent.TableRows[r][c], ModelQuery.CellText(table.Rows[r].Cells[c]), "cell " + r + "," + c);
        }

        /// <summary>Consecutive preformatted paragraphs merge into one code block.</summary>
        public static async Task CodeBlock(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            CodeBlock code = doc.Blocks.OfType<CodeBlock>().Single();
            TestSupport.AssertEqual(ReferenceContent.CodeText, code.Text, "code text");
        }

        /// <summary>Quote styled paragraphs become a quote block.</summary>
        public static async Task Quote(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            QuoteBlock quote = doc.Blocks.OfType<QuoteBlock>().Single();
            TestSupport.AssertEqual(ReferenceContent.QuoteText, ModelQuery.Text(quote.Blocks[0]), "quote text");
        }

        /// <summary>The image keeps its bytes, alt text and position between the quote and the international paragraph.</summary>
        public static async Task ImageInPosition(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            int quoteIndex = doc.Blocks.FindIndex(b => b is QuoteBlock);
            TestSupport.Assert(quoteIndex >= 0 && quoteIndex + 2 < doc.Blocks.Count, "quote found");
            ImageBlock? image = doc.Blocks[quoteIndex + 1] as ImageBlock;
            TestSupport.Assert(image != null, "image immediately follows the quote (got " + doc.Blocks[quoteIndex + 1].GetType().Name + ")");
            TestSupport.AssertEqual(ReferenceContent.International, ModelQuery.Text(doc.Blocks[quoteIndex + 2]), "international paragraph follows the image");
            TestSupport.AssertEqual(ReferenceContent.ImageAlt, image!.AltText, "alt text");
            BinaryResource resource = doc.Resources[image.ResourceId];
            TestSupport.AssertEqual("image/png", resource.MediaType, "media type");
            TestSupport.Assert(resource.Data.SequenceEqual(ReferenceContent.ImagePng()), "image bytes identical");
            TestSupport.AssertEqual(ReferenceContent.ImageSize, resource.PixelWidth ?? 0, "pixel width");
            TestSupport.Assert(image.Width.HasValue && Math.Abs(image.Width.Value - 12.0) < 0.01, "display width 16 px = 12 pt");
        }

        /// <summary>Core properties fill the metadata.</summary>
        public static async Task Metadata(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            TestSupport.AssertEqual(ReferenceContent.Title, doc.Metadata.Title, "title");
            TestSupport.AssertEqual(ReferenceContent.Author, doc.Metadata.Author, "author");
            TestSupport.AssertEqual(ReferenceContent.Subject, doc.Metadata.Subject, "subject");
            TestSupport.AssertEqual(new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc), doc.Metadata.CreatedUtc ?? DateTime.MinValue, "created");
        }

        /// <summary>International and special characters survive exactly, and the document ends with the closing paragraph.</summary>
        public static async Task TextFidelity(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(Reference).ConfigureAwait(false);
            List<string> paragraphs = doc.Blocks.OfType<ParagraphBlock>().Select(p => ModelQuery.Text(p.Inlines)).ToList();
            TestSupport.Assert(paragraphs.Contains(ReferenceContent.International), "international paragraph exact");
            TestSupport.Assert(paragraphs.Contains(ReferenceContent.Special), "special paragraph exact");
            TestSupport.AssertEqual(ReferenceContent.Closing, ModelQuery.Text(doc.Blocks[doc.Blocks.Count - 1]), "closing is last");
        }

        /// <summary>gridSpan and vMerge become column and row spans; covered cells are not duplicated.</summary>
        public static async Task Spans(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.SpanTable()).ConfigureAwait(false);
            TableBlock table = doc.Blocks.OfType<TableBlock>().Single();
            TestSupport.AssertEqual(3, table.Rows.Count, "rows");
            TestSupport.AssertEqual(2, table.Rows[0].Cells.Count, "row 0 cells");
            TestSupport.AssertEqual(2, table.Rows[0].Cells[0].ColumnSpan, "A spans two columns");
            TestSupport.AssertEqual(2, table.Rows[0].Cells[1].RowSpan, "B spans two rows");
            TestSupport.AssertEqual(2, table.Rows[1].Cells.Count, "row 1 has no duplicate of B");
            TestSupport.AssertEqual("D", ModelQuery.CellText(table.Rows[1].Cells[1]), "row 1 second cell");
            TestSupport.AssertEqual(3, table.Rows[2].Cells.Count, "row 2 cells");
            TestSupport.AssertEqual(3, table.ColumnCount, "column count");
        }

        /// <summary>Footnotes and endnotes are appended as trailing sections with markers in the body and a warning.</summary>
        public static async Task NotesIncluded(CancellationToken token)
        {
            byte[] docx = DocxEdgeFixtures.WithNotes();
            DocumentModel doc = await ReadAsync(docx).ConfigureAwait(false);
            string body = ModelQuery.Text(doc.Blocks[0]);
            TestSupport.AssertContains(body, "Body text[1] continues[e1].", "note markers in body");
            List<SectionBlock> sections = doc.Blocks.OfType<SectionBlock>().ToList();
            TestSupport.AssertEqual(2, sections.Count, "footnote and endnote sections");
            TestSupport.AssertEqual("Footnotes", sections[0].Title, "footnote section title");
            TestSupport.AssertContains(ModelQuery.Text(sections[0]), "[1] Footnote content here.", "footnote text");
            TestSupport.AssertEqual("Endnotes", sections[1].Title, "endnote section title");
            TestSupport.AssertContains(ModelQuery.Text(sections[1]), "[e1] Endnote content here.", "endnote text");

            using (Converter converter = new Converter())
            {
                BytesConversionResult result = await converter.ConvertToBytesAsync(docx, DocumentFormatEnum.Docx, DocumentFormatEnum.Docx).ConfigureAwait(false);
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost warning raised");
            }
        }

        /// <summary>With IncludeFootnotes false there are no note sections and no markers.</summary>
        public static async Task NotesExcluded(CancellationToken token)
        {
            ConversionOptions options = new ConversionOptions();
            options.Docx.IncludeFootnotes = false;
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.WithNotes(), options).ConfigureAwait(false);
            TestSupport.AssertEqual(0, doc.Blocks.OfType<SectionBlock>().Count(), "no sections");
            TestSupport.AssertEqual("Body text continues.", ModelQuery.Text(doc.Blocks[0]), "no markers");
        }

        /// <summary>Text box content is appended as a "Text boxes" section.</summary>
        public static async Task TextBox(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.WithTextBox()).ConfigureAwait(false);
            SectionBlock section = doc.Blocks.OfType<SectionBlock>().Single();
            TestSupport.AssertEqual("Text boxes", section.Title, "section title");
            TestSupport.AssertContains(ModelQuery.Text(section), "Boxed text", "box text");
            TestSupport.AssertEqual("Before box", ModelQuery.Text(doc.Blocks[0]), "body before box");
            TestSupport.AssertEqual("After box", ModelQuery.Text(doc.Blocks[1]), "body after box, box text not inline");
        }

        /// <summary>Outline levels and localized heading style names are recognized; Title style fills a missing title.</summary>
        public static async Task OutlineHeadings(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.OutlineHeadings()).ConfigureAwait(false);
            List<HeadingBlock> headings = ModelQuery.All<HeadingBlock>(doc.Blocks);
            TestSupport.AssertEqual(3, headings.Count, "heading count");
            TestSupport.AssertEqual(1, headings[0].Level, "title level");
            TestSupport.AssertEqual(2, headings[1].Level, "outline level 1 is heading 2");
            TestSupport.AssertEqual("Outline heading", ModelQuery.Text(headings[1].Inlines), "outline text");
            TestSupport.AssertEqual(3, headings[2].Level, "localized heading 3");
            TestSupport.AssertEqual("Document Title Text", doc.Metadata.Title, "title fallback");
        }

        /// <summary>An image paragraph between two paragraphs stays between them.</summary>
        public static async Task ImageInMiddle(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.ImageInMiddle()).ConfigureAwait(false);
            TestSupport.AssertEqual(3, doc.Blocks.Count, "block count");
            TestSupport.Assert(doc.Blocks[1] is ImageBlock, "image is the middle block");
            TestSupport.AssertEqual("Middle image", ((ImageBlock)doc.Blocks[1]).AltText, "alt");
            TestSupport.AssertEqual(1, doc.Resources.Count, "one resource");
        }

        /// <summary>A HYPERLINK complex field becomes a link and its instruction text is not leaked.</summary>
        public static async Task FieldHyperlink(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.FieldHyperlink()).ConfigureAwait(false);
            ParagraphBlock p = (ParagraphBlock)doc.Blocks[0];
            TestSupport.AssertEqual("See the field link now.", ModelQuery.Text(p.Inlines), "visible text");
            LinkInline link = p.Inlines.OfType<LinkInline>().Single();
            TestSupport.AssertEqual("https://example.org/field", link.Url, "field url");
        }

        /// <summary>A page break run splits the paragraph around a page break block.</summary>
        public static async Task PageBreak(CancellationToken token)
        {
            DocumentModel doc = await ReadAsync(DocxEdgeFixtures.PageBreakInParagraph()).ConfigureAwait(false);
            TestSupport.AssertEqual(3, doc.Blocks.Count, "block count");
            TestSupport.AssertEqual("Page one", ModelQuery.Text(doc.Blocks[0]), "first");
            TestSupport.Assert(doc.Blocks[1] is PageBreakBlock, "page break");
            TestSupport.AssertEqual("Page two", ModelQuery.Text(doc.Blocks[2]), "second");
        }

        /// <summary>DocumentAtom's hand assembled sample DOCX reads without error.</summary>
        public static async Task RealWorldSample(CancellationToken token)
        {
            byte[] docx = DocxFixtureFiles.Load("RealWorld/DocumentAtom/sample.docx");
            DocumentModel doc = await ReadAsync(docx).ConfigureAwait(false);
            TestSupport.Assert(doc.Blocks.Count > 0, "sample has blocks");
            string text = string.Join(" ", ModelQuery.All<ParagraphBlock>(doc.Blocks).Select(p => ModelQuery.Text(p.Inlines)));
            TestSupport.Assert(text.Trim().Length > 0, "sample has text");
        }

        /// <summary>Auto detection recognizes the reference DOCX.</summary>
        public static async Task Detection(CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                DetectionResult detection = await converter.DetectFormatAsync(Reference).ConfigureAwait(false);
                TestSupport.AssertEqual(DocumentFormatEnum.Docx, detection.Format ?? DocumentFormatEnum.Auto, "detected");
                DocumentModel doc = await converter.ReadAsync(Reference, DocumentFormatEnum.Auto).ConfigureAwait(false);
                TestSupport.Assert(doc.Blocks.Count > 5, "auto read");
            }
        }
    }
}
