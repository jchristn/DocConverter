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
    using DocConverter.Options;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Touchstone.Core;

    /// <summary>
    /// Readers for Text, Markdown, HTML, JSON, XML, CSV and TSV, checked against the hand-written reference fixtures and
    /// targeted edge cases.
    /// </summary>
    public static class TextReadersSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("TextReaders", "Text family readers");
            Converter c = new Converter();

            s.Add("Markdown_Reference", "Markdown reference: every element, style, link, image and front matter", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Markdown), DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                AssertRichReference(m, true);
                TestSupport.AssertEqual(ReferenceContent.Title, m.Metadata.Title, "front matter title");
                TestSupport.AssertEqual(ReferenceContent.Author, m.Metadata.Author, "front matter author");
                TableBlock table = ModelInspector.AllBlocks(m.Blocks).OfType<TableBlock>().Single();
                TestSupport.AssertEqual(TextAlignmentEnum.Right, table.ColumnAlignments[2], "third column right aligned");
            });

            s.Add("Html_Reference", "HTML reference: every element, style, link, image and head metadata; script and style ignored", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Html), DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                AssertRichReference(m, true);
                TestSupport.AssertEqual(ReferenceContent.Title, m.Metadata.Title, "title element");
                TestSupport.AssertEqual(ReferenceContent.Author, m.Metadata.Author, "meta author");
                TestSupport.AssertEqual("en", m.Metadata.Language, "lang attribute");
                ContentSnapshot snap = ModelInspector.Inspect(m);
                TestSupport.Assert(!snap.ContainsText("script content"), "script text excluded");
                TestSupport.Assert(!snap.ContainsText("font-family"), "style text excluded");
            });

            s.Add("Text_Reference", "Plain text: paragraphs split on blank lines, line breaks kept, every word present", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Text), DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(m);
                foreach (string snippet in ReferenceContent.CoreTextSnippets) TestSupport.Assert(snap.ContainsText(snippet), "text contains '" + snippet + "'");
                TestSupport.Assert(m.Blocks.All(b => b is ParagraphBlock), "only paragraphs");
                ParagraphBlock list = (ParagraphBlock)m.Blocks.First(b => ModelInspector.Text(((ParagraphBlock)b).Inlines).StartsWith("- First", StringComparison.Ordinal));
                TestSupport.Assert(list.Inlines.OfType<LineBreakInline>().Count() == 4, "five lines joined by four line breaks");
            });

            foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
            {
                DocumentFormatEnum format = f;
                s.Add(format + "_Reference", format + " reference: one table, header row, exact cells", async ct =>
                {
                    DocumentModel m = await c.ReadAsync(TextFixtures.Reference(format), format, null, ct).ConfigureAwait(false);
                    TableBlock table = (TableBlock)m.Blocks.Single();
                    TestSupport.AssertEqual(1, table.HeaderRowCount, "header rows");
                    TestSupport.Assert(table.Rows[0].Cells.All(x => x.IsHeader), "header cells flagged");
                    ContentSnapshot snap = ModelInspector.Inspect(m);
                    foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "row " + string.Join("|", row));
                });
            }

            s.Add("Csv_Quoting", "RFC 4180: quoted delimiters, doubled quotes and embedded newlines", async ct =>
            {
                string csv = "a,b,c\n\"x,1\",\"say \"\"hi\"\"\",\"line1\nline2\"\n";
                DocumentModel m = await c.ReadAsync(csv, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TableBlock t = (TableBlock)m.Blocks.Single();
                TestSupport.AssertEqual(2, t.Rows.Count, "rows");
                TestSupport.AssertEqual("x,1", CellText(t, 1, 0), "quoted comma");
                TestSupport.AssertEqual("say \"hi\"", CellText(t, 1, 1), "doubled quotes");
                TestSupport.AssertEqual("line1\nline2", CellText(t, 1, 2), "embedded newline");
            });

            s.Add("Csv_Ragged", "Ragged rows are padded to the widest row", async ct =>
            {
                DocumentModel m = await c.ReadAsync("a,b,c\n1\n1,2,3,4\n", DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TableBlock t = (TableBlock)m.Blocks.Single();
                TestSupport.Assert(t.Rows.All(r => r.Cells.Count == 4), "every row padded to 4 cells");
            });

            s.Add("Csv_Options", "HasHeaderRow false and a custom delimiter are honored", async ct =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Csv.HasHeaderRow = false;
                o.Csv.Delimiter = ';';
                DocumentModel m = await c.ReadAsync("a;b\n1;2\n", DocumentFormatEnum.Csv, o, ct).ConfigureAwait(false);
                TableBlock t = (TableBlock)m.Blocks.Single();
                TestSupport.AssertEqual(0, t.HeaderRowCount, "no header row");
                TestSupport.AssertEqual(2, t.Rows[0].Cells.Count, "semicolon split");
            });

            s.Add("Csv_EmptyAndBom", "Empty CSV gives an empty document; a UTF-8 BOM is not part of the first cell", async ct =>
            {
                DocumentModel empty = await c.ReadAsync(new byte[0], DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(0, empty.Blocks.Count, "empty");
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("Name,Role\nA,B\n")).ToArray();
                DocumentModel m = await c.ReadAsync(bom, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("Name", CellText((TableBlock)m.Blocks[0], 0, 0), "first cell without BOM");
            });

            s.Add("Json_Generic", "Arbitrary JSON maps scalars to a key/value table, arrays of objects to tables, arrays of scalars to lists", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Json), DocumentFormatEnum.Json, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(m);
                TestSupport.Assert(snap.HasTableRow(new[] { "title", ReferenceContent.Title }), "key/value row");
                TestSupport.Assert(snap.HasTableRow(new[] { "Name", "Role", "Years" }), "array of objects header");
                TestSupport.Assert(snap.HasTableRow(new[] { "Grace Hopper", "Admiral", "40" }), "array of objects row with number");
                TestSupport.Assert(snap.ListItems.Contains("Step two"), "array of scalars as list");
                TestSupport.Assert(snap.ContainsText(ReferenceContent.International), "unicode kept");
                TestSupport.Assert(ModelInspector.AllBlocks(m.Blocks).OfType<SectionBlock>().Any(x => x.Title == "notes"), "nested object is a titled section");
            });

            s.Add("Json_Canonical", "Canonical JSON reads back to exactly the reference model", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.CanonicalJson(), DocumentFormatEnum.Json, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(ReferenceContent.ToModel(), m, "canonical JSON");
            });

            s.Add("Json_DepthLimit", "JSON deeper than MaxNestingDepth is written as text with NestedDepthLimited", async ct =>
            {
                Converter shallow = new Converter(new ConverterSettings { MaxNestingDepth = 5 });
                StringConversionResult r = await shallow.ConvertToStringAsync(NegativeFixtures.DeepJson(20), DocumentFormatEnum.Json, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.NestedDepthLimited), "NestedDepthLimited raised");
                TestSupport.AssertContains(r.Output, "\"a\"", "deep part kept as JSON text");
            });

            s.Add("Json_TooDeep", "Pathologically deep JSON (10,000 levels) is rejected with DocumentReadException, not a stack overflow", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync(NegativeFixtures.DeepJson(10000), DocumentFormatEnum.Json, null, ct), "deep json").ConfigureAwait(false);
            });

            s.Add("Json_Malformed", "Malformed JSON throws DocumentReadException with the parser position", async ct =>
            {
                DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync("{\"a\": [1, 2,, }", DocumentFormatEnum.Json, null, ct), "malformed json").ConfigureAwait(false);
                TestSupport.AssertContains(ex.Message, "JSON", "message names JSON");
            });

            s.Add("Json_Scalars", "Top level scalars and empty containers read without error", async ct =>
            {
                foreach (string json in new[] { "42", "\"text\"", "true", "null", "[]", "{}", "[1, \"two\", 3.5]" })
                {
                    DocumentModel m = await c.ReadAsync(json, DocumentFormatEnum.Json, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(m != null, "read " + json);
                }
            });

            s.Add("Xml_Generic", "Arbitrary XML maps records to tables, leaves to key/value rows, repeated leaves to lists", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Xml), DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(m);
                TestSupport.Assert(snap.HasTableRow(new[] { "@title", ReferenceContent.Title }), "attribute row");
                TestSupport.Assert(snap.HasTableRow(new[] { "heading", ReferenceContent.Heading1 }), "leaf row");
                TestSupport.Assert(snap.HasTableRow(new[] { "Grace Hopper", "Admiral", "40" }), "record table row");
                TestSupport.Assert(snap.ListItems.Contains("Step three"), "repeated leaves as list");
                TestSupport.Assert(snap.ContainsText(ReferenceContent.Special), "entities decoded");
            });

            s.Add("Xml_NoAttributes", "XmlOptions.IncludeAttributes false leaves attributes out", async ct =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Xml.IncludeAttributes = false;
                DocumentModel m = await c.ReadAsync(TextFixtures.Reference(DocumentFormatEnum.Xml), DocumentFormatEnum.Xml, o, ct).ConfigureAwait(false);
                TestSupport.Assert(!ModelInspector.Inspect(m).ContainsText(ReferenceContent.Title), "attribute value absent");
            });

            s.Add("Xml_Canonical", "Canonical XML reads back to exactly the reference model", async ct =>
            {
                DocumentModel m = await c.ReadAsync(TextFixtures.CanonicalXml(), DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(ReferenceContent.ToModel(), m, "canonical XML");
            });

            s.Add("Xml_Malformed", "Malformed XML throws DocumentReadException", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync("<a><b></a>", DocumentFormatEnum.Xml, null, ct), "malformed xml").ConfigureAwait(false);
            });

            s.Add("Markdown_TaskListsAndStart", "Task lists keep checked state; ordered lists keep their start number", async ct =>
            {
                DocumentModel m = await c.ReadAsync("- [x] done\n- [ ] open\n\n5. five\n6. six\n", DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                ListBlock tasks = (ListBlock)m.Blocks[0];
                TestSupport.AssertEqual(ListKindEnum.Task, tasks.Kind, "task list kind");
                TestSupport.AssertEqual<bool?>(true, tasks.Items[0].Checked, "first checked");
                TestSupport.AssertEqual<bool?>(false, tasks.Items[1].Checked, "second unchecked");
                TestSupport.AssertEqual("done", ModelInspector.Text(((ParagraphBlock)tasks.Items[0].Blocks[0]).Inlines), "task text without marker");
                ListBlock ordered = (ListBlock)m.Blocks[1];
                TestSupport.AssertEqual(5, ordered.Start, "start number");
            });

            s.Add("Markdown_RemoteImageNotFetched", "A remote image URL becomes a link and is never fetched", async ct =>
            {
                DocumentModel m = await c.ReadAsync("![logo](https://127.0.0.1:1/never.png)", DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(0, m.Resources.Count, "no resource");
                ContentSnapshot snap = ModelInspector.Inspect(m);
                TestSupport.Assert(snap.LinkUrls.Contains("https://127.0.0.1:1/never.png"), "kept as link");
            });

            s.Add("Markdown_FencedCodeWithBlankLines", "Fenced code containing blank lines stays one code block (DocumentAtom split it)", async ct =>
            {
                DocumentModel m = await c.ReadAsync("```\nline 1\n\nline 3\n```\n", DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                CodeBlock code = (CodeBlock)m.Blocks.Single();
                TestSupport.AssertEqual("line 1\n\nline 3", code.Text.TrimEnd('\n'), "code text");
            });

            s.Add("Html_SpansAndHeaders", "HTML colspan, rowspan, th and caption are read", async ct =>
            {
                string html = "<table><caption>Cap</caption><tr><th colspan=\"2\">H</th></tr><tr><td rowspan=\"2\">A</td><td>B</td></tr><tr><td>C</td></tr></table>";
                DocumentModel m = await c.ReadAsync(html, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TableBlock t = (TableBlock)m.Blocks.Single();
                TestSupport.AssertEqual("Cap", t.Caption, "caption");
                TestSupport.AssertEqual(1, t.HeaderRowCount, "header rows");
                TestSupport.AssertEqual(2, t.Rows[0].Cells[0].ColumnSpan, "colspan");
                TestSupport.AssertEqual(2, t.Rows[1].Cells[0].RowSpan, "rowspan");
                TestSupport.AssertEqual(2, t.ColumnCount, "column count");
            });

            s.Add("Html_InlineNesting", "Nested inline markup combines styles", async ct =>
            {
                DocumentModel m = await c.ReadAsync("<p><strong>bold <em>both</em></strong> <sup>up</sup><sub>down</sub> <kbd>key</kbd></p>", DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                List<TextInline> runs = ((ParagraphBlock)m.Blocks.Single()).Inlines.OfType<TextInline>().ToList();
                TestSupport.Assert(runs.Any(r => r.Text == "both" && r.Style == (InlineStyleEnum.Bold | InlineStyleEnum.Italic)), "bold and italic combined");
                TestSupport.Assert(runs.Any(r => r.Text == "up" && r.Style == InlineStyleEnum.Superscript), "superscript");
                TestSupport.Assert(runs.Any(r => r.Text == "down" && r.Style == InlineStyleEnum.Subscript), "subscript");
                TestSupport.Assert(runs.Any(r => r.Text == "key" && r.Style == InlineStyleEnum.Code), "kbd as code");
            });

            s.Add("Html_BareText", "HTML without body, and loose text between blocks, becomes paragraphs", async ct =>
            {
                DocumentModel m = await c.ReadAsync("Hello <b>there</b><div>block</div>after", DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = ModelInspector.Inspect(m);
                TestSupport.Assert(snap.ContainsText("Hello there"), "leading loose text");
                TestSupport.Assert(snap.ContainsText("block"), "div text");
                TestSupport.Assert(snap.ContainsText("after"), "trailing loose text");
                TestSupport.Assert(m.Blocks.Count == 3, "three paragraphs, got " + m.Blocks.Count);
            });

            s.Add("Html_TaskList", "Checkbox list items become a task list", async ct =>
            {
                DocumentModel m = await c.ReadAsync("<ul><li><input type=\"checkbox\" checked> a</li><li><input type=\"checkbox\"> b</li></ul>", DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                ListBlock l = (ListBlock)m.Blocks.Single();
                TestSupport.AssertEqual(ListKindEnum.Task, l.Kind, "task kind");
                TestSupport.AssertEqual<bool?>(true, l.Items[0].Checked, "checked");
            });

            s.Add("Text_Empty", "Empty and whitespace-only text read as an empty document", async ct =>
            {
                foreach (string t in new[] { "", "   ", "\n\n\r\n" })
                {
                    DocumentModel m = await c.ReadAsync(t, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, m.Blocks.Count, "no blocks for '" + t.Replace("\n", "\\n") + "'");
                }
            });

            return s.Build();
        }

        /// <summary>
        /// Assert a model read from a rich reference fixture holds every reference element.
        /// </summary>
        /// <param name="m">Model.</param>
        /// <param name="expectUnderline">True when the source can express underline.</param>
        /// <param name="expectCodeLanguage">True when the source keeps the code block language (DOCX does not).</param>
        public static void AssertRichReference(DocumentModel m, bool expectUnderline, bool expectCodeLanguage = true)
        {
            ContentSnapshot snap = ModelInspector.Inspect(m);
            foreach (string snippet in ReferenceContent.CoreTextSnippets) TestSupport.Assert(snap.ContainsText(snippet), "text contains '" + snippet + "'");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.International), "international text");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.Special), "special characters paragraph");
            foreach (string h in new[] { ReferenceContent.Heading1, ReferenceContent.HeadingLists, ReferenceContent.HeadingTable, ReferenceContent.HeadingCode })
                TestSupport.Assert(snap.Headings.Contains(h), "heading '" + h + "' in [" + string.Join(", ", snap.Headings) + "]");
            List<HeadingBlock> headings = ModelInspector.AllBlocks(m.Blocks).OfType<HeadingBlock>().ToList();
            TestSupport.AssertEqual(1, headings.First(h => ModelInspector.Text(h.Inlines) == ReferenceContent.Heading1).Level, "h1 level");
            TestSupport.AssertEqual(3, headings.First(h => ModelInspector.Text(h.Inlines) == ReferenceContent.HeadingCode).Level, "h3 level");

            foreach (string b in ReferenceContent.Bullets) TestSupport.Assert(snap.ListItems.Contains(b), "bullet " + b);
            TestSupport.Assert(snap.ListItems.Contains(ReferenceContent.NestedBullet), "nested bullet");
            TestSupport.Assert(snap.ListItems.Contains(ReferenceContent.DeepBullet), "deep bullet");
            ListBlock steps = ModelInspector.AllBlocks(m.Blocks).OfType<ListBlock>().First(l => l.Kind == ListKindEnum.Ordered);
            TestSupport.AssertEqual(3, steps.Items.Count, "ordered items");
            int depth = Depth(ModelInspector.AllBlocks(m.Blocks).OfType<ListBlock>().First(l => l.Kind == ListKindEnum.Unordered));
            TestSupport.AssertEqual(3, depth, "unordered nesting depth");

            foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "table row " + string.Join("|", row));
            TableBlock table = ModelInspector.AllBlocks(m.Blocks).OfType<TableBlock>().Single();
            TestSupport.AssertEqual(1, table.HeaderRowCount, "table header rows");

            CodeBlock code = ModelInspector.AllBlocks(m.Blocks).OfType<CodeBlock>().Single();
            TestSupport.AssertEqual(ReferenceContent.CodeText, code.Text.TrimEnd('\n'), "code text");
            if (expectCodeLanguage) TestSupport.AssertEqual(ReferenceContent.CodeLanguage, code.Language, "code language");
            QuoteBlock quote = ModelInspector.AllBlocks(m.Blocks).OfType<QuoteBlock>().Single();
            TestSupport.AssertContains(ModelInspector.Inspect(new DocumentModel { Blocks = quote.Blocks }).AllText, ReferenceContent.QuoteText, "quote text");

            TestSupport.AssertEqual(1, snap.ImageCount, "one image");
            BinaryResource image = m.Resources.Values.Single();
            TestSupport.AssertEqual("image/png", image.MediaType, "image media type");
            TestSupport.AssertEqual<int?>(ReferenceContent.ImageSize, image.PixelWidth, "image width");
            ImageBlock imageBlock = ModelInspector.AllBlocks(m.Blocks).OfType<ImageBlock>().Single();
            TestSupport.AssertEqual(ReferenceContent.ImageAlt, imageBlock.AltText, "alt text");
            TestSupport.Assert(snap.LinkUrls.Contains(ReferenceContent.LinkUrl), "link url");

            List<TextInline> runs = ModelInspector.AllBlocks(m.Blocks).OfType<ParagraphBlock>().SelectMany(p => p.Inlines).OfType<TextInline>().ToList();
            TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.BoldText && r.Style == InlineStyleEnum.Bold), "bold run");
            TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.ItalicText && r.Style == InlineStyleEnum.Italic), "italic run");
            TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.StrikeText && r.Style == InlineStyleEnum.Strikethrough), "strike run");
            TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.InlineCode && r.Style == InlineStyleEnum.Code), "code run");
            if (expectUnderline) TestSupport.Assert(runs.Any(r => r.Text == ReferenceContent.UnderlineText && r.Style == InlineStyleEnum.Underline), "underline run");

            int imageIndex = m.Blocks.FindIndex(b => b is ImageBlock);
            int quoteIndex = m.Blocks.FindIndex(b => b is QuoteBlock);
            TestSupport.Assert(imageIndex == quoteIndex + 1, "image sits right after the quote (position kept)");
        }

        private static int Depth(ListBlock list)
        {
            int max = 0;
            foreach (ListItemBlock item in list.Items)
                foreach (Block b in item.Blocks)
                    if (b is ListBlock nested) max = Math.Max(max, Depth(nested));
            return max + 1;
        }

        private static string CellText(TableBlock t, int row, int col)
        {
            return ModelInspector.Text(t.Rows[row].Cells[col].Blocks.OfType<ParagraphBlock>().SelectMany(p => p.Inlines));
        }
    }
}
