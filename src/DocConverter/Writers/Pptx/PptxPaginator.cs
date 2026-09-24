namespace DocConverter.Writers.Pptx
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Turns a logical slide into one or more physical slides. A continuation slide starts when the page holds
    /// PptxOptions.MaxBlocksPerSlide blocks or the estimated content height no longer fits. Tables are split every
    /// PptxOptions.MaxTableRowsPerSlide rows with the header rows repeated.
    /// </summary>
    internal sealed class PptxPaginator
    {
        internal const long TextLineHeight = 274320L;
        internal const long TableRowHeight = 370840L;
        private const int _CharsPerLine = 90;

        private readonly PptxSlidePlan _Plan;
        private readonly DocumentModel _Document;
        private readonly ConversionOptions _Options;
        private readonly ConversionContext _Context;
        private readonly List<PptxSlidePage> _Pages = new List<PptxSlidePage>();
        private readonly long _Available = PresentationScaffold.ContentBottom - PresentationScaffold.ContentTop;

        private PptxPaginator(PptxSlidePlan plan, DocumentModel document, ConversionOptions options, ConversionContext context)
        {
            _Plan = plan;
            _Document = document;
            _Options = options;
            _Context = context;
        }

        internal static List<PptxSlidePage> Paginate(PptxSlidePlan plan, DocumentModel document, ConversionOptions options, ConversionContext context)
        {
            PptxPaginator paginator = new PptxPaginator(plan, document, options, context);
            PptxSlidePage first = new PptxSlidePage(plan.Title);
            first.Subtitle = plan.Subtitle;
            paginator._Pages.Add(first);
            foreach (Block block in plan.Body) paginator.Add(block);
            return paginator._Pages;
        }

        internal static long ImageHeight(BinaryResource resource, long maxWidth, long maxHeight, out long width)
        {
            int pw = resource.PixelWidth ?? 0;
            int ph = resource.PixelHeight ?? 0;
            if (pw <= 0 || ph <= 0)
            {
                ImageInfo? info = ImageHeaderReader.Read(resource.Data);
                pw = info?.Width ?? 0;
                ph = info?.Height ?? 0;
            }

            if (pw <= 0 || ph <= 0)
            {
                pw = 400;
                ph = 300;
            }

            double w = pw * 9525.0;
            double h = ph * 9525.0;
            double scale = Math.Min(1.0, Math.Min(maxWidth / w, maxHeight / h));
            width = (long)(w * scale);
            return (long)(h * scale);
        }

        private void Add(Block block)
        {
            switch (block)
            {
                case TableBlock table:
                    AddTable(table);
                    break;
                case ImageBlock image:
                    AddPicture(image.ResourceId, image.AltText);
                    break;
                case PageBreakBlock _:
                    NewPage();
                    break;
                case ThematicBreakBlock _:
                    break;
                case SectionBlock section:
                    if (!string.IsNullOrEmpty(section.Title)) AddText(new HeadingBlock(3, section.Title));
                    foreach (Block child in section.Blocks) Add(child);
                    break;
                default:
                    AddText(block);
                    if (block is ParagraphBlock paragraph) AddInlinePictures(paragraph.Inlines);
                    else if (block is HeadingBlock heading) AddInlinePictures(heading.Inlines);
                    break;
            }
        }

        private void AddInlinePictures(List<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is ImageInline image) AddPicture(image.ResourceId, image.AltText);
                else if (inline is LinkInline link) AddInlinePictures(link.Inlines);
            }
        }

        private void AddText(Block block)
        {
            long height = EstimateText(block);
            PptxSlidePage page = PageFor(height);
            PptxUnit? last = page.Units.Count > 0 ? page.Units[page.Units.Count - 1] : null;
            if (last == null || last.Kind != PptxUnitKind.Text)
            {
                last = new PptxUnit(PptxUnitKind.Text);
                page.Units.Add(last);
            }

            last.TextBlocks.Add(block);
            last.Height += height;
            page.UsedHeight += height;
            page.BlockCount++;
        }

        private void AddPicture(string resourceId, string? altText)
        {
            if (!_Document.Resources.TryGetValue(resourceId, out BinaryResource? resource) || resource.Data.Length == 0 || ImageHeaderReader.Read(resource.Data) == null)
            {
                _Context.AddWarning(WarningCodeEnum.ImagesOmitted, "An image was omitted because its resource '" + resourceId + "' is missing or not a recognized image format.");
                return;
            }

            long height = ImageHeight(resource, PresentationScaffold.ContentWidth, _Available, out long _);
            PptxSlidePage page = PageFor(height);
            PptxUnit unit = new PptxUnit(PptxUnitKind.Picture) { ResourceId = resourceId, AltText = altText, Height = height };
            page.Units.Add(unit);
            page.UsedHeight += height;
            page.BlockCount++;
        }

        private void AddTable(TableBlock table)
        {
            if (table.Rows.Count == 0) return;
            int header = Math.Min(table.HeaderRowCount, table.Rows.Count);
            int perSlide = Math.Max(1, _Options.Pptx.MaxTableRowsPerSlide - header);
            int dataRows = table.Rows.Count - header;
            if (dataRows == 0)
            {
                PlaceTable(Chunk(table, header, 0, 0), false);
                return;
            }

            bool first = true;
            for (int start = header; start < table.Rows.Count; start += perSlide)
            {
                int count = Math.Min(perSlide, table.Rows.Count - start);
                PlaceTable(Chunk(table, header, start, count), !first);
                first = false;
            }
        }

        private void PlaceTable(TableBlock chunk, bool forceNewPage)
        {
            long height = chunk.Rows.Count * TableRowHeight;
            if (height > _Available) height = _Available;
            PptxSlidePage page = forceNewPage ? NewPage() : PageFor(height);
            PptxUnit unit = new PptxUnit(PptxUnitKind.Table) { Table = chunk, Height = height };
            page.Units.Add(unit);
            page.UsedHeight += height;
            page.BlockCount++;
        }

        private static TableBlock Chunk(TableBlock table, int header, int start, int count)
        {
            TableBlock chunk = new TableBlock();
            chunk.HeaderRowCount = header;
            chunk.Caption = table.Caption;
            chunk.ColumnAlignments.AddRange(table.ColumnAlignments);
            for (int i = 0; i < header; i++) chunk.Rows.Add(table.Rows[i]);
            for (int i = start; i < start + count; i++) chunk.Rows.Add(table.Rows[i]);
            return chunk;
        }

        private PptxSlidePage PageFor(long height)
        {
            PptxSlidePage page = _Pages[_Pages.Count - 1];
            bool full = page.BlockCount >= _Options.Pptx.MaxBlocksPerSlide;
            bool overflow = page.BlockCount > 0 && page.UsedHeight + height > _Available;
            return full || overflow ? NewPage() : page;
        }

        private PptxSlidePage NewPage()
        {
            string? title = _Plan.Title == null ? null : _Plan.Title + " (continued)";
            PptxSlidePage page = new PptxSlidePage(title);
            _Pages.Add(page);
            return page;
        }

        private static long EstimateText(Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    return Lines(ModelText.Inlines(heading.Inlines)) * 350000L + 76200L;
                case CodeBlock code:
                    return Math.Max(1, code.Text.Split('\n').Length) * 220000L + 76200L;
                case ListBlock list:
                    long total = 0;
                    foreach (ListItemBlock item in list.Items)
                        foreach (Block child in item.Blocks) total += EstimateText(child);
                    return Math.Max(total, TextLineHeight);
                case ListItemBlock looseItem:
                    long itemTotal = 0;
                    foreach (Block child in looseItem.Blocks) itemTotal += EstimateText(child);
                    return Math.Max(itemTotal, TextLineHeight);
                case QuoteBlock quote:
                    long quoteTotal = 0;
                    foreach (Block child in quote.Blocks) quoteTotal += EstimateText(child);
                    return Math.Max(quoteTotal, TextLineHeight);
                default:
                    return Lines(ModelText.Block(block)) * TextLineHeight + 76200L;
            }
        }

        private static long Lines(string text)
        {
            long lines = 0;
            foreach (string line in text.Split('\n'))
                lines += Math.Max(1, (line.Length + _CharsPerLine - 1) / _CharsPerLine);
            return Math.Max(1, lines);
        }
    }
}
