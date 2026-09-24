namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Option driven changes the pipeline applies to the model between reading and writing, so every writer honors
    /// IncludeImages, IncludeMetadata and Title the same way.
    /// </summary>
    internal static class ModelTransforms
    {
        internal static void Apply(DocumentModel document, ConversionOptions options, ConversionContext context)
        {
            ClearRedundantBold(document.Blocks);
            if (!options.IncludeMetadata) document.Metadata = new DocumentMetadata();
            if (options.Title != null) document.Metadata.Title = options.Title;

            if (!options.IncludeImages)
            {
                int removed = RemoveImages(document.Blocks);
                if (removed > 0)
                    context.AddWarning(WarningCodeEnum.ImagesOmitted, removed + " image(s) were omitted because IncludeImages is false.");
                document.Resources.Clear();
            }
        }

        // Headings and header cells are bold by nature. Readers of visual formats (PDF, RTF, DOCX) report the bold font
        // faithfully, which would make writers emit "# **Title**". When every run of a heading or header cell is bold,
        // the Bold flag is cleared.
        private static void ClearRedundantBold(List<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case HeadingBlock heading:
                        ClearIfAllBold(heading.Inlines);
                        break;
                    case SectionBlock section:
                        ClearRedundantBold(section.Blocks);
                        break;
                    case QuoteBlock quote:
                        ClearRedundantBold(quote.Blocks);
                        break;
                    case ListBlock list:
                        foreach (ListItemBlock item in list.Items) ClearRedundantBold(item.Blocks);
                        break;
                    case TableBlock table:
                        for (int r = 0; r < table.Rows.Count; r++)
                        {
                            foreach (TableCell cell in table.Rows[r].Cells)
                            {
                                if (cell.IsHeader || r < table.HeaderRowCount)
                                    foreach (Block b in cell.Blocks)
                                        if (b is ParagraphBlock p) ClearIfAllBold(p.Inlines);
                                ClearRedundantBold(cell.Blocks);
                            }
                        }

                        break;
                }
            }
        }

        private static void ClearIfAllBold(List<Inline> inlines)
        {
            bool any = false;
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline t)
                {
                    if (t.Text.Trim().Length == 0) continue;
                    if ((t.Style & InlineStyleEnum.Bold) != InlineStyleEnum.Bold) return;
                    any = true;
                }
                else if (inline is LinkInline)
                {
                    return;
                }
            }

            if (!any) return;
            foreach (Inline inline in inlines)
                if (inline is TextInline t) t.Style &= ~InlineStyleEnum.Bold;
        }

        private static int RemoveImages(List<Block> blocks)
        {
            int removed = 0;
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                Block block = blocks[i];
                if (block is ImageBlock)
                {
                    blocks.RemoveAt(i);
                    removed++;
                    continue;
                }

                switch (block)
                {
                    case SectionBlock section:
                        removed += RemoveImages(section.Blocks);
                        break;
                    case QuoteBlock quote:
                        removed += RemoveImages(quote.Blocks);
                        break;
                    case ListBlock list:
                        foreach (ListItemBlock item in list.Items) removed += RemoveImages(item.Blocks);
                        break;
                    case ListItemBlock listItem:
                        removed += RemoveImages(listItem.Blocks);
                        break;
                    case TableBlock table:
                        foreach (TableRow row in table.Rows)
                            foreach (TableCell cell in row.Cells) removed += RemoveImages(cell.Blocks);
                        break;
                    case HeadingBlock heading:
                        removed += RemoveInlineImages(heading.Inlines);
                        break;
                    case ParagraphBlock paragraph:
                        removed += RemoveInlineImages(paragraph.Inlines);
                        break;
                }
            }

            return removed;
        }

        private static int RemoveInlineImages(List<Inline> inlines)
        {
            int removed = 0;
            for (int i = inlines.Count - 1; i >= 0; i--)
            {
                if (inlines[i] is ImageInline)
                {
                    inlines.RemoveAt(i);
                    removed++;
                }
                else if (inlines[i] is LinkInline link)
                {
                    removed += RemoveInlineImages(link.Inlines);
                }
            }

            return removed;
        }
    }
}
