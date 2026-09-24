namespace Test.Shared.Suites.Docx
{
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
    using Test.Shared.Inspection;

    /// <summary>
    /// DOCX writer cases. Output is checked with DocxInspector (OpenXml SDK and schema validation), never only with
    /// DocConverter's own reader.
    /// </summary>
    public static class DocxWriterCases
    {
        /// <summary>
        /// Write a model to DOCX.
        /// </summary>
        /// <param name="document">Model.</param>
        /// <param name="options">Options, or null.</param>
        /// <returns>Result with bytes.</returns>
        public static async Task<BytesConversionResult> WriteAsync(DocumentModel document, ConversionOptions? options = null)
        {
            using (Converter converter = new Converter())
            {
                return await converter.WriteToBytesAsync(document, DocumentFormatEnum.Docx, options).ConfigureAwait(false);
            }
        }

        /// <summary>The reference model writes a valid DOCX holding all reference content.</summary>
        public static async Task ReferenceValidates(CancellationToken token)
        {
            BytesConversionResult result = await WriteAsync(ReferenceContent.ToModel()).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            foreach (string snippet in ReferenceContent.CoreTextSnippets)
                TestSupport.Assert(snap.ContainsText(snippet), "missing snippet '" + snippet + "'");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.International), "international text exact");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.Special), "special text exact");
            TestSupport.Assert(snap.Headings.Contains(ReferenceContent.Heading1), "heading 1");
            TestSupport.Assert(snap.Headings.Contains(ReferenceContent.HeadingCode), "heading 3");
            TestSupport.Assert(snap.ListItems.Contains(ReferenceContent.DeepBullet), "deep bullet numbered");
            TestSupport.Assert(snap.ListItems.Contains(ReferenceContent.Steps[2]), "step numbered");
            TestSupport.Assert(snap.HasTableRow(ReferenceContent.TableRows[2]), "table row");
            TestSupport.AssertEqual(1, snap.ImageCount, "image count");
            TestSupport.Assert(snap.LinkUrls.Contains(ReferenceContent.LinkUrl), "link url");
            TestSupport.AssertEqual(ReferenceContent.Title, snap.Title, "title");
            TestSupport.AssertEqual(0, result.Warnings.Count, "no warnings");
        }

        /// <summary>Styles, numbering and table header markup are present in the written parts.</summary>
        public static async Task Markup(CancellationToken token)
        {
            BytesConversionResult result = await WriteAsync(ReferenceContent.ToModel()).ConfigureAwait(false);
            string xml = DocxInspector.PartXml(result.Output, "word/document.xml");
            TestSupport.AssertContains(xml, "w:val=\"Heading1\"", "heading style");
            TestSupport.AssertContains(xml, "<w:b />", "bold run");
            TestSupport.AssertContains(xml, "<w:strike />", "strike run");
            TestSupport.AssertContains(xml, "w:val=\"InlineCode\"", "inline code style");
            TestSupport.AssertContains(xml, "<w:tblHeader />", "repeating header row");
            TestSupport.AssertContains(xml, "w:val=\"Code\"", "code paragraph style");
            TestSupport.AssertContains(xml, "w:val=\"Quote\"", "quote style");
            TestSupport.AssertContains(xml, "w:ilvl w:val=\"2\"", "third list level");
            string numbering = DocxInspector.PartXml(result.Output, "word/numbering.xml");
            TestSupport.AssertContains(numbering, "w:val=\"decimal\"", "decimal numbering");
            TestSupport.AssertContains(numbering, "w:val=\"bullet\"", "bullet numbering");
        }

        /// <summary>Default page setup is Letter with one inch margins.</summary>
        public static async Task DefaultPage(CancellationToken token)
        {
            BytesConversionResult result = await WriteAsync(ReferenceContent.ToModel()).ConfigureAwait(false);
            string xml = DocxInspector.PartXml(result.Output, "word/document.xml");
            TestSupport.AssertContains(xml, "w:w=\"12240\" w:h=\"15840\"", "letter page");
            TestSupport.AssertContains(xml, "w:top=\"1440\"", "1 inch margin");
        }

        /// <summary>A4 with half inch margins is reflected in the section properties.</summary>
        public static async Task PageOptions(CancellationToken token)
        {
            ConversionOptions options = new ConversionOptions();
            options.Docx.PageSize = PdfPageSizeEnum.A4;
            options.Docx.MarginPoints = 36;
            BytesConversionResult result = await WriteAsync(ReferenceContent.ToModel(), options).ConfigureAwait(false);
            DocxInspector.Inspect(result.Output);
            string xml = DocxInspector.PartXml(result.Output, "word/document.xml");
            TestSupport.AssertContains(xml, "w:w=\"11906\" w:h=\"16838\"", "A4 page");
            TestSupport.AssertContains(xml, "w:top=\"720\"", "half inch margin");
            TestSupport.AssertContains(xml, "w:left=\"720\"", "half inch left margin");
        }

        /// <summary>Deterministic output is byte identical across runs and still valid.</summary>
        public static async Task Deterministic(CancellationToken token)
        {
            ConversionOptions options = new ConversionOptions();
            options.Deterministic = true;
            BytesConversionResult first = await WriteAsync(ReferenceContent.ToModel(), options).ConfigureAwait(false);
            await Task.Delay(1100, token).ConfigureAwait(false);
            BytesConversionResult second = await WriteAsync(ReferenceContent.ToModel(), options).ConfigureAwait(false);
            TestSupport.Assert(first.Output.SequenceEqual(second.Output), "deterministic bytes identical (" + first.Output.Length + " vs " + second.Output.Length + ")");
            DocxInspector.Inspect(first.Output);
            string core = DocxInspector.PartXml(first.Output, "docProps/core.xml");
            TestSupport.AssertContains(core, "2000-01-01T00:00:00Z", "pinned timestamp");
        }

        /// <summary>A javascript: link is dropped, its text kept, and LinkRemovedUnsafe raised.</summary>
        public static async Task UnsafeLink(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            ParagraphBlock p = new ParagraphBlock("Click ");
            p.Inlines.Add(new LinkInline("javascript:alert(1)", "here"));
            p.Inlines.Add(new LinkInline("mailto:someone@example.com", "mail"));
            p.Inlines.Add(new LinkInline("#section-2", "anchor"));
            doc.Blocks.Add(p);
            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.Assert(snap.ContainsText("Click heremailanchor"), "all text kept");
            TestSupport.Assert(!snap.LinkUrls.Any(u => u.StartsWith("javascript", System.StringComparison.OrdinalIgnoreCase)), "no javascript link");
            TestSupport.Assert(snap.LinkUrls.Contains("mailto:someone@example.com"), "mailto kept");
            TestSupport.Assert(snap.LinkUrls.Contains("#section-2"), "anchor kept");
            TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.LinkRemovedUnsafe), "warning");
            TestSupport.AssertNotContains(DocxInspector.PartXml(result.Output, "word/_rels/document.xml.rels"), "javascript", "no javascript relationship");
        }

        /// <summary>Column and row spans write as gridSpan and vMerge, validate, and read back as spans.</summary>
        public static async Task Spans(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            TableBlock table = new TableBlock();
            table.HeaderRowCount = 1;
            TableRow r0 = new TableRow(new string?[] { "A", "B" });
            r0.Cells[0].ColumnSpan = 2;
            r0.Cells[1].RowSpan = 2;
            table.Rows.Add(r0);
            table.Rows.Add(new TableRow(new string?[] { "C", "D" }));
            table.Rows.Add(new TableRow(new string?[] { "E", "F", "G" }));
            table.Rows.Add(new TableRow(new string?[] { "ragged" }));
            doc.Blocks.Add(table);
            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.AssertEqual(4, snap.TableRows.Count, "row count");
            TestSupport.AssertEqual(3, snap.TableRows[3].Count, "ragged row padded");
            string xml = DocxInspector.PartXml(result.Output, "word/document.xml");
            TestSupport.AssertContains(xml, "<w:gridSpan w:val=\"2\" />", "grid span");
            TestSupport.AssertContains(xml, "<w:vMerge w:val=\"restart\" />", "vmerge restart");
            TestSupport.AssertContains(xml, "<w:vMerge />", "vmerge continue");

            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);
            TableBlock t = back.Blocks.OfType<TableBlock>().Single();
            TestSupport.AssertEqual(2, t.Rows[0].Cells[0].ColumnSpan, "colspan round trip");
            TestSupport.AssertEqual(2, t.Rows[0].Cells[1].RowSpan, "rowspan round trip");
            TestSupport.AssertEqual(2, t.Rows[1].Cells.Count, "covered cell not duplicated");
            TestSupport.AssertEqual(1, t.HeaderRowCount, "header row round trip");
        }

        /// <summary>Every sample image format embeds and validates: PNG variants, JPEG variants, GIF, BMP, TIFF and WebP.</summary>
        public static async Task AllImageFormats(CancellationToken token)
        {
            string[] names = new string[] { "sample.png", "sample-alpha.png", "sample-palette.png", "sample.jpg", "sample-progressive.jpg", "sample-cmyk.jpg", "sample.gif", "sample.bmp", "sample.tiff", "sample.webp", "sample-lossless.webp" };
            DocumentModel doc = new DocumentModel();
            foreach (string name in names)
            {
                string id = doc.AddResource(new BinaryResource { Data = DocxFixtureFiles.Load("Images/" + name), MediaType = "application/octet-stream", FileName = name });
                doc.Blocks.Add(new ImageBlock(id, name));
            }

            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.AssertEqual(names.Length, snap.ImageCount, "image count");
            TestSupport.AssertEqual(0, result.Warnings.Count, "no warnings");
            string types = DocxInspector.PartXml(result.Output, "[Content_Types].xml");
            TestSupport.AssertContains(types, "image/webp", "webp content type");
            TestSupport.AssertContains(types, "image/tiff", "tiff content type");

            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);
            TestSupport.AssertEqual(names.Length, back.Resources.Count, "resources read back");
            TestSupport.AssertEqual(96, back.Resources.Values.First().PixelWidth ?? 0, "pixel size read back");
        }

        /// <summary>A missing resource becomes a placeholder and raises ImagesOmitted; unknown bytes raise ImageFormatUnsupported.</summary>
        public static async Task MissingAndUnknownImages(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            doc.Blocks.Add(new ImageBlock("nope", "ghost"));
            string id = doc.AddResource(new BinaryResource { Data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, MediaType = "application/x-unknown" });
            doc.Blocks.Add(new ImageBlock(id, "mystery"));
            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.Assert(snap.ContainsText("[Image: ghost]"), "missing placeholder");
            TestSupport.Assert(snap.ContainsText("[Image: mystery]"), "unknown placeholder");
            TestSupport.AssertEqual(0, snap.ImageCount, "no images embedded");
            TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "ImagesOmitted");
            TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImageFormatUnsupported), "ImageFormatUnsupported");
        }

        /// <summary>Lists nested twelve deep are written at Word's ninth level, validate, and warn.</summary>
        public static async Task DeepList(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            ListBlock root = new ListBlock(ListKindEnum.Unordered);
            ListBlock current = root;
            for (int i = 0; i < 12; i++)
            {
                ListItemBlock item = new ListItemBlock("level " + i);
                current.Items.Add(item);
                ListBlock next = new ListBlock(i % 2 == 0 ? ListKindEnum.Ordered : ListKindEnum.Unordered);
                if (i < 11) item.Blocks.Add(next);
                current = next;
            }

            doc.Blocks.Add(root);
            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.AssertEqual(12, snap.ListItems.Count, "all items numbered");
            TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.NestedDepthLimited), "depth warning");
        }

        /// <summary>An ordered list starting at 5 and a task list round trip.</summary>
        public static async Task ListKindsRoundTrip(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            ListBlock ordered = new ListBlock(ListKindEnum.Ordered);
            ordered.Start = 5;
            ordered.Items.Add(new ListItemBlock("five"));
            ordered.Items.Add(new ListItemBlock("six"));
            doc.Blocks.Add(ordered);
            doc.Blocks.Add(new ParagraphBlock("between"));
            ListBlock tasks = new ListBlock(ListKindEnum.Task);
            ListItemBlock done = new ListItemBlock("done task");
            done.Checked = true;
            ListItemBlock open = new ListItemBlock("open task");
            open.Checked = false;
            tasks.Items.Add(done);
            tasks.Items.Add(open);
            doc.Blocks.Add(tasks);

            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            DocxInspector.Inspect(result.Output);
            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);
            List<ListBlock> lists = back.Blocks.OfType<ListBlock>().ToList();
            TestSupport.AssertEqual(2, lists.Count, "list count");
            TestSupport.AssertEqual(ListKindEnum.Ordered, lists[0].Kind, "ordered");
            TestSupport.AssertEqual(5, lists[0].Start, "start 5");
            TestSupport.AssertEqual(ListKindEnum.Task, lists[1].Kind, "task kind");
            TestSupport.AssertEqual(true, lists[1].Items[0].Checked ?? false, "checked");
            TestSupport.AssertEqual(false, lists[1].Items[1].Checked ?? true, "unchecked");
            TestSupport.AssertEqual("open task", ModelQuery.Text(lists[1].Items[1]), "prefix stripped");
        }

        /// <summary>The reference model survives model to DOCX to model with structure intact.</summary>
        public static async Task RoundTrip(CancellationToken token)
        {
            DocumentModel original = ReferenceContent.ToModel();
            BytesConversionResult result = await WriteAsync(original).ConfigureAwait(false);
            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);

            List<string> expectedHeadings = ModelQuery.All<HeadingBlock>(original.Blocks).Select(h => h.Level + ":" + ModelQuery.Text(h.Inlines)).ToList();
            List<string> actualHeadings = ModelQuery.All<HeadingBlock>(back.Blocks).Select(h => h.Level + ":" + ModelQuery.Text(h.Inlines)).ToList();
            TestSupport.AssertEqual(string.Join("|", expectedHeadings), string.Join("|", actualHeadings), "headings");

            List<string> kinds = back.Blocks.Select(b => b.GetType().Name).ToList();
            List<string> expectedKinds = original.Blocks.Select(b => b.GetType().Name).ToList();
            TestSupport.AssertEqual(string.Join(",", expectedKinds), string.Join(",", kinds), "block sequence");

            TestSupport.AssertEqual(ReferenceContent.CodeText, back.Blocks.OfType<CodeBlock>().Single().Text, "code");
            ListBlock bullets = back.Blocks.OfType<ListBlock>().First();
            TestSupport.AssertEqual(ReferenceContent.DeepBullet, ModelQuery.Text(bullets.Items[1].Blocks.OfType<ListBlock>().Single().Items[0].Blocks.OfType<ListBlock>().Single().Items[0]), "deep bullet nesting");
            TableBlock table = back.Blocks.OfType<TableBlock>().Single();
            TestSupport.AssertEqual(1, table.HeaderRowCount, "header");
            TestSupport.AssertEqual("Admiral", ModelQuery.CellText(table.Rows[2].Cells[1]), "cell");
            TestSupport.Assert(ModelQuery.StyledTexts(back, InlineStyleEnum.Bold).Contains(ReferenceContent.BoldText), "bold");
            TestSupport.Assert(ModelQuery.StyledTexts(back, InlineStyleEnum.Underline).Contains(ReferenceContent.UnderlineText), "underline");
            TestSupport.Assert(ModelQuery.StyledTexts(back, InlineStyleEnum.Code).Contains(ReferenceContent.InlineCode), "inline code");
            TestSupport.AssertEqual(ReferenceContent.LinkUrl, ModelQuery.Links(back).Single().Url, "link");
            TestSupport.Assert(back.Resources.Values.Single().Data.SequenceEqual(ReferenceContent.ImagePng()), "image bytes");
            TestSupport.AssertEqual(ReferenceContent.ImageAlt, back.Blocks.OfType<ImageBlock>().Single().AltText, "alt");
            TestSupport.AssertEqual(ReferenceContent.Title, back.Metadata.Title, "title");
            TestSupport.AssertEqual(ReferenceContent.Author, back.Metadata.Author, "author");
        }

        /// <summary>Sections, breaks, alignment, captions and superscript write validly and read back.</summary>
        public static async Task SectionsAndBreaks(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            SectionBlock page1 = new SectionBlock(SectionKindEnum.Page, null);
            page1.Blocks.Add(new ParagraphBlock("first page"));
            SectionBlock page2 = new SectionBlock(SectionKindEnum.Page, null);
            page2.Blocks.Add(new ParagraphBlock("second page"));
            SectionBlock sheet = new SectionBlock(SectionKindEnum.Sheet, "Sheet One");
            sheet.Blocks.Add(new ParagraphBlock("sheet body"));
            doc.Blocks.Add(page1);
            doc.Blocks.Add(page2);
            doc.Blocks.Add(sheet);
            doc.Blocks.Add(new ThematicBreakBlock());
            ParagraphBlock centered = new ParagraphBlock("centered");
            centered.Alignment = TextAlignmentEnum.Center;
            doc.Blocks.Add(centered);
            ParagraphBlock sup = new ParagraphBlock("E = mc");
            sup.Inlines.Add(new TextInline("2", InlineStyleEnum.Superscript));
            sup.Inlines.Add(new LineBreakInline());
            sup.Inlines.Add(new TextInline("tab\there"));
            doc.Blocks.Add(sup);
            doc.Blocks.Add(new PageBreakBlock());
            doc.Blocks.Add(new ParagraphBlock("after break"));

            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            DocxInspector.Inspect(result.Output);
            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);
            TestSupport.AssertEqual(2, back.Blocks.OfType<PageBreakBlock>().Count(), "page breaks between pages and explicit");
            TestSupport.Assert(ModelQuery.All<HeadingBlock>(back.Blocks).Any(h => ModelQuery.Text(h.Inlines) == "Sheet One"), "section title heading");
            ParagraphBlock c = back.Blocks.OfType<ParagraphBlock>().Single(p => ModelQuery.Text(p.Inlines) == "centered");
            TestSupport.AssertEqual(TextAlignmentEnum.Center, c.Alignment, "alignment");
            TestSupport.Assert(ModelQuery.StyledTexts(back, InlineStyleEnum.Superscript).Contains("2"), "superscript");
            TestSupport.Assert(back.Blocks.OfType<ParagraphBlock>().Any(p => ModelQuery.Text(p.Inlines) == "E = mc2\ntab\there"), "line break and tab");
        }

        /// <summary>An empty document writes a valid DOCX that reads back empty.</summary>
        public static async Task EmptyDocument(CancellationToken token)
        {
            BytesConversionResult result = await WriteAsync(new DocumentModel()).ConfigureAwait(false);
            DocxInspector.Inspect(result.Output);
            DocumentModel back = await DocxReaderCases.ReadAsync(result.Output).ConfigureAwait(false);
            TestSupport.AssertEqual(0, back.Blocks.Count, "no blocks");
        }

        /// <summary>Control characters and markup-like text are written safely.</summary>
        public static async Task HostileText(CancellationToken token)
        {
            DocumentModel doc = new DocumentModel();
            doc.Metadata.Title = "Title with <angle> & \u0001control";
            doc.Blocks.Add(new ParagraphBlock("bell\u0007 null\u0000 <w:p> ]]> & done"));
            BytesConversionResult result = await WriteAsync(doc).ConfigureAwait(false);
            ContentSnapshot snap = DocxInspector.Inspect(result.Output);
            TestSupport.Assert(snap.ContainsText("bell null <w:p> ]]> & done"), "text kept without control characters");
            TestSupport.AssertEqual("Title with <angle> & control", snap.Title, "title escaped");
        }

        /// <summary>DOCX to DOCX through the converter with Auto source detection produces valid output.</summary>
        public static async Task ConvertAuto(CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                BytesConversionResult result = await converter.ConvertToBytesAsync(DocxReaderCases.Reference, DocumentFormatEnum.Auto, DocumentFormatEnum.Docx).ConfigureAwait(false);
                TestSupport.AssertEqual(DocumentFormatEnum.Docx, result.SourceFormat, "source");
                ContentSnapshot snap = DocxInspector.Inspect(result.Output);
                foreach (string snippet in ReferenceContent.CoreTextSnippets)
                    TestSupport.Assert(snap.ContainsText(snippet), "missing snippet '" + snippet + "'");
                TestSupport.AssertEqual(result.Output.Length, (int)result.BytesWritten, "bytes written");
            }
        }
    }
}
