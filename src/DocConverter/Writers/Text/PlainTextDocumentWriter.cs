namespace DocConverter.Writers.Text
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
    /// Writes plain text. Every word of text is kept; inline styles are dropped (FormattingLost), links keep their URL in
    /// parentheses (TextOptions.IncludeLinkUrls), images become a bracketed placeholder line (ImagePlaceholderEmitted) or are
    /// omitted, tables are aligned columns or tab separated, and paragraphs can be hard wrapped. Stateless and thread safe.
    /// </summary>
    public sealed class PlainTextDocumentWriter : IDocumentWriter
    {
        #region Private-Members

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Text };

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

            string text = string.Join("\n\n", chunks);
            if (text.Length > 0) text += "\n";
            return TextIO.WriteAllTextAsync(text, output, options, token);
        }

        #endregion

        #region Private-Methods

        private static string RenderBlock(Block block, RenderState state, int sectionDepth)
        {
            TextOptions o = state.Options.Text;
            switch (block)
            {
                case HeadingBlock heading:
                    return Heading(RenderInlines(heading.Inlines, state).Replace("\n", " "), heading.Level, o.HeadingStyle);
                case ParagraphBlock paragraph:
                    return Wrap(RenderInlines(paragraph.Inlines, state), o.WrapColumn);
                case ListBlock list:
                    return RenderList(list, state, sectionDepth);
                case ListItemBlock item:
                    ListBlock wrapper = new ListBlock(ListKindEnum.Unordered);
                    wrapper.Items.Add(item);
                    return RenderList(wrapper, state, sectionDepth);
                case TableBlock table:
                    return RenderTable(table, state);
                case CodeBlock code:
                    return Indent(code.Text.TrimEnd('\n'), "    ", true);
                case QuoteBlock quote:
                    List<string> parts = new List<string>();
                    foreach (Block child in quote.Blocks)
                    {
                        string rendered = RenderBlock(child, state, sectionDepth);
                        if (rendered.Length > 0) parts.Add(rendered);
                    }

                    return Indent(string.Join("\n\n", parts), "> ", true);
                case ImageBlock image:
                    string placeholder = Image(image.ResourceId, image.AltText, state);
                    if (placeholder.Length > 0 && !string.IsNullOrEmpty(image.Caption)) placeholder += "\n" + image.Caption;
                    return placeholder;
                case ThematicBreakBlock _:
                    return "----------------------------------------";
                case PageBreakBlock _:
                    return "";
                case SectionBlock section:
                    List<string> sectionParts = new List<string>();
                    if (SectionTitles.ShouldRender(section)) sectionParts.Add(Heading(section.Title!, Math.Min(6, 2 + sectionDepth), o.HeadingStyle));
                    foreach (Block child in section.Blocks)
                    {
                        string rendered = RenderBlock(child, state, sectionDepth + 1);
                        if (rendered.Length > 0) sectionParts.Add(rendered);
                    }

                    return string.Join("\n\n", sectionParts);
                default:
                    return ModelText.Block(block);
            }
        }

        private static string Heading(string text, int level, TextHeadingStyleEnum style)
        {
            switch (style)
            {
                case TextHeadingStyleEnum.Uppercase:
                    return text.ToUpperInvariant();
                case TextHeadingStyleEnum.Underline:
                    if (level == 1) return text + "\n" + new string('=', Math.Max(3, text.Length));
                    if (level == 2) return text + "\n" + new string('-', Math.Max(3, text.Length));
                    return text;
                default:
                    return text;
            }
        }

        private static string RenderList(ListBlock list, RenderState state, int sectionDepth)
        {
            StringBuilder sb = new StringBuilder();
            int number = list.Start;
            bool first = true;
            foreach (ListItemBlock item in list.Items)
            {
                string marker = list.Kind == ListKindEnum.Ordered
                    ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". "
                    : "- ";
                if (list.Kind == ListKindEnum.Task || item.Checked.HasValue) marker += item.Checked == true ? "[x] " : "[ ] ";
                number++;

                List<string> parts = new List<string>();
                foreach (Block child in item.Blocks)
                {
                    string rendered = RenderBlock(child, state, sectionDepth);
                    if (rendered.Length > 0) parts.Add(rendered);
                }

                string body = string.Join("\n", parts);
                if (!first) sb.Append('\n');
                sb.Append(marker).Append(Indent(body, new string(' ', marker.Length), false));
                first = false;
            }

            return sb.ToString();
        }

        private static string RenderTable(TableBlock table, RenderState state)
        {
            if (table.Rows.Count == 0) return "";
            TextOptions o = state.Options.Text;
            TableGrid grid = TableGrid.Build(table, o.TableSpanMode, " ");
            if (grid.HadSpans) state.Context.AddWarning(WarningCodeEnum.TableSpansFlattened, "Merged table cells were " + (o.TableSpanMode == TableSpanModeEnum.Repeat ? "repeated" : "emptied") + " in plain text.");

            List<List<string>> rows = new List<List<string>>();
            foreach (List<string> row in grid.Rows)
            {
                List<string> clean = new List<string>();
                foreach (string cell in row) clean.Add(cell.Replace("\r", "").Replace("\n", " ").Replace("\t", " "));
                rows.Add(clean);
            }

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(table.Caption)) sb.Append(table.Caption).Append('\n');

            if (o.TableStyle == TextTableStyleEnum.Tabs)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    if (r > 0) sb.Append('\n');
                    sb.Append(string.Join("\t", rows[r]).TrimEnd());
                }

                return sb.ToString();
            }

            int[] widths = new int[grid.ColumnCount];
            foreach (List<string> row in rows)
                for (int c = 0; c < row.Count; c++)
                    if (row[c].Length > widths[c]) widths[c] = row[c].Length;

            int headerRows = Math.Max(0, Math.Min(table.HeaderRowCount, rows.Count));
            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0) sb.Append('\n');
                List<string> padded = new List<string>();
                for (int c = 0; c < rows[r].Count; c++) padded.Add(rows[r][c].PadRight(widths[c]));
                sb.Append(string.Join("  ", padded).TrimEnd());
                if (r == headerRows - 1)
                {
                    List<string> rule = new List<string>();
                    foreach (int w in widths) rule.Add(new string('-', Math.Max(1, w)));
                    sb.Append('\n').Append(string.Join("  ", rule));
                }
            }

            return sb.ToString();
        }

        private static string RenderInlines(List<Inline> inlines, RenderState state)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        if (text.Style != InlineStyleEnum.None)
                            state.Context.AddWarning(WarningCodeEnum.FormattingLost, "Inline styles (bold, italic and so on) have no plain text form and were dropped.");
                        sb.Append(text.Text);
                        break;
                    case LinkInline link:
                        string label = RenderInlines(link.Inlines, state);
                        sb.Append(label);
                        if (state.Options.Text.IncludeLinkUrls && link.Url.Length > 0 && !string.Equals(label.Trim(), link.Url, StringComparison.Ordinal))
                            sb.Append(" (").Append(link.Url).Append(')');
                        break;
                    case ImageInline image:
                        sb.Append(Image(image.ResourceId, image.AltText, state));
                        break;
                    case LineBreakInline _:
                        sb.Append('\n');
                        break;
                }
            }

            return sb.ToString();
        }

        private static string Image(string resourceId, string? altText, RenderState state)
        {
            if (!state.Options.Text.IncludeImagePlaceholders)
            {
                state.Context.AddWarning(WarningCodeEnum.ImagesOmitted, "Images were omitted because TextOptions.IncludeImagePlaceholders is false.");
                return "";
            }

            state.Context.AddWarning(WarningCodeEnum.ImagePlaceholderEmitted, "Images cannot be shown in plain text and were written as placeholders. No text was extracted from them (no OCR).");
            return ImagePlaceholder.Describe(altText, resourceId, state.Document.Resources);
        }

        private static string Wrap(string text, int column)
        {
            if (column <= 0) return text;
            StringBuilder sb = new StringBuilder();
            string[] lines = text.Split('\n');
            for (int l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append('\n');
                int lineLength = 0;
                foreach (string word in lines[l].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (lineLength > 0 && lineLength + 1 + word.Length > column)
                    {
                        sb.Append('\n');
                        lineLength = 0;
                    }
                    else if (lineLength > 0)
                    {
                        sb.Append(' ');
                        lineLength++;
                    }

                    sb.Append(word);
                    lineLength += word.Length;
                }
            }

            return sb.ToString();
        }

        private static string Indent(string text, string indent, bool firstLine)
        {
            string[] lines = text.Split('\n');
            for (int i = firstLine ? 0 : 1; i < lines.Length; i++)
                if (lines[i].Length > 0 || indent.Trim().Length > 0) lines[i] = (indent + lines[i]).TrimEnd();
            return string.Join("\n", lines);
        }

        #endregion
    }
}
