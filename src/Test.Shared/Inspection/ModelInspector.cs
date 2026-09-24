namespace Test.Shared.Inspection
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// Snapshots a document model with a walker written for the tests, independent of the library's internal helpers.
    /// </summary>
    public static class ModelInspector
    {
        /// <summary>
        /// Snapshot a document model.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot Inspect(DocumentModel document)
        {
            ContentSnapshot snapshot = new ContentSnapshot();
            snapshot.Title = document.Metadata.Title;
            StringBuilder all = new StringBuilder();
            Walk(document.Blocks, document, snapshot, all);
            snapshot.AllText = ContentSnapshot.Normalize(all.ToString());
            return snapshot;
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
                else if (inline is LineBreakInline) sb.Append(' ');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Every block in the document, depth first, including blocks inside sections, lists, quotes and table cells.
        /// </summary>
        /// <param name="blocks">Root blocks.</param>
        /// <returns>All blocks.</returns>
        public static List<Block> AllBlocks(IEnumerable<Block> blocks)
        {
            List<Block> result = new List<Block>();
            Collect(blocks, result);
            return result;
        }

        private static void Collect(IEnumerable<Block> blocks, List<Block> result)
        {
            foreach (Block block in blocks)
            {
                result.Add(block);
                if (block is SectionBlock s) Collect(s.Blocks, result);
                else if (block is QuoteBlock q) Collect(q.Blocks, result);
                else if (block is ListBlock l) foreach (ListItemBlock item in l.Items) { result.Add(item); Collect(item.Blocks, result); }
                else if (block is ListItemBlock li) Collect(li.Blocks, result);
                else if (block is TableBlock t) foreach (TableRow row in t.Rows) foreach (TableCell cell in row.Cells) Collect(cell.Blocks, result);
            }
        }

        private static void Walk(IEnumerable<Block> blocks, DocumentModel document, ContentSnapshot snapshot, StringBuilder all)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case HeadingBlock h:
                        string heading = Text(h.Inlines);
                        snapshot.Headings.Add(ContentSnapshot.Normalize(heading));
                        all.Append(' ').Append(heading);
                        Links(h.Inlines, document, snapshot);
                        break;
                    case ParagraphBlock p:
                        all.Append(' ').Append(Text(p.Inlines));
                        Links(p.Inlines, document, snapshot);
                        break;
                    case SectionBlock s:
                        if (s.Title != null) all.Append(' ').Append(s.Title);
                        Walk(s.Blocks, document, snapshot, all);
                        break;
                    case QuoteBlock q:
                        Walk(q.Blocks, document, snapshot, all);
                        break;
                    case ListBlock l:
                        foreach (ListItemBlock item in l.Items)
                        {
                            if (item.Blocks.Count > 0 && item.Blocks[0] is ParagraphBlock first) snapshot.ListItems.Add(ContentSnapshot.Normalize(Text(first.Inlines)));
                            Walk(item.Blocks, document, snapshot, all);
                        }

                        break;
                    case TableBlock t:
                        foreach (TableRow row in t.Rows)
                        {
                            List<string> cells = new List<string>();
                            foreach (TableCell cell in row.Cells)
                            {
                                StringBuilder cellText = new StringBuilder();
                                Walk(cell.Blocks, document, new ContentSnapshot(), cellText);
                                cells.Add(ContentSnapshot.Normalize(cellText.ToString()));
                                all.Append(' ').Append(cellText);
                            }

                            snapshot.TableRows.Add(cells);
                        }

                        break;
                    case CodeBlock c:
                        all.Append(' ').Append(c.Text);
                        break;
                    case ImageBlock i:
                        if (document.Resources.ContainsKey(i.ResourceId)) snapshot.ImageCount++;
                        break;
                }
            }
        }

        private static void Links(IEnumerable<Inline> inlines, DocumentModel document, ContentSnapshot snapshot)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is LinkInline link)
                {
                    snapshot.LinkUrls.Add(link.Url);
                    Links(link.Inlines, document, snapshot);
                }
                else if (inline is ImageInline image && document.Resources.ContainsKey(image.ResourceId))
                {
                    snapshot.ImageCount++;
                }
            }
        }
    }
}
