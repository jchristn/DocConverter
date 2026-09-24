namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// Checks document model invariants: every resource id resolves, heading levels are 1 to 6, and nothing is null.
    /// Returns the list of problems found; an empty list means the model is valid.
    /// </summary>
    internal static class DocumentModelValidator
    {
        internal static List<string> Validate(DocumentModel document)
        {
            List<string> problems = new List<string>();
            if (document == null)
            {
                problems.Add("document is null");
                return problems;
            }

            foreach (KeyValuePair<string, BinaryResource> pair in document.Resources)
            {
                if (pair.Value == null) problems.Add("resource '" + pair.Key + "' is null");
                else if (pair.Value.Id != pair.Key) problems.Add("resource key '" + pair.Key + "' does not match its id '" + pair.Value.Id + "'");
            }

            WalkBlocks(document, document.Blocks, problems, "blocks");
            return problems;
        }

        private static void WalkBlocks(DocumentModel document, List<Block> blocks, List<string> problems, string path)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];
                string here = path + "[" + i + "]";
                if (block == null)
                {
                    problems.Add(here + " is null");
                    continue;
                }

                switch (block)
                {
                    case HeadingBlock heading:
                        if (heading.Level < 1 || heading.Level > 6) problems.Add(here + " heading level " + heading.Level + " is outside 1..6");
                        WalkInlines(document, heading.Inlines, problems, here);
                        break;
                    case ParagraphBlock paragraph:
                        WalkInlines(document, paragraph.Inlines, problems, here);
                        break;
                    case SectionBlock section:
                        WalkBlocks(document, section.Blocks, problems, here + ".blocks");
                        break;
                    case ListBlock list:
                        for (int j = 0; j < list.Items.Count; j++)
                        {
                            if (list.Items[j] == null) problems.Add(here + ".items[" + j + "] is null");
                            else WalkBlocks(document, list.Items[j].Blocks, problems, here + ".items[" + j + "]");
                        }

                        break;
                    case ListItemBlock item:
                        WalkBlocks(document, item.Blocks, problems, here + ".blocks");
                        break;
                    case QuoteBlock quote:
                        WalkBlocks(document, quote.Blocks, problems, here + ".blocks");
                        break;
                    case TableBlock table:
                        for (int r = 0; r < table.Rows.Count; r++)
                        {
                            if (table.Rows[r] == null)
                            {
                                problems.Add(here + ".rows[" + r + "] is null");
                                continue;
                            }

                            for (int c = 0; c < table.Rows[r].Cells.Count; c++)
                            {
                                TableCell cell = table.Rows[r].Cells[c];
                                if (cell == null) problems.Add(here + ".rows[" + r + "].cells[" + c + "] is null");
                                else WalkBlocks(document, cell.Blocks, problems, here + ".rows[" + r + "].cells[" + c + "]");
                            }
                        }

                        break;
                    case ImageBlock image:
                        if (!document.Resources.ContainsKey(image.ResourceId)) problems.Add(here + " references missing resource '" + image.ResourceId + "'");
                        break;
                }
            }
        }

        private static void WalkInlines(DocumentModel document, List<Inline> inlines, List<string> problems, string path)
        {
            for (int i = 0; i < inlines.Count; i++)
            {
                Inline inline = inlines[i];
                if (inline == null)
                {
                    problems.Add(path + ".inlines[" + i + "] is null");
                    continue;
                }

                if (inline is LinkInline link) WalkInlines(document, link.Inlines, problems, path + ".inlines[" + i + "]");
                else if (inline is ImageInline image && !document.Resources.ContainsKey(image.ResourceId))
                    problems.Add(path + ".inlines[" + i + "] references missing resource '" + image.ResourceId + "'");
            }
        }
    }
}
