namespace DocConverter.Readers.Html
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using HtmlAgilityPack;

    /// <summary>
    /// Reads HTML with HtmlAgilityPack. Headings, paragraphs, lists, tables (with th, thead, colspan and rowspan), pre and
    /// code, blockquote, hr and img map to the model; inline b, strong, i, em, u, ins, s, del, code, sup, sub and a become
    /// styles and links. script, style, noscript, template, and head content other than metadata are ignored. Images with
    /// data URIs become resources; other image sources are kept as links and never fetched. Stateless and thread safe.
    /// </summary>
    public sealed class HtmlDocumentReader : IDocumentReader
    {
        #region Private-Members

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Html };

        private static readonly HashSet<string> _Ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "noscript", "template", "head", "svg", "canvas", "iframe", "object", "embed", "button", "select", "input", "textarea", "form", "#comment"
        };

        private static readonly HashSet<string> _Containers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "html", "body", "div", "section", "article", "main", "header", "footer", "nav", "aside", "figure", "center", "details", "summary", "dl", "dd", "dt", "address", "fieldset", "hgroup"
        };

        #endregion

        #region Public-Members

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = (await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false)).TrimStart('﻿');
            token.ThrowIfCancellationRequested();

            HtmlDocument html = new HtmlDocument();
            html.OptionFixNestedTags = true;
            html.OptionAutoCloseOnEnd = true;
            html.LoadHtml(text);

            DocumentModel document = new DocumentModel();
            ReadMetadata(html, document.Metadata);

            HtmlNode root = html.DocumentNode.SelectSingleNode("//body") ?? html.DocumentNode;
            ParagraphBlock? pending = null;
            MapChildren(root, document.Blocks, document, ref pending, 0, context, token);
            Flush(document.Blocks, ref pending);
            return document;
        }

        #endregion

        #region Private-Methods

        private static void ReadMetadata(HtmlDocument html, DocumentMetadata metadata)
        {
            HtmlNode? title = html.DocumentNode.SelectSingleNode("//head/title") ?? html.DocumentNode.SelectSingleNode("//title");
            if (title != null)
            {
                string t = Collapse(HtmlEntity.DeEntitize(title.InnerText ?? ""));
                if (t.Length > 0) metadata.Title = t;
            }

            HtmlNodeCollection? metas = html.DocumentNode.SelectNodes("//meta[@name]");
            if (metas != null)
            {
                foreach (HtmlNode meta in metas)
                {
                    string name = meta.GetAttributeValue("name", "").ToLowerInvariant();
                    string content = HtmlEntity.DeEntitize(meta.GetAttributeValue("content", "")).Trim();
                    if (content.Length == 0) continue;
                    if (name == "author") metadata.Author = content;
                    else if (name == "description") metadata.Description = content;
                    else if (name == "keywords") metadata.Keywords = content;
                    else if (name == "subject") metadata.Subject = content;
                }
            }

            HtmlNode? htmlNode = html.DocumentNode.SelectSingleNode("//html");
            if (htmlNode != null)
            {
                string lang = htmlNode.GetAttributeValue("lang", "");
                if (lang.Length > 0) metadata.Language = lang;
            }
        }

        private static void MapChildren(HtmlNode parent, List<Block> target, DocumentModel document, ref ParagraphBlock? pending, int depth, ConversionContext context, CancellationToken token)
        {
            foreach (HtmlNode node in parent.ChildNodes)
            {
                token.ThrowIfCancellationRequested();
                MapNode(node, target, document, ref pending, depth, context, token);
            }
        }

        private static void MapNode(HtmlNode node, List<Block> target, DocumentModel document, ref ParagraphBlock? pending, int depth, ConversionContext context, CancellationToken token)
        {
            if (node.NodeType == HtmlNodeType.Comment) return;
            if (node.NodeType == HtmlNodeType.Text)
            {
                AppendInline(node, target, document, ref pending, InlineStyleEnum.None);
                return;
            }

            if (node.NodeType != HtmlNodeType.Element) return;
            string name = node.Name.ToLowerInvariant();
            if (_Ignored.Contains(name)) return;

            if (depth >= context.MaxNestingDepth)
            {
                context.AddWarning(WarningCodeEnum.NestedDepthLimited, "HTML nested deeper than " + context.MaxNestingDepth + " levels was flattened to text.");
                Flush(target, ref pending);
                string flat = Collapse(HtmlEntity.DeEntitize(node.InnerText ?? ""));
                if (flat.Length > 0) target.Add(new ParagraphBlock(flat));
                return;
            }

            switch (name)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    Flush(target, ref pending);
                    HeadingBlock heading = new HeadingBlock();
                    heading.Level = name[1] - '0';
                    heading.Id = NullIfEmpty(node.GetAttributeValue("id", ""));
                    heading.Inlines = Trim(InlinesOf(node, document, InlineStyleEnum.None));
                    target.Add(heading);
                    return;
                case "p":
                    Flush(target, ref pending);
                    MapParagraphElement(node, target, document);
                    return;
                case "ul":
                case "ol":
                case "menu":
                    Flush(target, ref pending);
                    target.Add(MapList(node, document, depth, context, token));
                    return;
                case "table":
                    Flush(target, ref pending);
                    target.Add(MapTable(node, document, depth, context, token));
                    return;
                case "pre":
                    Flush(target, ref pending);
                    target.Add(MapPre(node));
                    return;
                case "blockquote":
                    Flush(target, ref pending);
                    QuoteBlock quote = new QuoteBlock();
                    ParagraphBlock? inner = null;
                    MapChildren(node, quote.Blocks, document, ref inner, depth + 1, context, token);
                    Flush(quote.Blocks, ref inner);
                    target.Add(quote);
                    return;
                case "hr":
                    Flush(target, ref pending);
                    target.Add(new ThematicBreakBlock());
                    return;
                case "img":
                    if (IsBlockImage(node))
                    {
                        Flush(target, ref pending);
                        Inline image = MapImage(node, document);
                        if (image is ImageInline img) target.Add(new ImageBlock(img.ResourceId, img.AltText));
                        else target.Add(new ParagraphBlock(new List<Inline> { image }));
                        return;
                    }

                    AppendInline(node, target, document, ref pending, InlineStyleEnum.None);
                    return;
                case "figcaption":
                    Flush(target, ref pending);
                    string caption = Collapse(HtmlEntity.DeEntitize(node.InnerText ?? ""));
                    if (caption.Length > 0)
                    {
                        if (target.Count > 0 && target[target.Count - 1] is ImageBlock previousImage && previousImage.Caption == null) previousImage.Caption = caption;
                        else target.Add(new ParagraphBlock(caption));
                    }

                    return;
                case "li":
                    Flush(target, ref pending);
                    ListBlock stray = new ListBlock(ListKindEnum.Unordered);
                    stray.Items.Add(MapListItem(node, document, depth, context, token));
                    target.Add(stray);
                    return;
            }

            if (_Containers.Contains(name))
            {
                Flush(target, ref pending);
                MapChildren(node, target, document, ref pending, depth + 1, context, token);
                Flush(target, ref pending);
                return;
            }

            AppendInline(node, target, document, ref pending, InlineStyleEnum.None);
        }

        private static void MapParagraphElement(HtmlNode node, List<Block> target, DocumentModel document)
        {
            List<Inline> inlines = Trim(InlinesOf(node, document, InlineStyleEnum.None));
            if (inlines.Count == 0) return;
            bool onlyImages = true;
            foreach (Inline inline in inlines)
            {
                if (inline is ImageInline) continue;
                if (inline is TextInline t && t.Text.Trim().Length == 0) continue;
                onlyImages = false;
                break;
            }

            if (onlyImages)
            {
                foreach (Inline inline in inlines)
                    if (inline is ImageInline image) target.Add(new ImageBlock(image.ResourceId, image.AltText));
                return;
            }

            ParagraphBlock paragraph = new ParagraphBlock(inlines);
            paragraph.Alignment = Alignment(node);
            paragraph.Id = NullIfEmpty(node.GetAttributeValue("id", ""));
            target.Add(paragraph);
        }

        private static ListBlock MapList(HtmlNode node, DocumentModel document, int depth, ConversionContext context, CancellationToken token)
        {
            ListBlock list = new ListBlock(node.Name.Equals("ol", StringComparison.OrdinalIgnoreCase) ? ListKindEnum.Ordered : ListKindEnum.Unordered);
            if (list.Kind == ListKindEnum.Ordered && int.TryParse(node.GetAttributeValue("start", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)) list.Start = start;

            foreach (HtmlNode child in node.ChildNodes)
            {
                if (child.NodeType != HtmlNodeType.Element) continue;
                if (!child.Name.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;
                ListItemBlock item = MapListItem(child, document, depth, context, token);
                if (item.Checked.HasValue) list.Kind = ListKindEnum.Task;
                list.Items.Add(item);
            }

            return list;
        }

        private static ListItemBlock MapListItem(HtmlNode li, DocumentModel document, int depth, ConversionContext context, CancellationToken token)
        {
            ListItemBlock item = new ListItemBlock();
            HtmlNode? checkbox = li.SelectSingleNode("./input[@type='checkbox']") ?? li.SelectSingleNode("./p/input[@type='checkbox']");
            if (checkbox != null) item.Checked = checkbox.Attributes.Contains("checked");

            ParagraphBlock? pending = null;
            MapChildren(li, item.Blocks, document, ref pending, depth + 1, context, token);
            Flush(item.Blocks, ref pending);
            return item;
        }

        private static TableBlock MapTable(HtmlNode node, DocumentModel document, int depth, ConversionContext context, CancellationToken token)
        {
            TableBlock table = new TableBlock();
            HtmlNode? caption = node.SelectSingleNode("./caption");
            if (caption != null)
            {
                string c = Collapse(HtmlEntity.DeEntitize(caption.InnerText ?? ""));
                if (c.Length > 0) table.Caption = c;
            }

            List<HtmlNode> rows = new List<HtmlNode>();
            int theadRows = 0;
            foreach (HtmlNode child in node.ChildNodes)
            {
                if (child.NodeType != HtmlNodeType.Element) continue;
                string n = child.Name.ToLowerInvariant();
                if (n == "tr") rows.Add(child);
                else if (n == "thead" || n == "tbody" || n == "tfoot")
                {
                    foreach (HtmlNode tr in child.ChildNodes)
                    {
                        if (tr.NodeType == HtmlNodeType.Element && tr.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
                        {
                            rows.Add(tr);
                            if (n == "thead") theadRows++;
                        }
                    }
                }
            }

            int headerRows = 0;
            bool leading = true;
            for (int r = 0; r < rows.Count; r++)
            {
                token.ThrowIfCancellationRequested();
                TableRow row = new TableRow();
                bool allHeader = true;
                bool any = false;
                foreach (HtmlNode cellNode in rows[r].ChildNodes)
                {
                    if (cellNode.NodeType != HtmlNodeType.Element) continue;
                    string n = cellNode.Name.ToLowerInvariant();
                    if (n != "td" && n != "th") continue;
                    any = true;
                    TableCell cell = new TableCell();
                    cell.IsHeader = n == "th" || r < theadRows;
                    if (!cell.IsHeader) allHeader = false;
                    cell.ColumnSpan = ParsePositive(cellNode.GetAttributeValue("colspan", "1"));
                    cell.RowSpan = ParsePositive(cellNode.GetAttributeValue("rowspan", "1"));
                    ParagraphBlock? pending = null;
                    MapChildren(cellNode, cell.Blocks, document, ref pending, depth + 1, context, token);
                    Flush(cell.Blocks, ref pending);
                    row.Cells.Add(cell);
                }

                if (!any) continue;
                if (leading && allHeader) headerRows++;
                else leading = false;
                table.Rows.Add(row);
            }

            table.HeaderRowCount = headerRows;
            return table;
        }

        private static CodeBlock MapPre(HtmlNode node)
        {
            string? language = null;
            HtmlNode? code = node.SelectSingleNode("./code");
            string cls = (code ?? node).GetAttributeValue("class", "");
            foreach (string part in cls.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("language-", StringComparison.OrdinalIgnoreCase)) language = part.Substring(9);
                else if (part.StartsWith("lang-", StringComparison.OrdinalIgnoreCase)) language = part.Substring(5);
            }

            string text = HtmlEntity.DeEntitize(node.InnerText ?? "").Replace("\r\n", "\n");
            if (text.StartsWith("\n", StringComparison.Ordinal)) text = text.Substring(1);
            return new CodeBlock(text.TrimEnd('\n'), language);
        }

        private static void AppendInline(HtmlNode node, List<Block> target, DocumentModel document, ref ParagraphBlock? pending, InlineStyleEnum style)
        {
            List<Inline> inlines = new List<Inline>();
            MapInline(node, inlines, document, style);
            if (inlines.Count == 0) return;
            if (pending == null)
            {
                bool meaningful = false;
                foreach (Inline i in inlines)
                {
                    if (i is TextInline t && t.Text.Trim().Length == 0) continue;
                    meaningful = true;
                    break;
                }

                if (!meaningful) return;
                pending = new ParagraphBlock();
            }

            pending.Inlines.AddRange(inlines);
        }

        private static void Flush(List<Block> target, ref ParagraphBlock? pending)
        {
            if (pending == null) return;
            pending.Inlines = Trim(Merge(pending.Inlines));
            if (pending.Inlines.Count > 0) target.Add(pending);
            pending = null;
        }

        private static List<Inline> InlinesOf(HtmlNode node, DocumentModel document, InlineStyleEnum style)
        {
            List<Inline> inlines = new List<Inline>();
            foreach (HtmlNode child in node.ChildNodes) MapInline(child, inlines, document, style);
            return Merge(inlines);
        }

        private static void MapInline(HtmlNode node, List<Inline> target, DocumentModel document, InlineStyleEnum style)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string raw = HtmlEntity.DeEntitize(((HtmlTextNode)node).Text ?? "");
                string text = CollapseKeepEdges(raw);
                if (text.Length > 0) target.Add(new TextInline(text, style));
                return;
            }

            if (node.NodeType != HtmlNodeType.Element) return;
            string name = node.Name.ToLowerInvariant();
            if (_Ignored.Contains(name)) return;

            switch (name)
            {
                case "br":
                    target.Add(new LineBreakInline());
                    return;
                case "img":
                    target.Add(MapImage(node, document));
                    return;
                case "a":
                    string href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", "")).Trim();
                    if (href.Length == 0)
                    {
                        foreach (HtmlNode child in node.ChildNodes) MapInline(child, target, document, style);
                        return;
                    }

                    LinkInline link = new LinkInline();
                    link.Url = href;
                    string title = node.GetAttributeValue("title", "");
                    if (title.Length > 0) link.Title = HtmlEntity.DeEntitize(title);
                    foreach (HtmlNode child in node.ChildNodes) MapInline(child, link.Inlines, document, style);
                    link.Inlines = Merge(link.Inlines);
                    if (link.Inlines.Count == 0) link.Inlines.Add(new TextInline(href, style));
                    target.Add(link);
                    return;
                case "input":
                    return;
            }

            InlineStyleEnum added = InlineStyleEnum.None;
            switch (name)
            {
                case "b":
                case "strong":
                    added = InlineStyleEnum.Bold;
                    break;
                case "i":
                case "em":
                case "cite":
                case "dfn":
                    added = InlineStyleEnum.Italic;
                    break;
                case "u":
                case "ins":
                    added = InlineStyleEnum.Underline;
                    break;
                case "s":
                case "strike":
                case "del":
                    added = InlineStyleEnum.Strikethrough;
                    break;
                case "code":
                case "kbd":
                case "samp":
                case "tt":
                    added = InlineStyleEnum.Code;
                    break;
                case "sup":
                    added = InlineStyleEnum.Superscript;
                    break;
                case "sub":
                    added = InlineStyleEnum.Subscript;
                    break;
            }

            bool blockLike = name == "p" || name == "div" || name == "li" || name == "tr" || name == "h1" || name == "h2" || name == "h3" || name == "h4" || name == "h5" || name == "h6";
            if (blockLike && target.Count > 0) target.Add(new LineBreakInline());
            foreach (HtmlNode child in node.ChildNodes) MapInline(child, target, document, style | added);
        }

        private static Inline MapImage(HtmlNode node, DocumentModel document)
        {
            string src = HtmlEntity.DeEntitize(node.GetAttributeValue("src", "")).Trim();
            string alt = HtmlEntity.DeEntitize(node.GetAttributeValue("alt", "")).Trim();
            BinaryResource? resource = DataUri.TryDecode(src);
            if (resource == null)
            {
                return new LinkInline(src, alt.Length > 0 ? alt : src);
            }

            string id = document.AddResource(resource);
            return new ImageInline(id, alt.Length > 0 ? alt : null);
        }

        private static bool IsBlockImage(HtmlNode node)
        {
            HtmlNode? parent = node.ParentNode;
            if (parent == null) return true;
            string p = parent.Name.ToLowerInvariant();
            if (p == "figure" || p == "body" || p == "#document") return true;
            if (!_Containers.Contains(p)) return false;
            foreach (HtmlNode sibling in parent.ChildNodes)
            {
                if (sibling == node) continue;
                if (sibling.NodeType == HtmlNodeType.Text && Collapse(sibling.InnerText ?? "").Length == 0) continue;
                if (sibling.NodeType == HtmlNodeType.Comment) continue;
                if (sibling.NodeType == HtmlNodeType.Element && (sibling.Name == "img" || sibling.Name == "figcaption" || _Containers.Contains(sibling.Name) || sibling.Name == "p")) continue;
                return false;
            }

            return true;
        }

        private static TextAlignmentEnum Alignment(HtmlNode node)
        {
            string align = node.GetAttributeValue("align", "").ToLowerInvariant();
            string style = node.GetAttributeValue("style", "").ToLowerInvariant().Replace(" ", "");
            if (align == "center" || style.Contains("text-align:center")) return TextAlignmentEnum.Center;
            if (align == "right" || style.Contains("text-align:right")) return TextAlignmentEnum.Right;
            if (align == "justify" || style.Contains("text-align:justify")) return TextAlignmentEnum.Justify;
            if (align == "left" || style.Contains("text-align:left")) return TextAlignmentEnum.Left;
            return TextAlignmentEnum.Default;
        }

        private static List<Inline> Merge(List<Inline> inlines)
        {
            List<Inline> merged = new List<Inline>();
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text && merged.Count > 0 && merged[merged.Count - 1] is TextInline previous && previous.Style == text.Style)
                {
                    string joined = previous.Text + text.Text;
                    previous.Text = joined.Replace("  ", " ");
                    continue;
                }

                merged.Add(inline);
            }

            return merged;
        }

        private static List<Inline> Trim(List<Inline> inlines)
        {
            while (inlines.Count > 0 && (inlines[0] is LineBreakInline || (inlines[0] is TextInline first && first.Text.Trim().Length == 0)))
                inlines.RemoveAt(0);
            while (inlines.Count > 0 && (inlines[inlines.Count - 1] is LineBreakInline || (inlines[inlines.Count - 1] is TextInline last && last.Text.Trim().Length == 0)))
                inlines.RemoveAt(inlines.Count - 1);
            if (inlines.Count > 0 && inlines[0] is TextInline head) head.Text = head.Text.TrimStart();
            if (inlines.Count > 0 && inlines[inlines.Count - 1] is TextInline tail) tail.Text = tail.Text.TrimEnd();
            return inlines;
        }

        private static string Collapse(string value)
        {
            return string.Join(" ", value.Split(new char[] { ' ', '\t', '\r', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string CollapseKeepEdges(string value)
        {
            if (value.Length == 0) return value;
            StringBuilder sb = new StringBuilder(value.Length);
            bool space = false;
            foreach (char c in value)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f')
                {
                    if (!space) sb.Append(' ');
                    space = true;
                    continue;
                }

                space = false;
                sb.Append(c == ' ' ? ' ' : c);
            }

            return sb.ToString();
        }

        private static int ParsePositive(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0) return parsed;
            return 1;
        }

        private static string? NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        #endregion
    }
}
