namespace DocConverter.Readers.Markdown
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using HtmlAgilityPack;
    using Markdig;
    using Markdig.Extensions.Tables;
    using Markdig.Extensions.TaskLists;
    using Markdig.Extensions.Yaml;
    using Md = Markdig.Syntax;
    using MdInline = Markdig.Syntax.Inlines;

    /// <summary>
    /// Reads Markdown (CommonMark plus GitHub flavored pipe tables, task lists, strikethrough and autolinks) with Markdig.
    /// Images with data URIs become resources; images with other URLs are kept as links and never fetched. YAML front matter
    /// supplies title, author, subject and keywords. Stateless and thread safe.
    /// </summary>
    public sealed class MarkdownDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Markdown };

        private static readonly MarkdownPipeline _Pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UseAutoLinks()
            .UseYamlFrontMatter()
            .Build();

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = (await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false)).TrimStart('﻿');
            token.ThrowIfCancellationRequested();
            Md.MarkdownDocument markdown = Markdig.Markdown.Parse(text, _Pipeline);

            DocumentModel document = new DocumentModel();
            foreach (Md.Block block in markdown)
            {
                token.ThrowIfCancellationRequested();
                MapBlock(block, document.Blocks, document, 0, context);
            }

            return document;
        }

        private static void MapBlock(Md.Block block, List<Block> target, DocumentModel document, int depth, ConversionContext context)
        {
            if (depth >= context.MaxNestingDepth)
            {
                context.AddWarning(WarningCodeEnum.NestedDepthLimited, "Markdown nested deeper than " + context.MaxNestingDepth + " levels was flattened.");
                return;
            }

            switch (block)
            {
                case YamlFrontMatterBlock yaml:
                    ReadFrontMatter(yaml, document.Metadata);
                    break;
                case Md.HeadingBlock heading:
                    HeadingBlock h = new HeadingBlock();
                    h.Level = heading.Level;
                    h.Inlines = MapInlines(heading.Inline, document, InlineStyleEnum.None);
                    target.Add(h);
                    break;
                case Md.ParagraphBlock paragraph:
                    MapParagraph(paragraph, target, document);
                    break;
                case Md.ListBlock list:
                    target.Add(MapList(list, document, depth, context));
                    break;
                case Md.QuoteBlock quote:
                    QuoteBlock q = new QuoteBlock();
                    foreach (Md.Block child in quote) MapBlock(child, q.Blocks, document, depth + 1, context);
                    target.Add(q);
                    break;
                case Md.FencedCodeBlock fenced:
                    string? language = string.IsNullOrWhiteSpace(fenced.Info) ? null : fenced.Info!.Trim();
                    target.Add(new CodeBlock(fenced.Lines.ToString(), language));
                    break;
                case Md.CodeBlock code:
                    target.Add(new CodeBlock(code.Lines.ToString(), null));
                    break;
                case Md.ThematicBreakBlock _:
                    target.Add(new ThematicBreakBlock());
                    break;
                case Table table:
                    target.Add(MapTable(table, document, depth, context));
                    break;
                case Md.HtmlBlock html:
                    string htmlText = HtmlText(html.Lines.ToString());
                    if (htmlText.Length > 0) target.Add(new ParagraphBlock(htmlText));
                    break;
                case Md.LinkReferenceDefinitionGroup _:
                    break;
                case Md.ContainerBlock container:
                    foreach (Md.Block child in container) MapBlock(child, target, document, depth + 1, context);
                    break;
            }
        }

        private static void MapParagraph(Md.ParagraphBlock paragraph, List<Block> target, DocumentModel document)
        {
            List<Inline> inlines = MapInlines(paragraph.Inline, document, InlineStyleEnum.None);
            bool onlyImages = inlines.Count > 0;
            foreach (Inline inline in inlines)
            {
                if (inline is ImageInline) continue;
                if (inline is TextInline t && t.Text.Trim().Length == 0) continue;
                if (inline is LineBreakInline) continue;
                onlyImages = false;
                break;
            }

            if (onlyImages)
            {
                foreach (Inline inline in inlines)
                    if (inline is ImageInline image) target.Add(new ImageBlock(image.ResourceId, image.AltText));
                return;
            }

            target.Add(new ParagraphBlock(inlines));
        }

        private static ListBlock MapList(Md.ListBlock list, DocumentModel document, int depth, ConversionContext context)
        {
            ListBlock result = new ListBlock(list.IsOrdered ? ListKindEnum.Ordered : ListKindEnum.Unordered);
            if (list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)) result.Start = start;

            foreach (Md.Block child in list)
            {
                if (!(child is Md.ListItemBlock item)) continue;
                ListItemBlock li = new ListItemBlock();
                foreach (Md.Block itemChild in item)
                {
                    if (itemChild is Md.ParagraphBlock p && p.Inline != null && p.Inline.FirstChild is TaskList task)
                    {
                        li.Checked = task.Checked;
                        result.Kind = ListKindEnum.Task;
                    }

                    MapBlock(itemChild, li.Blocks, document, depth + 1, context);
                }

                if (li.Checked.HasValue && li.Blocks.Count > 0 && li.Blocks[0] is ParagraphBlock first && first.Inlines.Count > 0 && first.Inlines[0] is TextInline lead)
                    lead.Text = lead.Text.TrimStart();

                result.Items.Add(li);
            }

            return result;
        }

        private static TableBlock MapTable(Table table, DocumentModel document, int depth, ConversionContext context)
        {
            TableBlock result = new TableBlock();
            foreach (TableColumnDefinition column in table.ColumnDefinitions)
            {
                TextAlignmentEnum alignment = TextAlignmentEnum.Default;
                if (column.Alignment.HasValue)
                {
                    if (column.Alignment.Value == TableColumnAlign.Left) alignment = TextAlignmentEnum.Left;
                    else if (column.Alignment.Value == TableColumnAlign.Center) alignment = TextAlignmentEnum.Center;
                    else if (column.Alignment.Value == TableColumnAlign.Right) alignment = TextAlignmentEnum.Right;
                }

                result.ColumnAlignments.Add(alignment);
            }

            int headerRows = 0;
            bool inHeader = true;
            foreach (Md.Block rowBlock in table)
            {
                if (!(rowBlock is Markdig.Extensions.Tables.TableRow row)) continue;
                Model.TableRow r = new Model.TableRow();
                foreach (Md.Block cellBlock in row)
                {
                    if (!(cellBlock is Markdig.Extensions.Tables.TableCell cell)) continue;
                    Model.TableCell c = new Model.TableCell();
                    c.IsHeader = row.IsHeader;
                    c.ColumnSpan = cell.ColumnSpan;
                    c.RowSpan = cell.RowSpan;
                    foreach (Md.Block content in cell) MapBlock(content, c.Blocks, document, depth + 1, context);
                    r.Cells.Add(c);
                }

                if (row.IsHeader && inHeader) headerRows++;
                else inHeader = false;
                result.Rows.Add(r);
            }

            while (result.ColumnAlignments.Count > 0 && result.ColumnAlignments[result.ColumnAlignments.Count - 1] == TextAlignmentEnum.Default)
                result.ColumnAlignments.RemoveAt(result.ColumnAlignments.Count - 1);

            result.HeaderRowCount = headerRows;
            return result;
        }

        private static List<Inline> MapInlines(MdInline.ContainerInline? container, DocumentModel document, InlineStyleEnum style)
        {
            List<Inline> result = new List<Inline>();
            if (container == null) return result;
            foreach (MdInline.Inline inline in container) MapInline(inline, result, document, style);
            return Merge(result);
        }

        private static void MapInline(MdInline.Inline inline, List<Inline> target, DocumentModel document, InlineStyleEnum style)
        {
            switch (inline)
            {
                case MdInline.LiteralInline literal:
                    target.Add(new TextInline(literal.Content.ToString(), style));
                    break;
                case MdInline.CodeInline code:
                    target.Add(new TextInline(code.Content, style | InlineStyleEnum.Code));
                    break;
                case MdInline.EmphasisInline emphasis:
                    InlineStyleEnum added = EmphasisStyle(emphasis);
                    foreach (MdInline.Inline child in emphasis) MapInline(child, target, document, style | added);
                    break;
                case MdInline.LinkInline link:
                    if (link.IsImage)
                    {
                        string alt = ModelText.Inlines(MapInlines(link, document, InlineStyleEnum.None));
                        Inline image = MapImage(link.Url ?? "", alt, link.Title, document);
                        target.Add(image);
                    }
                    else
                    {
                        LinkInline l = new LinkInline();
                        l.Url = link.Url ?? "";
                        l.Title = string.IsNullOrEmpty(link.Title) ? null : link.Title;
                        foreach (MdInline.Inline child in link) MapInline(child, l.Inlines, document, style);
                        if (l.Inlines.Count == 0) l.Inlines.Add(new TextInline(l.Url, style));
                        target.Add(l);
                    }

                    break;
                case MdInline.AutolinkInline auto:
                    string url = auto.IsEmail && !auto.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? "mailto:" + auto.Url : auto.Url;
                    LinkInline autoLink = new LinkInline();
                    autoLink.Url = url;
                    autoLink.Inlines.Add(new TextInline(auto.Url, style));
                    target.Add(autoLink);
                    break;
                case MdInline.LineBreakInline lineBreak:
                    if (lineBreak.IsHard) target.Add(new LineBreakInline());
                    else target.Add(new TextInline(" ", style));
                    break;
                case MdInline.HtmlEntityInline entity:
                    target.Add(new TextInline(entity.Transcoded.ToString(), style));
                    break;
                case MdInline.HtmlInline html:
                    string tag = html.Tag.Trim().ToLowerInvariant();
                    if (tag.StartsWith("<br", StringComparison.Ordinal)) target.Add(new LineBreakInline());
                    break;
                case TaskList _:
                    break;
                case MdInline.ContainerInline containerInline:
                    foreach (MdInline.Inline child in containerInline) MapInline(child, target, document, style);
                    break;
            }
        }

        private static InlineStyleEnum EmphasisStyle(MdInline.EmphasisInline emphasis)
        {
            char c = emphasis.DelimiterChar;
            int count = emphasis.DelimiterCount;
            if (c == '*' || c == '_') return count >= 2 ? InlineStyleEnum.Bold : InlineStyleEnum.Italic;
            if (c == '~') return count >= 2 ? InlineStyleEnum.Strikethrough : InlineStyleEnum.Subscript;
            if (c == '^') return InlineStyleEnum.Superscript;
            if (c == '+') return InlineStyleEnum.Underline;
            return InlineStyleEnum.None;
        }

        private static Inline MapImage(string url, string alt, string? title, DocumentModel document)
        {
            BinaryResource? resource = DataUri.TryDecode(url);
            if (resource == null)
            {
                LinkInline link = new LinkInline(url, string.IsNullOrEmpty(alt) ? url : alt);
                link.Title = title;
                return link;
            }

            string id = document.AddResource(resource);
            return new ImageInline(id, string.IsNullOrEmpty(alt) ? null : alt);
        }

        private static List<Inline> Merge(List<Inline> inlines)
        {
            List<Inline> merged = new List<Inline>();
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text && merged.Count > 0 && merged[merged.Count - 1] is TextInline previous && previous.Style == text.Style)
                {
                    previous.Text += text.Text;
                    continue;
                }

                merged.Add(inline);
            }

            return merged;
        }

        private static void ReadFrontMatter(YamlFrontMatterBlock yaml, DocumentMetadata metadata)
        {
            string[] lines = yaml.Lines.ToString().Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim().Trim('"', '\'');
                if (value.Length == 0) continue;
                if (key == "title") metadata.Title = value;
                else if (key == "author") metadata.Author = value;
                else if (key == "subject") metadata.Subject = value;
                else if (key == "description") metadata.Description = value;
                else if (key == "keywords") metadata.Keywords = value;
                else if (key == "lang" || key == "language") metadata.Language = value;
            }
        }

        private static string HtmlText(string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            foreach (HtmlNode node in doc.DocumentNode.Descendants())
                if (node.Name == "script" || node.Name == "style") node.InnerHtml = "";
            string text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? "");
            return string.Join(" ", text.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
