namespace DocConverter.Writers.Markdown
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Writes GitHub flavored Markdown: ATX headings, "-" and "1." lists with nesting, pipe tables, fenced code, quotes and
    /// thematic breaks. Text is escaped so it reads back as the same text. Underline has no Markdown form and is dropped with
    /// the FormattingLost warning; merged table cells are repeated or emptied per MarkdownOptions.TableSpanMode. Images
    /// follow MarkdownOptions.ImageMode. Links with unsafe schemes are written as plain text. Stateless and thread safe.
    /// </summary>
    public sealed class MarkdownDocumentWriter : IDocumentWriter
    {
        #region Private-Members

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Markdown };

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
            List<string> chunks = new List<string>();
            foreach (Block block in document.Blocks)
            {
                token.ThrowIfCancellationRequested();
                string rendered = RenderBlock(block, state, 0);
                if (rendered.Length > 0) chunks.Add(rendered);
            }

            string markdown = string.Join("\n\n", chunks);
            if (markdown.Length > 0) markdown += "\n";
            return TextIO.WriteAllTextAsync(markdown, output, options, token);
        }

        #endregion

        #region Private-Methods

        private static string RenderBlock(Block block, RenderState state, int sectionDepth)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    string headingText = RenderInlines(heading.Inlines, state).Replace("\n", " ");
                    return new string('#', heading.Level) + " " + headingText;
                case ParagraphBlock paragraph:
                    return EscapeLineStarts(RenderInlines(paragraph.Inlines, state));
                case ListBlock list:
                    return RenderList(list, state, sectionDepth);
                case ListItemBlock item:
                    ListBlock wrapper = new ListBlock(ListKindEnum.Unordered);
                    wrapper.Items.Add(item);
                    return RenderList(wrapper, state, sectionDepth);
                case TableBlock table:
                    return RenderTable(table, state);
                case CodeBlock code:
                    string fence = Fence(code.Text, '`');
                    return fence + (code.Language ?? "") + "\n" + code.Text.TrimEnd('\n') + "\n" + fence;
                case QuoteBlock quote:
                    List<string> parts = new List<string>();
                    foreach (Block child in quote.Blocks)
                    {
                        string rendered = RenderBlock(child, state, sectionDepth);
                        if (rendered.Length > 0) parts.Add(rendered);
                    }

                    return Prefix(string.Join("\n\n", parts), "> ", ">");
                case ImageBlock image:
                    string imageText = RenderImage(image.ResourceId, image.AltText, state);
                    if (!string.IsNullOrEmpty(image.Caption) && imageText.Length > 0) imageText += "\n\n*" + EscapeText(image.Caption!) + "*";
                    return imageText;
                case ThematicBreakBlock _:
                    return "---";
                case PageBreakBlock _:
                    return "";
                case SectionBlock section:
                    List<string> sectionParts = new List<string>();
                    if (!string.IsNullOrEmpty(section.Title))
                    {
                        int level = Math.Min(6, 2 + sectionDepth);
                        sectionParts.Add(new string('#', level) + " " + EscapeText(section.Title!));
                    }

                    foreach (Block child in section.Blocks)
                    {
                        string rendered = RenderBlock(child, state, sectionDepth + 1);
                        if (rendered.Length > 0) sectionParts.Add(rendered);
                    }

                    return string.Join("\n\n", sectionParts);
                default:
                    state.Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "A block of type " + block.GetType().Name + " has no Markdown form and was skipped.");
                    return "";
            }
        }

        private static string RenderList(ListBlock list, RenderState state, int sectionDepth)
        {
            StringBuilder sb = new StringBuilder();
            int number = list.Start;
            bool first = true;
            bool loose = false;
            foreach (ListItemBlock item in list.Items)
                if (item.Blocks.Count > 1 && !(item.Blocks.Count == 2 && item.Blocks[1] is ListBlock)) loose = true;

            foreach (ListItemBlock item in list.Items)
            {
                string marker;
                if (list.Kind == ListKindEnum.Ordered) marker = number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". ";
                else marker = "- ";
                if (list.Kind == ListKindEnum.Task || item.Checked.HasValue) marker += item.Checked == true ? "[x] " : "[ ] ";
                number++;

                string indent = new string(' ', list.Kind == ListKindEnum.Ordered ? marker.Length : 2);
                List<string> parts = new List<string>();
                foreach (Block child in item.Blocks)
                {
                    string rendered = RenderBlock(child, state, sectionDepth);
                    if (rendered.Length > 0) parts.Add(rendered);
                }

                string body = parts.Count == 0 ? "" : parts[0];
                for (int i = 1; i < parts.Count; i++)
                {
                    bool nestedList = item.Blocks.Count > i && item.Blocks[i] is ListBlock;
                    body += (nestedList && !loose ? "\n" : "\n\n") + parts[i];
                }

                if (!first) sb.Append(loose ? "\n\n" : "\n");
                sb.Append(marker).Append(Indent(body, indent));
                first = false;
            }

            return sb.ToString();
        }

        private static string RenderTable(TableBlock table, RenderState state)
        {
            if (table.Rows.Count == 0) return "";
            TableGrid grid = TableGrid.Build(table, state.Options.Markdown.TableSpanMode, " ");
            if (grid.HadSpans) state.Context.AddWarning(WarningCodeEnum.TableSpansFlattened, "Merged table cells were " + (state.Options.Markdown.TableSpanMode == TableSpanModeEnum.Repeat ? "repeated" : "emptied") + " because Markdown tables cannot merge cells.");

            List<List<string>> cells = new List<List<string>>();
            for (int r = 0; r < table.Rows.Count; r++)
            {
                List<string> row = new List<string>();
                TableRow source = table.Rows[r];
                for (int c = 0; c < grid.ColumnCount; c++)
                {
                    // Without spans the grid lines up with the source cells, so cells keep their inline formatting.
                    string text = !grid.HadSpans && c < source.Cells.Count
                        ? RenderCell(source.Cells[c], state)
                        : EscapeText(grid.Rows[r][c]);
                    row.Add(text.Replace("\n", "<br>"));
                }

                cells.Add(row);
            }

            StringBuilder sb = new StringBuilder();
            int headerIndex = 0;
            sb.Append("| ").Append(string.Join(" | ", cells[headerIndex])).Append(" |\n");
            List<string> separator = new List<string>();
            for (int c = 0; c < grid.ColumnCount; c++)
            {
                TextAlignmentEnum alignment = c < table.ColumnAlignments.Count ? table.ColumnAlignments[c] : TextAlignmentEnum.Default;
                if (alignment == TextAlignmentEnum.Left) separator.Add(":---");
                else if (alignment == TextAlignmentEnum.Center) separator.Add(":---:");
                else if (alignment == TextAlignmentEnum.Right) separator.Add("---:");
                else separator.Add("---");
            }

            sb.Append("| ").Append(string.Join(" | ", separator)).Append(" |");
            for (int r = 1; r < cells.Count; r++) sb.Append("\n| ").Append(string.Join(" | ", cells[r])).Append(" |");
            if (!string.IsNullOrEmpty(table.Caption)) sb.Append("\n\n*").Append(EscapeText(table.Caption!)).Append('*');
            return sb.ToString();
        }

        private static string RenderCell(TableCell cell, RenderState state)
        {
            List<string> parts = new List<string>();
            foreach (Block block in cell.Blocks)
            {
                if (block is ParagraphBlock p) parts.Add(RenderInlines(p.Inlines, state));
                else if (block is HeadingBlock h) parts.Add("**" + RenderInlines(h.Inlines, state) + "**");
                else if (block is ImageBlock image) parts.Add(RenderImage(image.ResourceId, image.AltText, state));
                else
                {
                    state.Context.AddWarning(WarningCodeEnum.FormattingLost, "Block content inside table cells (lists, code, quotes, nested tables) was flattened to text because Markdown table cells hold inline content only.");
                    parts.Add(EscapeText(ModelText.Block(block).Replace("\n", " ")));
                }
            }

            return string.Join("<br>", parts);
        }

        private static string RenderInlines(List<Inline> inlines, RenderState state)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Inline inline in inlines) sb.Append(RenderInline(inline, state));
            return sb.ToString();
        }

        private static string RenderInline(Inline inline, RenderState state)
        {
            switch (inline)
            {
                case TextInline text:
                    return RenderText(text, state);
                case LinkInline link:
                    string label = RenderInlines(link.Inlines, state);
                    if (!LinkSafety.IsSafe(link.Url))
                    {
                        state.Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link with a disallowed URL scheme was written as plain text.");
                        return label;
                    }

                    if (label.Length == 0) label = EscapeText(link.Url);
                    string title = string.IsNullOrEmpty(link.Title) ? "" : " \"" + link.Title!.Replace("\"", "\\\"") + "\"";
                    return "[" + label + "](" + EscapeUrl(link.Url) + title + ")";
                case ImageInline image:
                    return RenderImage(image.ResourceId, image.AltText, state);
                case LineBreakInline _:
                    return "\\\n";
                default:
                    return "";
            }
        }

        private static string RenderText(TextInline text, RenderState state)
        {
            if (text.Text.Length == 0) return "";
            InlineStyleEnum style = text.Style;
            if ((style & InlineStyleEnum.Code) == InlineStyleEnum.Code)
            {
                string content = text.Text.Replace("\n", " ");
                string ticks = Fence(content, '`', 1);
                string pad = content.StartsWith("`", StringComparison.Ordinal) || content.EndsWith("`", StringComparison.Ordinal) ? " " : "";
                string code = ticks + pad + content + pad + ticks;
                return Wrap(code, style & ~InlineStyleEnum.Code, state);
            }

            return Wrap(EscapeText(text.Text), style, state);
        }

        private static string Wrap(string content, InlineStyleEnum style, RenderState state)
        {
            if (style == InlineStyleEnum.None || content.Trim().Length == 0) return content;

            int leadLength = content.Length - content.TrimStart().Length;
            int trailLength = content.Length - content.TrimEnd().Length;
            string lead = content.Substring(0, leadLength);
            string trail = content.Substring(content.Length - trailLength);
            string core = content.Trim();

            if ((style & InlineStyleEnum.Underline) == InlineStyleEnum.Underline)
                state.Context.AddWarning(WarningCodeEnum.FormattingLost, "Underline has no Markdown form and was dropped.");
            if ((style & InlineStyleEnum.Superscript) == InlineStyleEnum.Superscript) core = "^" + core + "^";
            if ((style & InlineStyleEnum.Subscript) == InlineStyleEnum.Subscript) core = "~" + core + "~";
            if ((style & InlineStyleEnum.Strikethrough) == InlineStyleEnum.Strikethrough) core = "~~" + core + "~~";
            if ((style & InlineStyleEnum.Italic) == InlineStyleEnum.Italic) core = "*" + core + "*";
            if ((style & InlineStyleEnum.Bold) == InlineStyleEnum.Bold) core = "**" + core + "**";
            return lead + core + trail;
        }

        private static string RenderImage(string resourceId, string? altText, RenderState state)
        {
            ImageModeEnum mode = state.Options.Markdown.ImageMode;
            state.Document.Resources.TryGetValue(resourceId, out BinaryResource? resource);
            string alt = EscapeText(altText ?? "").Replace("]", "\\]");

            if (mode == ImageModeEnum.Omit)
            {
                state.Context.AddWarning(WarningCodeEnum.ImagesOmitted, "Images were omitted because MarkdownOptions.ImageMode is Omit.");
                return "";
            }

            if (mode == ImageModeEnum.Placeholder || resource == null)
            {
                state.Context.AddWarning(WarningCodeEnum.ImagePlaceholderEmitted, "Images were written as text placeholders.");
                return EscapeText(ImagePlaceholder.Describe(altText, resourceId, state.Document.Resources));
            }

            if (mode == ImageModeEnum.External)
            {
                string fileName = state.ExternalName(resource);
                return "![" + alt + "](" + EscapeUrl(fileName) + ")";
            }

            return "![" + alt + "](" + DataUri.Encode(resource) + ")";
        }

        private static string EscapeText(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '\\':
                    case '`':
                    case '*':
                    case '_':
                    case '[':
                    case ']':
                    case '~':
                    case '^':
                    case '|':
                        sb.Append('\\').Append(c);
                        break;
                    case '<':
                        // Only a following letter, slash, '!' or '?' could open an HTML tag or autolink.
                        char next = i + 1 < text.Length ? text[i + 1] : ' ';
                        if (char.IsLetter(next) || next == '/' || next == '!' || next == '?') sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '&':
                        // Only text that looks like an entity reference needs protecting.
                        if (LooksLikeEntity(text, i)) sb.Append('\\');
                        sb.Append(c);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static bool LooksLikeEntity(string text, int ampersand)
        {
            int i = ampersand + 1;
            int start = i;
            while (i < text.Length && i - start < 32 && (char.IsLetterOrDigit(text[i]) || text[i] == '#')) i++;
            return i > start && i < text.Length && text[i] == ';';
        }

        private static string EscapeLineStarts(string text)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                if (trimmed.Length == 0) continue;
                int lead = line.Length - trimmed.Length;
                char c = trimmed[0];
                if (c == '#' || c == '-' || c == '+' || c == '=')
                {
                    lines[i] = line.Substring(0, lead) + "\\" + trimmed;
                    continue;
                }

                int digits = 0;
                while (digits < trimmed.Length && char.IsDigit(trimmed[digits])) digits++;
                if (digits > 0 && digits < trimmed.Length && (trimmed[digits] == '.' || trimmed[digits] == ')'))
                    lines[i] = line.Substring(0, lead) + trimmed.Substring(0, digits) + "\\" + trimmed.Substring(digits);
            }

            return string.Join("\n", lines);
        }

        private static string EscapeUrl(string url)
        {
            if (url.IndexOf(' ') >= 0 || url.IndexOf('(') >= 0 || url.IndexOf(')') >= 0)
                return "<" + url.Replace("<", "%3C").Replace(">", "%3E") + ">";
            return url;
        }

        private static string Fence(string content, char c, int minimum = 3)
        {
            int longest = 0;
            int run = 0;
            foreach (char ch in content)
            {
                if (ch == c)
                {
                    run++;
                    if (run > longest) longest = run;
                }
                else
                {
                    run = 0;
                }
            }

            return new string(c, Math.Max(minimum, longest + 1));
        }

        private static string Indent(string text, string indent)
        {
            string[] lines = text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].Length > 0) lines[i] = indent + lines[i];
            return string.Join("\n", lines);
        }

        private static string Prefix(string text, string prefix, string emptyPrefix)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].Length == 0 ? emptyPrefix : prefix + lines[i];
            return string.Join("\n", lines);
        }

        #endregion
    }
}
