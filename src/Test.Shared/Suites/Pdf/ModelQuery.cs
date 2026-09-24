namespace Test.Shared.Suites.Pdf
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Results;
    using Test.Shared.Inspection;

    /// <summary>
    /// Queries over a document model for the format suites.
    /// </summary>
    public static class ModelQuery
    {
        /// <summary>
        /// Every block in the document, depth first, including blocks nested in sections, quotes, list items and cells.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Blocks.</returns>
        public static List<Block> AllBlocks(DocumentModel document)
        {
            List<Block> list = new List<Block>();
            Walk(document.Blocks, list);
            return list;
        }

        /// <summary>
        /// All text in the document, whitespace normalized.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Text.</returns>
        public static string AllText(DocumentModel document)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block block in document.Blocks) sb.Append(DocConverter.Internal.ModelText.Block(block)).Append('\n');
            return ContentSnapshot.Normalize(sb.ToString());
        }

        /// <summary>
        /// Plain text of inlines.
        /// </summary>
        /// <param name="inlines">Inlines.</param>
        /// <returns>Text.</returns>
        public static string Text(List<Inline> inlines)
        {
            return ContentSnapshot.Normalize(DocConverter.Internal.ModelText.Inlines(inlines));
        }

        /// <summary>
        /// Blocks of a type.
        /// </summary>
        /// <typeparam name="T">Block type.</typeparam>
        /// <param name="document">Document.</param>
        /// <returns>Blocks.</returns>
        public static List<T> Of<T>(DocumentModel document) where T : Block
        {
            return AllBlocks(document).OfType<T>().ToList();
        }

        /// <summary>
        /// Every text inline (including inside links) with its style.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Text inlines.</returns>
        public static List<TextInline> TextInlines(DocumentModel document)
        {
            List<TextInline> list = new List<TextInline>();
            foreach (Block block in AllBlocks(document))
            {
                if (block is ParagraphBlock p) CollectText(p.Inlines, list);
                else if (block is HeadingBlock h) CollectText(h.Inlines, list);
            }

            return list;
        }

        /// <summary>
        /// Every link inline.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Links.</returns>
        public static List<LinkInline> Links(DocumentModel document)
        {
            List<LinkInline> list = new List<LinkInline>();
            foreach (Block block in AllBlocks(document))
            {
                List<Inline>? inlines = block is ParagraphBlock p ? p.Inlines : (block is HeadingBlock h ? h.Inlines : null);
                if (inlines == null) continue;
                foreach (Inline inline in inlines) if (inline is LinkInline link) list.Add(link);
            }

            return list;
        }

        /// <summary>
        /// Cell texts of a table, row by row.
        /// </summary>
        /// <param name="table">Table.</param>
        /// <returns>Rows of cell text.</returns>
        public static List<List<string>> Cells(TableBlock table)
        {
            List<List<string>> rows = new List<List<string>>();
            foreach (TableRow row in table.Rows)
            {
                List<string> cells = new List<string>();
                foreach (TableCell cell in row.Cells) cells.Add(ContentSnapshot.Normalize(DocConverter.Internal.ModelText.Blocks(cell.Blocks, " ")));
                rows.Add(cells);
            }

            return rows;
        }

        /// <summary>
        /// True when the result carries a warning with the code.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <param name="code">Code.</param>
        /// <returns>True when present.</returns>
        public static bool HasWarning(ConversionResult result, WarningCodeEnum code)
        {
            foreach (ConversionWarning w in result.Warnings) if (w.Code == code) return true;
            return false;
        }

        /// <summary>
        /// The warning with the code, or null.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <param name="code">Code.</param>
        /// <returns>Warning.</returns>
        public static ConversionWarning? Warning(ConversionResult result, WarningCodeEnum code)
        {
            foreach (ConversionWarning w in result.Warnings) if (w.Code == code) return w;
            return null;
        }

        private static void CollectText(List<Inline> inlines, List<TextInline> list)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline t) list.Add(t);
                else if (inline is LinkInline l) CollectText(l.Inlines, list);
            }
        }

        private static void Walk(List<Block> blocks, List<Block> list)
        {
            foreach (Block block in blocks)
            {
                list.Add(block);
                switch (block)
                {
                    case SectionBlock s:
                        Walk(s.Blocks, list);
                        break;
                    case QuoteBlock q:
                        Walk(q.Blocks, list);
                        break;
                    case ListBlock l:
                        foreach (ListItemBlock item in l.Items)
                        {
                            list.Add(item);
                            Walk(item.Blocks, list);
                        }

                        break;
                    case TableBlock t:
                        foreach (TableRow row in t.Rows)
                            foreach (TableCell cell in row.Cells) Walk(cell.Blocks, list);
                        break;
                }
            }
        }
    }
}
