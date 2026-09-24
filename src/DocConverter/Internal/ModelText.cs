namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// Flattens model content to plain text.
    /// </summary>
    internal static class ModelText
    {
        internal static string Inlines(IEnumerable<Inline> inlines)
        {
            StringBuilder sb = new StringBuilder();
            AppendInlines(sb, inlines);
            return sb.ToString();
        }

        internal static void AppendInlines(StringBuilder sb, IEnumerable<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text) sb.Append(text.Text);
                else if (inline is LinkInline link) AppendInlines(sb, link.Inlines);
                else if (inline is LineBreakInline) sb.Append('\n');
                else if (inline is ImageInline image && !string.IsNullOrEmpty(image.AltText)) sb.Append(image.AltText);
            }
        }

        /// <summary>
        /// Plain text of a block, with child blocks separated by newlines.
        /// </summary>
        internal static string Block(Block block)
        {
            StringBuilder sb = new StringBuilder();
            AppendBlock(sb, block);
            return sb.ToString().Trim('\n');
        }

        /// <summary>
        /// Plain text of a list of blocks (for example a table cell), joined by single spaces when inline is requested.
        /// </summary>
        internal static string Blocks(IEnumerable<Block> blocks, string separator)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (Block block in blocks)
            {
                string text = Block(block);
                if (text.Length == 0) continue;
                if (!first) sb.Append(separator);
                sb.Append(text);
                first = false;
            }

            return sb.ToString();
        }

        private static void AppendBlock(StringBuilder sb, Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    AppendInlines(sb, heading.Inlines);
                    sb.Append('\n');
                    break;
                case ParagraphBlock paragraph:
                    AppendInlines(sb, paragraph.Inlines);
                    sb.Append('\n');
                    break;
                case CodeBlock code:
                    sb.Append(code.Text).Append('\n');
                    break;
                case ListBlock list:
                    foreach (ListItemBlock item in list.Items) AppendBlock(sb, item);
                    break;
                case ListItemBlock item:
                    foreach (Block child in item.Blocks) AppendBlock(sb, child);
                    break;
                case QuoteBlock quote:
                    foreach (Block child in quote.Blocks) AppendBlock(sb, child);
                    break;
                case SectionBlock section:
                    if (!string.IsNullOrEmpty(section.Title)) sb.Append(section.Title).Append('\n');
                    foreach (Block child in section.Blocks) AppendBlock(sb, child);
                    break;
                case TableBlock table:
                    foreach (TableRow row in table.Rows)
                    {
                        List<string> cells = new List<string>();
                        foreach (TableCell cell in row.Cells) cells.Add(Blocks(cell.Blocks, " "));
                        sb.Append(string.Join("\t", cells)).Append('\n');
                    }

                    break;
                case ImageBlock image:
                    if (!string.IsNullOrEmpty(image.AltText)) sb.Append(image.AltText).Append('\n');
                    break;
            }
        }
    }
}
