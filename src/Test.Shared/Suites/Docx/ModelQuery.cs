namespace Test.Shared.Suites.Docx
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Model;

    /// <summary>
    /// Queries over a document model used by the DOCX suite.
    /// </summary>
    public static class ModelQuery
    {
        /// <summary>
        /// Every block of type T anywhere in the tree, in document order.
        /// </summary>
        /// <typeparam name="T">Block type.</typeparam>
        /// <param name="blocks">Root blocks.</param>
        /// <returns>Matching blocks.</returns>
        public static List<T> All<T>(IEnumerable<Block> blocks) where T : Block
        {
            List<T> found = new List<T>();
            Walk(blocks, found);
            return found;
        }

        /// <summary>
        /// Plain text of inlines.
        /// </summary>
        /// <param name="inlines">Inlines.</param>
        /// <returns>Text.</returns>
        public static string Text(IEnumerable<Inline> inlines)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline t) sb.Append(t.Text);
                else if (inline is LinkInline l) sb.Append(Text(l.Inlines));
                else if (inline is LineBreakInline) sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Plain text of a block (headings, paragraphs, first paragraph of list items, cells joined by spaces).
        /// </summary>
        /// <param name="block">Block.</param>
        /// <returns>Text.</returns>
        public static string Text(Block block)
        {
            switch (block)
            {
                case HeadingBlock h: return Text(h.Inlines);
                case ParagraphBlock p: return Text(p.Inlines);
                case CodeBlock c: return c.Text;
                case ListItemBlock li:
                    StringBuilder item = new StringBuilder();
                    foreach (Block b in li.Blocks) if (b is ParagraphBlock) item.Append(Text(b));
                    return item.ToString();
                case QuoteBlock _:
                case SectionBlock _:
                case TableBlock _:
                case ListBlock _:
                    StringBuilder sb = new StringBuilder();
                    foreach (ParagraphBlock p in All<ParagraphBlock>(new Block[] { block })) sb.Append(Text(p.Inlines)).Append(' ');
                    foreach (HeadingBlock h in All<HeadingBlock>(new Block[] { block })) sb.Append(Text(h.Inlines)).Append(' ');
                    return sb.ToString().Trim();
                default: return "";
            }
        }

        /// <summary>
        /// Text of a table cell.
        /// </summary>
        /// <param name="cell">Cell.</param>
        /// <returns>Text.</returns>
        public static string CellText(TableCell cell)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block b in cell.Blocks) sb.Append(Text(b));
            return sb.ToString();
        }

        /// <summary>
        /// Every text inline with the given style flag, anywhere in the document.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <param name="style">Style flag.</param>
        /// <returns>Texts.</returns>
        public static List<string> StyledTexts(DocumentModel document, InlineStyleEnum style)
        {
            List<string> result = new List<string>();
            foreach (ParagraphBlock p in All<ParagraphBlock>(document.Blocks)) CollectStyled(p.Inlines, style, result);
            foreach (HeadingBlock h in All<HeadingBlock>(document.Blocks)) CollectStyled(h.Inlines, style, result);
            return result;
        }

        /// <summary>
        /// Every link anywhere in the document.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Links.</returns>
        public static List<LinkInline> Links(DocumentModel document)
        {
            List<LinkInline> result = new List<LinkInline>();
            foreach (ParagraphBlock p in All<ParagraphBlock>(document.Blocks))
                foreach (Inline i in p.Inlines) if (i is LinkInline l) result.Add(l);
            return result;
        }

        private static void CollectStyled(IEnumerable<Inline> inlines, InlineStyleEnum style, List<string> result)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline t && (t.Style & style) == style) result.Add(t.Text);
                else if (inline is LinkInline l) CollectStyled(l.Inlines, style, result);
            }
        }

        private static void Walk<T>(IEnumerable<Block> blocks, List<T> found) where T : Block
        {
            foreach (Block block in blocks)
            {
                if (block is T match) found.Add(match);
                switch (block)
                {
                    case SectionBlock s: Walk(s.Blocks, found); break;
                    case QuoteBlock q: Walk(q.Blocks, found); break;
                    case ListBlock l:
                        foreach (ListItemBlock item in l.Items)
                        {
                            if (item is T itemMatch) found.Add(itemMatch);
                            Walk(item.Blocks, found);
                        }

                        break;
                    case TableBlock t:
                        foreach (TableRow row in t.Rows)
                            foreach (TableCell cell in row.Cells) Walk(cell.Blocks, found);
                        break;
                }
            }
        }
    }
}
