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
    /// Writers for Markdown, HTML, plain text, JSON, XML, CSV and TSV: reference content, every option, escaping and
    /// edge cases. Output is checked by inspectors that parse it independently.
    /// </summary>
    public static class TextWritersSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("TextWriters", "Text family writers");
            Converter c = new Converter();

            s.Add("Markdown_Reference", "Markdown output holds every reference element", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = MarkdownInspector.Inspect(r.Output);
                AssertCore(snap);
                TestSupport.Assert(snap.Headings.Contains(ReferenceContent.HeadingCode), "headings");
                TestSupport.Assert(snap.ListItems.Contains(ReferenceContent.DeepBullet), "deep bullet");
                foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "row " + string.Join("|", row));
                TestSupport.AssertEqual(0, snap.ImageCount, "no embedded image by default");
                TestSupport.Assert(snap.ContainsText("[Image:"), "image placeholder by default");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted by default");
                TestSupport.Assert(Encoding.UTF8.GetString(r.Output).IndexOf("data:image/", StringComparison.Ordinal) < 0, "no base64 payload by default");
                TestSupport.Assert(snap.LinkUrls.Contains(ReferenceContent.LinkUrl), "link");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "underline loss reported");
            });

            s.Add("Markdown_EmbedImages", "Markdown with ImageMode DataUri embeds the image as a data URI and raises no image warning", async ct =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Markdown.ImageMode = ImageModeEnum.DataUri;
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Markdown, o, ct).ConfigureAwait(false);
                ContentSnapshot snap = MarkdownInspector.Inspect(r.Output);
                TestSupport.AssertEqual(1, snap.ImageCount, "data URI image");
                TestSupport.AssertContains(Encoding.UTF8.GetString(r.Output), "data:image/png;base64,", "base64 payload");
                TestSupport.Assert(!r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "no placeholder warning");
            });

            s.Add("Markdown_MultiLineAltText", "Alt text spanning lines stays on one line in placeholders and in embedded image syntax", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.AddResource(new BinaryResource { Id = "logo", MediaType = "image/png", Data = ReferenceContent.ImagePng() });
                m.Blocks.Add(new ImageBlock("logo", "Logo\r\n\r\nDescription automatically generated"));
                StringConversionResult placeholder = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(placeholder.Output, "Image: Logo Description automatically generated, PNG", "placeholder on one line");

                ConversionOptions o = new ConversionOptions();
                o.Markdown.ImageMode = ImageModeEnum.DataUri;
                StringConversionResult embedded = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, o, ct).ConfigureAwait(false);
                TestSupport.AssertContains(embedded.Output, "![Logo Description automatically generated](data:image/png;base64,", "image syntax on one line");
                TestSupport.AssertEqual(1, MarkdownInspector.Inspect(Encoding.UTF8.GetBytes(embedded.Output)).ImageCount, "image parses");
            });

            s.Add("Html_Reference", "HTML output holds every reference element and head metadata, and is script free", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = HtmlInspector.Inspect(r.Output);
                AssertCore(snap);
                TestSupport.AssertEqual(ReferenceContent.Title, snap.Title, "title");
                foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "row");
                TestSupport.AssertEqual(1, snap.ImageCount, "image");
                TestSupport.AssertEqual(0, r.Warnings.Count, "HTML carries everything, no warnings");
                string html = Encoding.UTF8.GetString(r.Output);
                TestSupport.AssertContains(html, "<meta name=\"author\" content=\"DocConverter Tests\">", "author meta");
                TestSupport.AssertContains(html, "<u>underlined text</u>", "underline kept");
            });

            s.Add("Text_Reference", "Plain text holds every word, placeholders the image, and reports lost styling", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = TextInspector.Inspect(r.Output);
                AssertCore(snap);
                TestSupport.Assert(snap.ContainsText("[Image: Reference image, PNG 16x16]"), "image placeholder");
                TestSupport.Assert(snap.ContainsText("(https://example.com/docs)"), "link url kept");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted");
            });

            s.Add("Json_Reference", "Canonical JSON output round trips exactly", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Json, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = CanonicalInspector.InspectJson(r.Output);
                AssertCore(snap);
                TestSupport.AssertEqual(1, snap.ImageCount, "image");
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Json, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(ReferenceContent.ToModel(), back, "json round trip");
            });

            s.Add("Xml_Reference", "Canonical XML output round trips exactly", async ct =>
            {
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                ContentSnapshot snap = CanonicalInspector.InspectXml(r.Output);
                AssertCore(snap);
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(ReferenceContent.ToModel(), back, "xml round trip");
            });

            foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
            {
                DocumentFormatEnum format = f;
                s.Add(format + "_Reference", format + " output is the reference table, with the other content reported as dropped", async ct =>
                {
                    BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), format, null, ct).ConfigureAwait(false);
                    ContentSnapshot snap = DelimitedInspector.Inspect(r.Output, format == DocumentFormatEnum.Tsv ? '\t' : ',');
                    TestSupport.AssertEqual(ReferenceContent.TableRows.Length, snap.TableRows.Count, "rows");
                    foreach (string[] row in ReferenceContent.TableRows) TestSupport.Assert(snap.HasTableRow(row), "row");
                    TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.NonTableContentDropped), "NonTableContentDropped");
                });
            }

            s.Add("Markdown_EscapingRoundTrip", "Tricky text survives Markdown write and read unchanged", async ct =>
            {
                string[] samples =
                {
                    "*not italic*", "_not italic_", "**not bold**", "# not a heading", "1. not a list", "- not a bullet", "+ plus",
                    "> not a quote", "[not](a link)", "![not](an image)", "`not code`", "back\\slash", "pipe | pipe", "<b>not html</b>",
                    "&amp; entity", "AT&T and R&D", "a < b > c", "~~not struck~~", "x^2^", "=== not setext", "--- not a rule",
                    "100. hundred", "tab\tseparated", "trailing space ", "3 * 4 = 12", "snake_case_name", "C# and F#"
                };

                foreach (string sample in samples)
                {
                    DocumentModel m = new DocumentModel();
                    m.Blocks.Add(new ParagraphBlock(sample));
                    StringConversionResult md = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    DocumentModel back = await c.ReadAsync(md.Output, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    string text = back.Blocks.Count == 1 && back.Blocks[0] is ParagraphBlock p ? ModelInspector.Text(p.Inlines) : "(structure changed: " + back.Blocks.Count + " blocks)";
                    TestSupport.AssertEqual(sample.TrimEnd(), text.TrimEnd(), "round trip of '" + sample + "' via '" + md.Output.Trim() + "'");
                }
            });

            s.Add("Markdown_StyleRoundTrip", "Every Markdown expressible style combination round trips", async ct =>
            {
                InlineStyleEnum[] styles =
                {
                    InlineStyleEnum.Bold, InlineStyleEnum.Italic, InlineStyleEnum.Strikethrough, InlineStyleEnum.Code,
                    InlineStyleEnum.Superscript, InlineStyleEnum.Subscript, InlineStyleEnum.Bold | InlineStyleEnum.Italic,
                    InlineStyleEnum.Bold | InlineStyleEnum.Strikethrough, InlineStyleEnum.Italic | InlineStyleEnum.Code
                };
                foreach (InlineStyleEnum style in styles)
                {
                    DocumentModel m = new DocumentModel();
                    ParagraphBlock p = new ParagraphBlock();
                    p.Inlines.Add(new TextInline("before "));
                    p.Inlines.Add(new TextInline("styled", style));
                    p.Inlines.Add(new TextInline(" after"));
                    m.Blocks.Add(p);
                    StringConversionResult md = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    DocumentModel back = await c.ReadAsync(md.Output, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    TextInline? run = ((ParagraphBlock)back.Blocks[0]).Inlines.OfType<TextInline>().FirstOrDefault(t => t.Text == "styled");
                    TestSupport.Assert(run != null, "styled run present for " + style + ": " + md.Output.Trim());
                    TestSupport.AssertEqual(style, run!.Style, "style of " + md.Output.Trim());
                }
            });

            s.Add("Markdown_ImageModes", "Markdown image modes: DataUri, Omit, Placeholder and External with side files", async ct =>
            {
                foreach (ImageModeEnum mode in new[] { ImageModeEnum.DataUri, ImageModeEnum.Omit, ImageModeEnum.Placeholder, ImageModeEnum.External })
                {
                    ConversionOptions o = new ConversionOptions();
                    o.Markdown.ImageMode = mode;
                    StringConversionResult r = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Markdown, o, ct).ConfigureAwait(false);
                    switch (mode)
                    {
                        case ImageModeEnum.DataUri:
                            TestSupport.AssertContains(r.Output, "![Reference image](data:image/png;base64,", "data uri");
                            break;
                        case ImageModeEnum.Omit:
                            TestSupport.AssertNotContains(r.Output, "Reference image", "omitted");
                            TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "ImagesOmitted");
                            break;
                        case ImageModeEnum.Placeholder:
                            TestSupport.AssertContains(r.Output, "\\[Image: Reference image, PNG 16x16\\]", "placeholder");
                            TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagePlaceholderEmitted), "ImagePlaceholderEmitted");
                            break;
                        case ImageModeEnum.External:
                            TestSupport.AssertContains(r.Output, "![Reference image](reference.png)", "external reference");
                            TestSupport.AssertEqual(1, r.Resources.Count, "one side file");
                            TestSupport.AssertEqual("reference.png", r.Resources[0].FileName, "side file name");
                            TestSupport.Assert(r.Resources[0].Data.Length > 0, "side file bytes");
                            break;
                    }
                }
            });

            s.Add("Markdown_ExternalNamesUnique", "External image names never collide", async ct =>
            {
                DocumentModel m = new DocumentModel();
                for (int i = 0; i < 3; i++)
                {
                    string id = m.AddResource(new BinaryResource { MediaType = "image/png", Data = ReferenceContent.ImagePng(), FileName = "same.png" });
                    m.Blocks.Add(new ImageBlock(id, "pic " + i));
                }

                ConversionOptions o = new ConversionOptions();
                o.Markdown.ImageMode = ImageModeEnum.External;
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, o, ct).ConfigureAwait(false);
                List<string> names = r.Resources.Select(x => x.FileName!).ToList();
                TestSupport.AssertEqual(3, names.Distinct().Count(), "three distinct names: " + string.Join(",", names));
            });

            s.Add("Markdown_TableSpans", "Merged cells are repeated or emptied per TableSpanMode, with TableSpansFlattened", async ct =>
            {
                foreach (TableSpanModeEnum mode in new[] { TableSpanModeEnum.Repeat, TableSpanModeEnum.Empty })
                {
                    ConversionOptions o = new ConversionOptions();
                    o.Markdown.TableSpanMode = mode;
                    StringConversionResult r = await c.WriteToStringAsync(SpanTable(), DocumentFormatEnum.Markdown, o, ct).ConfigureAwait(false);
                    ContentSnapshot snap = MarkdownInspector.Inspect(Encoding.UTF8.GetBytes(r.Output));
                    TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.TableSpansFlattened), "warning");
                    if (mode == TableSpanModeEnum.Repeat)
                    {
                        TestSupport.Assert(snap.HasTableRow(new[] { "Wide", "Wide" }), "colspan repeated");
                        TestSupport.Assert(snap.HasTableRow(new[] { "Tall", "b2" }), "rowspan origin row");
                        TestSupport.Assert(snap.HasTableRow(new[] { "Tall", "c2" }), "rowspan repeated into row 3");
                    }
                    else
                    {
                        TestSupport.Assert(snap.HasTableRow(new[] { "Wide", "" }), "colspan emptied");
                        TestSupport.Assert(snap.HasTableRow(new[] { "Tall", "b2" }), "rowspan origin row");
                        TestSupport.Assert(snap.HasTableRow(new[] { "", "c2" }), "rowspan emptied in row 3");
                    }
                }
            });

            s.Add("Markdown_ListsAndCode", "Task lists, ordered start numbers, nested blocks in items and code with backticks", async ct =>
            {
                DocumentModel m = new DocumentModel();
                ListBlock tasks = new ListBlock(ListKindEnum.Task);
                tasks.Items.Add(new ListItemBlock("done") { Checked = true });
                tasks.Items.Add(new ListItemBlock("open") { Checked = false });
                m.Blocks.Add(tasks);
                ListBlock ordered = new ListBlock(ListKindEnum.Ordered) { Start = 7 };
                ListItemBlock item = new ListItemBlock("seven");
                item.Blocks.Add(new CodeBlock("x = `y`;\n```nested fence```", "js"));
                ordered.Items.Add(item);
                ordered.Items.Add(new ListItemBlock("eight"));
                m.Blocks.Add(ordered);
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(r.Output, "- [x] done", "checked task");
                TestSupport.AssertContains(r.Output, "7. seven", "start number");
                TestSupport.AssertContains(r.Output, "````js", "fence longer than inner backticks");
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(m, back, "markdown lists and code round trip");
            });

            s.Add("Markdown_EscapeHtmlOption", "EscapeHtml true protects markup-like text; false lets it through", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(new ParagraphBlock("Use <kbd>Ctrl</kbd> &amp; friends"));
                StringConversionResult escaped = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(escaped.Output, "\\<kbd>", "tag escaped by default");
                TestSupport.AssertContains(escaped.Output, "\\&amp;", "entity escaped by default");
                ConversionOptions raw = new ConversionOptions();
                raw.Markdown.EscapeHtml = false;
                StringConversionResult passthrough = await c.WriteToStringAsync(m, DocumentFormatEnum.Markdown, raw, ct).ConfigureAwait(false);
                TestSupport.AssertContains(passthrough.Output, "Use <kbd>Ctrl</kbd> &amp; friends", "passed through");
            });

            s.Add("Html_Modes", "Fragment mode omits the document shell; the stylesheet can be turned off", async ct =>
            {
                ConversionOptions fragment = new ConversionOptions();
                fragment.Html.Mode = HtmlOutputModeEnum.Fragment;
                StringConversionResult a = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Html, fragment, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(a.Output, "<!DOCTYPE", "no doctype");
                TestSupport.AssertNotContains(a.Output, "<head>", "no head");
                TestSupport.Assert(a.Output.StartsWith("<h1>", StringComparison.Ordinal), "starts with content");
                ConversionOptions noCss = new ConversionOptions();
                noCss.Html.IncludeStylesheet = false;
                StringConversionResult b = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Html, noCss, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(b.Output, "<style>", "no stylesheet");
                TestSupport.AssertContains(b.Output, "<!DOCTYPE html>", "still a document");
            });

            s.Add("Html_Escaping", "Text that looks like markup is encoded, attributes are quoted safely", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Metadata.Title = "A \"quoted\" <title> & more";
                m.Blocks.Add(new ParagraphBlock("<script>alert('x')</script> & <img src=x onerror=alert(1)>"));
                ParagraphBlock p = new ParagraphBlock();
                LinkInline link = new LinkInline("https://example.com/?a=1&b=\"2\"", "q");
                link.Title = "t\"itle";
                p.Inlines.Add(link);
                m.Blocks.Add(p);
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                HtmlInspector.Inspect(Encoding.UTF8.GetBytes(r.Output));
                TestSupport.AssertContains(r.Output, "&lt;script&gt;", "script text encoded");
                TestSupport.AssertContains(r.Output, "href=\"https://example.com/?a=1&amp;b=&quot;2&quot;\"", "href encoded");
                TestSupport.AssertContains(r.Output, "<title>A \"quoted\" &lt;title&gt; &amp; more</title>", "title encoded");
            });

            s.Add("Html_ImageModes", "HTML image modes: DataUri, Omit, Placeholder and External", async ct =>
            {
                foreach (ImageModeEnum mode in new[] { ImageModeEnum.DataUri, ImageModeEnum.Omit, ImageModeEnum.Placeholder, ImageModeEnum.External })
                {
                    ConversionOptions o = new ConversionOptions();
                    o.Html.ImageMode = mode;
                    StringConversionResult r = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Html, o, ct).ConfigureAwait(false);
                    if (mode == ImageModeEnum.DataUri) TestSupport.AssertContains(r.Output, "<img src=\"data:image/png;base64,", "data uri");
                    if (mode == ImageModeEnum.Omit) TestSupport.AssertNotContains(r.Output, "<img", "omitted");
                    if (mode == ImageModeEnum.Placeholder) TestSupport.AssertContains(r.Output, "[Image: Reference image, PNG 16x16]", "placeholder");
                    if (mode == ImageModeEnum.External)
                    {
                        TestSupport.AssertContains(r.Output, "<img src=\"reference.png\"", "external src");
                        TestSupport.AssertEqual(1, r.Resources.Count, "side file");
                    }
                }
            });

            s.Add("Html_Structure", "Spans, captions, task lists, sections, ids, alignment and page breaks", async ct =>
            {
                DocumentModel m = SpanTable();
                ((TableBlock)m.Blocks[0]).Caption = "Spans";
                SectionBlock slide = new SectionBlock(SectionKindEnum.Slide, "Slide one");
                slide.Blocks.Add(new ParagraphBlock("inside") { Alignment = TextAlignmentEnum.Center, Id = "p1" });
                m.Blocks.Add(slide);
                m.Blocks.Add(new PageBreakBlock());
                ListBlock tasks = new ListBlock(ListKindEnum.Task);
                tasks.Items.Add(new ListItemBlock("done") { Checked = true });
                m.Blocks.Add(tasks);
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(r.Output, "colspan=\"2\"", "colspan");
                TestSupport.AssertContains(r.Output, "rowspan=\"2\"", "rowspan");
                TestSupport.AssertContains(r.Output, "<caption>Spans</caption>", "caption");
                TestSupport.AssertContains(r.Output, "<section data-kind=\"slide\">", "section");
                TestSupport.AssertContains(r.Output, "<h2>Slide one</h2>", "section title");
                TestSupport.AssertContains(r.Output, "<p id=\"p1\" style=\"text-align:center\">inside</p>", "id and alignment");
                TestSupport.AssertContains(r.Output, "docconverter-page-break", "page break");
                TestSupport.AssertContains(r.Output, "<input type=\"checkbox\" disabled checked>", "task checkbox");
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TableBlock t = ModelInspector.AllBlocks(back.Blocks).OfType<TableBlock>().First();
                TestSupport.AssertEqual(2, t.Rows[0].Cells[0].ColumnSpan, "colspan read back");
                TestSupport.AssertEqual(2, t.Rows[1].Cells[0].RowSpan, "rowspan read back");
            });

            s.Add("Text_Options", "Heading styles, table styles, wrapping, link URLs and image placeholders", async ct =>
            {
                ConversionOptions upper = new ConversionOptions();
                upper.Text.HeadingStyle = TextHeadingStyleEnum.Uppercase;
                upper.Text.TableStyle = TextTableStyleEnum.Tabs;
                upper.Text.IncludeLinkUrls = false;
                upper.Text.IncludeImagePlaceholders = false;
                StringConversionResult a = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Text, upper, ct).ConfigureAwait(false);
                TestSupport.AssertContains(a.Output, "REFERENCE DOCUMENT", "uppercase heading");
                TestSupport.AssertContains(a.Output, "Grace Hopper\tAdmiral\t40", "tab table");
                TestSupport.AssertNotContains(a.Output, "(https://example.com/docs)", "no link url");
                TestSupport.AssertNotContains(a.Output, "[Image:", "no image placeholder");
                TestSupport.Assert(a.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "ImagesOmitted");

                ConversionOptions wrap = new ConversionOptions();
                wrap.Text.WrapColumn = 30;
                StringConversionResult b = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Text, wrap, ct).ConfigureAwait(false);
                string styled = b.Output.Split(new[] { "\n\n" }, StringSplitOptions.None).First(x => x.StartsWith("This paragraph", StringComparison.Ordinal));
                foreach (string line in styled.Split('\n')) TestSupport.Assert(line.Length <= 30, "wrapped line '" + line + "'");
                TestSupport.Assert(ContentSnapshot.Normalize(styled).Contains("bold text, italic text"), "wrapping keeps words");
            });

            s.Add("Csv_Selection", "TableSelection First, All and Index, and Index out of range", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(Table("A", "1"));
                m.Blocks.Add(new ParagraphBlock("between"));
                m.Blocks.Add(Table("B", "2"));
                ConversionOptions all = new ConversionOptions();
                all.Csv.TableSelection = TableSelectionEnum.All;
                StringConversionResult ra = await c.WriteToStringAsync(m, DocumentFormatEnum.Csv, all, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("A\n1\n\nB\n2\n", ra.Output, "all tables separated by an empty record");
                ConversionOptions index = new ConversionOptions();
                index.Csv.TableSelection = TableSelectionEnum.Index;
                index.Csv.TableIndex = 1;
                StringConversionResult ri = await c.WriteToStringAsync(m, DocumentFormatEnum.Csv, index, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("B\n2\n", ri.Output, "second table");
                StringConversionResult rf = await c.WriteToStringAsync(m, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("A\n1\n", rf.Output, "first table");
                TestSupport.Assert(rf.Warnings.Any(w => w.Code == WarningCodeEnum.NonTableContentDropped), "dropped content reported");
                index.Csv.TableIndex = 5;
                await TestSupport.ExpectThrowsAsync<DocumentWriteException>(() => c.WriteToStringAsync(m, DocumentFormatEnum.Csv, index, ct), "index out of range").ConfigureAwait(false);
            });

            s.Add("Csv_NoTables", "Without tables: one block per row by default, or DocumentWriteException with Error", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(new HeadingBlock(1, "Title"));
                m.Blocks.Add(new ParagraphBlock("Hello, world"));
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("Title\n\"Hello, world\"\n", r.Output, "rows");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost");
                ConversionOptions strict = new ConversionOptions();
                strict.Csv.NoTableBehavior = NoTableBehaviorEnum.Error;
                await TestSupport.ExpectThrowsAsync<DocumentWriteException>(() => c.WriteToStringAsync(m, DocumentFormatEnum.Csv, strict, ct), "Error behavior").ConfigureAwait(false);
            });

            s.Add("Csv_Quoting", "Fields with delimiters, quotes, newlines and edge spaces are quoted per RFC 4180", async ct =>
            {
                DocumentModel m = new DocumentModel();
                TableBlock t = new TableBlock();
                t.Rows.Add(new TableRow(new[] { "a,b", "say \"hi\"", "line1\nline2", " padded ", "plain" }));
                m.Blocks.Add(t);
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("\"a,b\",\"say \"\"hi\"\"\",\"line1\nline2\",\" padded \",plain\n", r.Output, "quoted record");
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Csv, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("line1\nline2", ModelInspector.Text(((TableBlock)back.Blocks[0]).Rows[0].Cells[2].Blocks.OfType<ParagraphBlock>().SelectMany(p => p.Inlines)), "newline survives");
            });

            s.Add("Csv_DelimiterAndSpans", "A custom delimiter and span handling in CSV", async ct =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Csv.Delimiter = ';';
                o.Csv.TableSpanMode = TableSpanModeEnum.Empty;
                StringConversionResult r = await c.WriteToStringAsync(SpanTable(), DocumentFormatEnum.Csv, o, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("Wide;\nTall;b2\n;c2\n", r.Output, "semicolons, emptied spans");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.TableSpansFlattened), "TableSpansFlattened");
            });

            s.Add("Json_Options", "Compact JSON has no newlines; IncludeBinary false drops bytes and warns", async ct =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Json.Indented = false;
                o.Json.IncludeBinary = false;
                StringConversionResult r = await c.WriteToStringAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Json, o, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(r.Output, "\n", "compact");
                TestSupport.AssertNotContains(r.Output, "\"data\"", "no data");
                TestSupport.AssertContains(r.Output, "\"size\":", "size kept");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "ImagesOmitted");
            });

            s.Add("Xml_InvalidCharacters", "Characters XML 1.0 cannot hold are removed and the output parses", async ct =>
            {
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(new ParagraphBlock("bell\u0007 null\u0000 escape\u001b ok 🚀"));
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                CanonicalInspector.InspectXml(Encoding.UTF8.GetBytes(r.Output));
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("bell null escape ok 🚀", ModelInspector.Text(((ParagraphBlock)back.Blocks[0]).Inlines), "cleaned text, emoji kept");
            });

            s.Add("Xml_EncodingDeclaration", "The XML declaration names the output encoding", async ct =>
            {
                ConversionOptions o = new ConversionOptions { OutputEncoding = new UnicodeEncoding(false, false) };
                BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Xml, o, ct).ConfigureAwait(false);
                string xml = Encoding.Unicode.GetString(r.Output);
                TestSupport.Assert(xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-16\"?>", StringComparison.Ordinal), "declaration: " + xml.Substring(0, 50));
                DocumentModel back = await c.ReadAsync(r.Output, DocumentFormatEnum.Xml, null, ct).ConfigureAwait(false);
                ModelComparer.AssertEqual(ReferenceContent.ToModel(), back, "utf-16 xml round trip");
            });

            s.Add("EmptyDocument", "Every text writer handles an empty document", async ct =>
            {
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
                {
                    BytesConversionResult r = await c.WriteToBytesAsync(new DocumentModel(), f, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(r.Output != null, f + " output");
                }
            });

            s.Add("LargeTable", "A 10,000 row table is written by every text writer", async ct =>
            {
                DocumentModel m = new DocumentModel();
                TableBlock t = new TableBlock { HeaderRowCount = 1 };
                t.Rows.Add(new TableRow(new[] { "id", "name", "value" }));
                for (int i = 0; i < 10000; i++) t.Rows.Add(new TableRow(new[] { i.ToString(System.Globalization.CultureInfo.InvariantCulture), "row " + i, (i * 3).ToString(System.Globalization.CultureInfo.InvariantCulture) }));
                m.Blocks.Add(t);
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv })
                {
                    StringConversionResult r = await c.WriteToStringAsync(m, f, null, ct).ConfigureAwait(false);
                    TestSupport.AssertContains(r.Output, "row 9999", f + " last row");
                }
            });

            return s.Build();
        }

        private static void AssertCore(ContentSnapshot snap)
        {
            foreach (string snippet in ReferenceContent.CoreTextSnippets) TestSupport.Assert(snap.ContainsText(snippet), "output contains '" + snippet + "'");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.International), "international text");
            TestSupport.Assert(snap.ContainsText(ReferenceContent.Special), "special characters paragraph intact");
            TestSupport.Assert(snap.ContainsText("Console.WriteLine(total);"), "code text");
        }

        private static DocumentModel SpanTable()
        {
            DocumentModel m = new DocumentModel();
            TableBlock t = new TableBlock();
            TableRow r1 = new TableRow();
            r1.Cells.Add(new TableCell("Wide") { ColumnSpan = 2 });
            t.Rows.Add(r1);
            TableRow r2 = new TableRow();
            r2.Cells.Add(new TableCell("Tall") { RowSpan = 2 });
            r2.Cells.Add(new TableCell("b2"));
            t.Rows.Add(r2);
            TableRow r3 = new TableRow();
            r3.Cells.Add(new TableCell("c2"));
            t.Rows.Add(r3);
            m.Blocks.Add(t);
            return m;
        }

        private static TableBlock Table(string header, string value)
        {
            TableBlock t = new TableBlock { HeaderRowCount = 1 };
            t.Rows.Add(new TableRow(new[] { header }));
            t.Rows.Add(new TableRow(new[] { value }));
            return t;
        }
    }
}
