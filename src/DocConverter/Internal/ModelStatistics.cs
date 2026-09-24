namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Results;

    /// <summary>
    /// Computes ConversionStatistics from a document model.
    /// </summary>
    internal static class ModelStatistics
    {
        internal static ConversionStatistics Compute(DocumentModel document)
        {
            ConversionStatistics stats = new ConversionStatistics();
            int maxPage = 0;
            Walk(document.Blocks, stats, ref maxPage);
            if (stats.Pages < maxPage) stats.Pages = maxPage;
            return stats;
        }

        private static void Walk(IEnumerable<Block> blocks, ConversionStatistics stats, ref int maxPage)
        {
            foreach (Block block in blocks)
            {
                if (block.SourcePage.HasValue && block.SourcePage.Value > maxPage) maxPage = block.SourcePage.Value;

                switch (block)
                {
                    case SectionBlock section:
                        if (section.Kind == SectionKindEnum.Page) stats.Pages++;
                        else if (section.Kind == SectionKindEnum.Slide) stats.Slides++;
                        else if (section.Kind == SectionKindEnum.Sheet) stats.Sheets++;
                        if (section.Title != null) stats.Characters += section.Title.Length;
                        Walk(section.Blocks, stats, ref maxPage);
                        break;
                    case HeadingBlock heading:
                        stats.Headings++;
                        WalkInlines(heading.Inlines, stats);
                        break;
                    case ParagraphBlock paragraph:
                        stats.Paragraphs++;
                        WalkInlines(paragraph.Inlines, stats);
                        break;
                    case ListBlock list:
                        stats.Lists++;
                        foreach (ListItemBlock item in list.Items)
                        {
                            stats.ListItems++;
                            Walk(item.Blocks, stats, ref maxPage);
                        }

                        break;
                    case ListItemBlock listItem:
                        stats.ListItems++;
                        Walk(listItem.Blocks, stats, ref maxPage);
                        break;
                    case TableBlock table:
                        stats.Tables++;
                        foreach (TableRow row in table.Rows)
                        {
                            stats.TableRows++;
                            foreach (TableCell cell in row.Cells)
                            {
                                stats.TableCells++;
                                Walk(cell.Blocks, stats, ref maxPage);
                            }
                        }

                        break;
                    case QuoteBlock quote:
                        Walk(quote.Blocks, stats, ref maxPage);
                        break;
                    case CodeBlock code:
                        stats.Characters += code.Text.Length;
                        break;
                    case ImageBlock _:
                        stats.Images++;
                        break;
                }
            }
        }

        private static void WalkInlines(IEnumerable<Inline> inlines, ConversionStatistics stats)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text) stats.Characters += text.Text.Length;
                else if (inline is LinkInline link)
                {
                    stats.Links++;
                    WalkInlines(link.Inlines, stats);
                }
                else if (inline is ImageInline) stats.Images++;
            }
        }
    }
}
