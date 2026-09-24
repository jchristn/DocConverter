namespace DocConverter.Writers.Html
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

    /// <summary>
    /// Writes HTML5, either a complete document or a body fragment (HtmlOptions.Mode). Every text node and attribute is
    /// encoded. Link URLs are allow-listed by scheme (http, https, mailto, relative, fragment); anything else is written as
    /// plain text with the LinkRemovedUnsafe warning. No script is ever emitted. Images follow HtmlOptions.ImageMode.
    /// Stateless and thread safe.
    /// </summary>
    public sealed class HtmlDocumentWriter : IDocumentWriter
    {
        #region Private-Members

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Html };

        private const string _Stylesheet =
            "body{font-family:-apple-system,Segoe UI,Helvetica,Arial,sans-serif;line-height:1.5;max-width:52rem;margin:2rem auto;padding:0 1rem;color:#1f2328}"
            + "table{border-collapse:collapse;margin:1rem 0}th,td{border:1px solid #d0d7de;padding:.3rem .6rem;vertical-align:top}th{background:#f6f8fa}"
            + "pre{background:#f6f8fa;padding:.8rem;overflow:auto}code{font-family:ui-monospace,Consolas,monospace}"
            + "blockquote{margin:0;padding:0 1rem;border-left:.25rem solid #d0d7de;color:#59636e}img{max-width:100%}"
            + ".docconverter-image{display:inline-block;border:1px dashed #8c959f;padding:.2rem .4rem;color:#59636e}"
            + ".docconverter-page-break{page-break-after:always;break-after:page}";

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
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            RenderState state = new RenderState(document, options, context, token);
            StringBuilder body = new StringBuilder();
            foreach (Block block in document.Blocks)
            {
                token.ThrowIfCancellationRequested();
                RenderBlock(block, body, state, 0);
            }

            string html;
            if (options.Html.Mode == HtmlOutputModeEnum.Fragment)
            {
                html = body.ToString();
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                DocumentMetadata m = document.Metadata;
                sb.Append("<!DOCTYPE html>\n<html");
                if (!string.IsNullOrEmpty(m.Language)) sb.Append(" lang=\"").Append(Attr(m.Language!)).Append('"');
                sb.Append(">\n<head>\n<meta charset=\"").Append(Attr(options.OutputEncoding.WebName)).Append("\">\n");
                sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
                sb.Append("<meta name=\"generator\" content=\"DocConverter\">\n");
                sb.Append("<title>").Append(Text(m.Title ?? FirstHeading(document) ?? "Document")).Append("</title>\n");
                if (!string.IsNullOrEmpty(m.Author)) sb.Append("<meta name=\"author\" content=\"").Append(Attr(m.Author!)).Append("\">\n");
                if (!string.IsNullOrEmpty(m.Description)) sb.Append("<meta name=\"description\" content=\"").Append(Attr(m.Description!)).Append("\">\n");
                if (!string.IsNullOrEmpty(m.Keywords)) sb.Append("<meta name=\"keywords\" content=\"").Append(Attr(m.Keywords!)).Append("\">\n");
                if (!string.IsNullOrEmpty(m.Subject)) sb.Append("<meta name=\"subject\" content=\"").Append(Attr(m.Subject!)).Append("\">\n");
                if (options.Html.IncludeStylesheet) sb.Append("<style>").Append(_Stylesheet).Append("</style>\n");
                sb.Append("</head>\n<body>\n").Append(body).Append("</body>\n</html>\n");
                html = sb.ToString();
            }

            return TextIO.WriteAllTextAsync(html, output, options, token);
        }

        #endregion

        #region Private-Methods

        private static void RenderBlock(Block block, StringBuilder sb, RenderState state, int sectionDepth)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    sb.Append("<h").Append(heading.Level).Append(IdAttr(heading)).Append('>');
                    RenderInlines(heading.Inlines, sb, state);
                    sb.Append("</h").Append(heading.Level).Append(">\n");
                    break;
                case ParagraphBlock paragraph:
                    sb.Append("<p").Append(IdAttr(paragraph)).Append(AlignAttr(paragraph.Alignment)).Append('>');
                    RenderInlines(paragraph.Inlines, sb, state);
                    sb.Append("</p>\n");
                    break;
                case ListBlock list:
                    RenderList(list, sb, state, sectionDepth);
                    break;
                case ListItemBlock item:
                    ListBlock wrapper = new ListBlock(ListKindEnum.Unordered);
                    wrapper.Items.Add(item);
                    RenderList(wrapper, sb, state, sectionDepth);
                    break;
                case TableBlock table:
                    RenderTable(table, sb, state, sectionDepth);
                    break;
                case CodeBlock code:
                    sb.Append("<pre><code");
                    if (!string.IsNullOrEmpty(code.Language)) sb.Append(" class=\"language-").Append(Attr(code.Language!)).Append('"');
                    sb.Append('>').Append(Text(code.Text)).Append("</code></pre>\n");
                    break;
                case QuoteBlock quote:
                    sb.Append("<blockquote>\n");
                    foreach (Block child in quote.Blocks) RenderBlock(child, sb, state, sectionDepth);
                    sb.Append("</blockquote>\n");
                    break;
                case ImageBlock image:
                    string rendered = RenderImage(image.ResourceId, image.AltText, state);
                    if (rendered.Length == 0) break;
                    if (!string.IsNullOrEmpty(image.Caption))
                        sb.Append("<figure>").Append(rendered).Append("<figcaption>").Append(Text(image.Caption!)).Append("</figcaption></figure>\n");
                    else
                        sb.Append("<p>").Append(rendered).Append("</p>\n");
                    break;
                case ThematicBreakBlock _:
                    sb.Append("<hr>\n");
                    break;
                case PageBreakBlock _:
                    sb.Append("<div class=\"docconverter-page-break\"></div>\n");
                    break;
                case SectionBlock section:
                    sb.Append("<section data-kind=\"").Append(section.Kind.ToString().ToLowerInvariant()).Append('"').Append(IdAttr(section)).Append(">\n");
                    if (SectionTitles.ShouldRender(section))
                    {
                        int level = Math.Min(6, 2 + sectionDepth);
                        sb.Append("<h").Append(level).Append('>').Append(Text(section.Title!)).Append("</h").Append(level).Append(">\n");
                    }

                    foreach (Block child in section.Blocks) RenderBlock(child, sb, state, sectionDepth + 1);
                    sb.Append("</section>\n");
                    break;
                default:
                    state.Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "A block of type " + block.GetType().Name + " has no HTML form and was skipped.");
                    break;
            }
        }

        private static void RenderList(ListBlock list, StringBuilder sb, RenderState state, int sectionDepth)
        {
            bool ordered = list.Kind == ListKindEnum.Ordered;
            sb.Append(ordered ? "<ol" : "<ul");
            if (ordered && list.Start != 1) sb.Append(" start=\"").Append(list.Start.ToString(CultureInfo.InvariantCulture)).Append('"');
            if (list.Kind == ListKindEnum.Task) sb.Append(" class=\"task-list\"");
            sb.Append(">\n");
            foreach (ListItemBlock item in list.Items)
            {
                sb.Append("<li>");
                if (list.Kind == ListKindEnum.Task || item.Checked.HasValue)
                    sb.Append(item.Checked == true ? "<input type=\"checkbox\" disabled checked> " : "<input type=\"checkbox\" disabled> ");

                bool tight = item.Blocks.Count > 0 && item.Blocks[0] is ParagraphBlock;
                for (int i = 0; i < item.Blocks.Count; i++)
                {
                    if (i == 0 && tight)
                    {
                        RenderInlines(((ParagraphBlock)item.Blocks[0]).Inlines, sb, state);
                        if (item.Blocks.Count > 1) sb.Append('\n');
                        continue;
                    }

                    RenderBlock(item.Blocks[i], sb, state, sectionDepth);
                }

                sb.Append("</li>\n");
            }

            sb.Append(ordered ? "</ol>\n" : "</ul>\n");
        }

        private static void RenderTable(TableBlock table, StringBuilder sb, RenderState state, int sectionDepth)
        {
            sb.Append("<table>\n");
            if (!string.IsNullOrEmpty(table.Caption)) sb.Append("<caption>").Append(Text(table.Caption!)).Append("</caption>\n");
            int header = Math.Min(table.HeaderRowCount, table.Rows.Count);
            if (header > 0) sb.Append("<thead>\n");
            for (int r = 0; r < table.Rows.Count; r++)
            {
                if (r == header) sb.Append(header > 0 ? "</thead>\n<tbody>\n" : "<tbody>\n");
                sb.Append("<tr>");
                int column = 0;
                foreach (TableCell cell in table.Rows[r].Cells)
                {
                    string tag = cell.IsHeader || r < header ? "th" : "td";
                    sb.Append('<').Append(tag);
                    if (cell.ColumnSpan > 1) sb.Append(" colspan=\"").Append(cell.ColumnSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                    if (cell.RowSpan > 1) sb.Append(" rowspan=\"").Append(cell.RowSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                    TextAlignmentEnum alignment = column < table.ColumnAlignments.Count ? table.ColumnAlignments[column] : TextAlignmentEnum.Default;
                    sb.Append(AlignAttr(alignment));
                    sb.Append('>');
                    if (cell.Blocks.Count == 1 && cell.Blocks[0] is ParagraphBlock only)
                    {
                        RenderInlines(only.Inlines, sb, state);
                    }
                    else
                    {
                        foreach (Block child in cell.Blocks) RenderBlock(child, sb, state, sectionDepth);
                    }

                    sb.Append("</").Append(tag).Append('>');
                    column += cell.ColumnSpan;
                }

                sb.Append("</tr>\n");
            }

            if (header == table.Rows.Count && header > 0) sb.Append("</thead>\n");
            else if (table.Rows.Count > 0) sb.Append("</tbody>\n");
            sb.Append("</table>\n");
        }

        private static void RenderInlines(List<Inline> inlines, StringBuilder sb, RenderState state)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        RenderText(text, sb);
                        break;
                    case LinkInline link:
                        if (!LinkSafety.IsSafe(link.Url))
                        {
                            state.Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link with a disallowed URL scheme was written as plain text.");
                            RenderInlines(link.Inlines, sb, state);
                            break;
                        }

                        sb.Append("<a href=\"").Append(Attr(link.Url)).Append('"');
                        if (!string.IsNullOrEmpty(link.Title)) sb.Append(" title=\"").Append(Attr(link.Title!)).Append('"');
                        sb.Append('>');
                        RenderInlines(link.Inlines, sb, state);
                        sb.Append("</a>");
                        break;
                    case ImageInline image:
                        sb.Append(RenderImage(image.ResourceId, image.AltText, state));
                        break;
                    case LineBreakInline _:
                        sb.Append("<br>\n");
                        break;
                }
            }
        }

        private static void RenderText(TextInline text, StringBuilder sb)
        {
            InlineStyleEnum s = text.Style;
            List<string> tags = new List<string>();
            if ((s & InlineStyleEnum.Bold) == InlineStyleEnum.Bold) tags.Add("strong");
            if ((s & InlineStyleEnum.Italic) == InlineStyleEnum.Italic) tags.Add("em");
            if ((s & InlineStyleEnum.Underline) == InlineStyleEnum.Underline) tags.Add("u");
            if ((s & InlineStyleEnum.Strikethrough) == InlineStyleEnum.Strikethrough) tags.Add("s");
            if ((s & InlineStyleEnum.Superscript) == InlineStyleEnum.Superscript) tags.Add("sup");
            if ((s & InlineStyleEnum.Subscript) == InlineStyleEnum.Subscript) tags.Add("sub");
            if ((s & InlineStyleEnum.Code) == InlineStyleEnum.Code) tags.Add("code");
            foreach (string tag in tags) sb.Append('<').Append(tag).Append('>');
            sb.Append(Text(text.Text));
            for (int i = tags.Count - 1; i >= 0; i--) sb.Append("</").Append(tags[i]).Append('>');
        }

        private static string RenderImage(string resourceId, string? altText, RenderState state)
        {
            ImageModeEnum mode = state.Options.Html.ImageMode;
            state.Document.Resources.TryGetValue(resourceId, out BinaryResource? resource);
            if (mode == ImageModeEnum.Omit)
            {
                state.Context.AddWarning(WarningCodeEnum.ImagesOmitted, "Images were omitted because HtmlOptions.ImageMode is Omit.");
                return "";
            }

            if (mode == ImageModeEnum.Placeholder || resource == null)
            {
                state.Context.AddWarning(WarningCodeEnum.ImagePlaceholderEmitted, "Images were written as text placeholders.");
                return "<span class=\"docconverter-image\">" + Text(ImagePlaceholder.Describe(altText, resourceId, state.Document.Resources)) + "</span>";
            }

            string src = mode == ImageModeEnum.External ? state.ExternalName(resource) : DataUri.Encode(resource);
            StringBuilder sb = new StringBuilder();
            sb.Append("<img src=\"").Append(Attr(src)).Append("\" alt=\"").Append(Attr(altText ?? "")).Append('"');
            if (resource.PixelWidth.HasValue && resource.PixelHeight.HasValue)
            {
                sb.Append(" width=\"").Append(resource.PixelWidth.Value.ToString(CultureInfo.InvariantCulture)).Append('"');
                sb.Append(" height=\"").Append(resource.PixelHeight.Value.ToString(CultureInfo.InvariantCulture)).Append('"');
            }

            sb.Append('>');
            return sb.ToString();
        }

        private static string? FirstHeading(DocumentModel document)
        {
            foreach (Block block in document.Blocks)
                if (block is HeadingBlock h) return ModelText.Inlines(h.Inlines);
            return null;
        }

        private static string IdAttr(Block block)
        {
            return string.IsNullOrEmpty(block.Id) ? "" : " id=\"" + Attr(block.Id!) + "\"";
        }

        private static string AlignAttr(TextAlignmentEnum alignment)
        {
            switch (alignment)
            {
                case TextAlignmentEnum.Left: return " style=\"text-align:left\"";
                case TextAlignmentEnum.Center: return " style=\"text-align:center\"";
                case TextAlignmentEnum.Right: return " style=\"text-align:right\"";
                case TextAlignmentEnum.Justify: return " style=\"text-align:justify\"";
                default: return "";
            }
        }

        private static string Text(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '\0': break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private static string Attr(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&#39;"); break;
                    case '\0': break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
