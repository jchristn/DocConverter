namespace DocConverter.Writers.Pptx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Renders one physical slide: title placeholders, text boxes, tables and pictures stacked in the content area.
    /// </summary>
    internal sealed class PptxSlideDrawer
    {
        #region Private-Members

        private const int _BodySize = 1800;
        private const int _CodeSize = 1400;
        private const int _TableSize = 1400;
        private const long _Indent = 342900L;
        private const string _MonospaceFont = "Courier New";

        private readonly SlidePart _Slide;
        private readonly DocumentModel _Document;
        private readonly ConversionContext _Context;
        private readonly P.ShapeTree _Tree;
        private uint _NextShapeId = 2;
        private int _NextLink = 1;
        private int _NextImage = 1;
        private readonly Dictionary<string, string> _ImageRelationships = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        internal PptxSlideDrawer(SlidePart slide, DocumentModel document, ConversionContext context)
        {
            _Slide = slide;
            _Document = document;
            _Context = context;
            _Tree = new P.ShapeTree(PresentationScaffold.GroupProperties(), new P.GroupShapeProperties(new A.TransformGroup()));
        }

        #endregion

        #region Internal-Methods

        internal P.Slide Draw(PptxSlidePage page, bool titleSlide)
        {
            if (titleSlide)
            {
                _Tree.Append(TitlePlaceholder(page.Title ?? "", P.PlaceholderValues.CenteredTitle, null));
                if (!string.IsNullOrEmpty(page.Subtitle)) _Tree.Append(TitlePlaceholder(page.Subtitle!, P.PlaceholderValues.SubTitle, 1U));
            }
            else
            {
                if (!string.IsNullOrEmpty(page.Title)) _Tree.Append(TitlePlaceholder(page.Title!, P.PlaceholderValues.Title, null));
                long y = PresentationScaffold.ContentTop;
                if (!string.IsNullOrEmpty(page.Subtitle))
                {
                    List<Block> subtitle = new List<Block> { new HeadingBlock(2, page.Subtitle) };
                    _Tree.Append(TextBox(subtitle, y, PptxPaginator.TextLineHeight * 2));
                    y += PptxPaginator.TextLineHeight * 2;
                }

                foreach (PptxUnit unit in page.Units)
                {
                    long height = Math.Max(unit.Height, PptxPaginator.TextLineHeight);
                    if (unit.Kind == PptxUnitKind.Text) _Tree.Append(TextBox(unit.TextBlocks, y, height));
                    else if (unit.Kind == PptxUnitKind.Table && unit.Table != null) _Tree.Append(TableFrame(unit.Table, y));
                    else if (unit.Kind == PptxUnitKind.Picture && unit.ResourceId != null)
                    {
                        OpenXmlElement? picture = Picture(unit.ResourceId, unit.AltText, y, out long drawnHeight);
                        if (picture != null) _Tree.Append(picture);
                        height = drawnHeight;
                    }

                    y += height + 76200L;
                }
            }

            return new P.Slide(new P.CommonSlideData(_Tree), new P.ColorMapOverride(new A.MasterColorMapping()));
        }

        #endregion

        #region Private-Methods

        private P.Shape TitlePlaceholder(string text, P.PlaceholderValues type, uint? index)
        {
            P.PlaceholderShape placeholder = new P.PlaceholderShape { Type = type };
            if (index.HasValue) placeholder.Index = index.Value;
            uint id = _NextShapeId++;
            A.Paragraph paragraph = new A.Paragraph(new A.Run(new A.RunProperties { Language = "en-US", Dirty = false }, new A.Text(OfficeWriteHelper.CleanText(text))));
            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = (type == P.PlaceholderValues.SubTitle ? "Subtitle " : "Title ") + id },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties(placeholder)),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), paragraph));
        }

        private P.Shape TextBox(List<Block> blocks, long y, long height)
        {
            uint id = _NextShapeId++;
            P.TextBody body = new P.TextBody(new A.BodyProperties(new A.NormalAutoFit()) { Wrap = A.TextWrappingValues.Square }, new A.ListStyle());
            foreach (Block block in blocks) AppendBlock(body, block, 0, false);
            if (!body.Elements<A.Paragraph>().GetEnumerator().MoveNext()) body.Append(new A.Paragraph());

            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = "TextBox " + id },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }) { TextBox = true },
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = PresentationScaffold.ContentLeft, Y = y }, new A.Extents { Cx = PresentationScaffold.ContentWidth, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                    new A.NoFill()),
                body);
        }

        private void AppendBlock(OpenXmlCompositeElement body, Block block, int depth, bool italic)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    int size = heading.Level <= 2 ? 2400 : 2000;
                    body.Append(Paragraph(heading.Inlines, size, InlineStyleEnum.Bold | (italic ? InlineStyleEnum.Italic : InlineStyleEnum.None), null, 0, false, 1));
                    break;
                case ParagraphBlock paragraph:
                    body.Append(Paragraph(paragraph.Inlines, _BodySize, italic ? InlineStyleEnum.Italic : InlineStyleEnum.None, null, depth, false, 1));
                    break;
                case CodeBlock code:
                    foreach (string line in code.Text.Split('\n'))
                    {
                        List<Inline> inlines = new List<Inline> { new TextInline(line.TrimEnd('\r'), InlineStyleEnum.Code) };
                        body.Append(Paragraph(inlines, _CodeSize, InlineStyleEnum.None, null, depth, false, 1));
                    }

                    break;
                case QuoteBlock quote:
                    foreach (Block child in quote.Blocks) AppendBlock(body, child, depth, true);
                    break;
                case ListBlock list:
                    AppendList(body, list, depth, italic);
                    break;
                case ListItemBlock looseItem:
                    foreach (Block child in looseItem.Blocks) AppendBlock(body, child, depth, italic);
                    break;
                case TableBlock table:
                    body.Append(Paragraph(new List<Inline> { new TextInline(ModelText.Block(table)) }, _BodySize, InlineStyleEnum.None, null, depth, false, 1));
                    break;
                case SectionBlock section:
                    foreach (Block child in section.Blocks) AppendBlock(body, child, depth, italic);
                    break;
            }
        }

        private void AppendList(OpenXmlCompositeElement body, ListBlock list, int depth, bool italic)
        {
            int level = Math.Min(depth, 8);
            bool ordered = list.Kind == ListKindEnum.Ordered;
            foreach (ListItemBlock item in list.Items)
            {
                bool bulletUsed = false;
                foreach (Block child in item.Blocks)
                {
                    if (child is ListBlock nested)
                    {
                        AppendList(body, nested, depth + 1, italic);
                        continue;
                    }

                    if (child is ParagraphBlock paragraph && !bulletUsed)
                    {
                        List<Inline> inlines = new List<Inline>(paragraph.Inlines);
                        if (list.Kind == ListKindEnum.Task && item.Checked.HasValue) inlines.Insert(0, new TextInline(item.Checked.Value ? "[x] " : "[ ] "));
                        body.Append(Paragraph(inlines, _BodySize, italic ? InlineStyleEnum.Italic : InlineStyleEnum.None, ordered ? "auto" : "char", level, true, list.Start));
                        bulletUsed = true;
                        continue;
                    }

                    AppendBlock(body, child, level + 1, italic);
                }

                if (!bulletUsed)
                    body.Append(Paragraph(new List<Inline>(), _BodySize, InlineStyleEnum.None, ordered ? "auto" : "char", level, true, list.Start));
            }
        }

        private A.Paragraph Paragraph(List<Inline> inlines, int size, InlineStyleEnum extra, string? bullet, int level, bool isListItem, int start)
        {
            A.Paragraph paragraph = new A.Paragraph();
            A.ParagraphProperties props = new A.ParagraphProperties();
            if (isListItem)
            {
                props.Level = level;
                props.LeftMargin = (int)(_Indent * (level + 1));
                props.Indent = (int)-_Indent;
                if (bullet == "auto")
                {
                    A.AutoNumberedBullet auto = new A.AutoNumberedBullet { Type = A.TextAutoNumberSchemeValues.ArabicPeriod };
                    if (start != 1) auto.StartAt = start < 1 ? 1 : start;
                    props.Append(auto);
                }
                else
                {
                    props.Append(new A.BulletFont { Typeface = "Arial" });
                    props.Append(new A.CharacterBullet { Char = "•" });
                }
            }
            else
            {
                if (level > 0) props.LeftMargin = (int)(_Indent * level);
                props.Append(new A.NoBullet());
            }

            paragraph.Append(props);
            AppendInlines(paragraph, inlines, size, extra, null);
            paragraph.Append(new A.EndParagraphRunProperties { Language = "en-US", FontSize = size, Dirty = false });
            return paragraph;
        }

        private void AppendInlines(A.Paragraph paragraph, List<Inline> inlines, int size, InlineStyleEnum extra, string? linkId)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        if (text.Text.Length == 0) continue;
                        paragraph.Append(new A.Run(RunProperties(text.Style | extra, size, linkId), new A.Text(OfficeWriteHelper.CleanText(text.Text))));
                        break;
                    case LineBreakInline _:
                        paragraph.Append(new A.Break(new A.RunProperties { Language = "en-US", FontSize = size, Dirty = false }));
                        break;
                    case LinkInline link:
                        string? id = LinkRelationship(link.Url);
                        AppendInlines(paragraph, link.Inlines, size, extra, id);
                        break;
                    case ImageInline image:
                        if (!string.IsNullOrEmpty(image.AltText))
                            paragraph.Append(new A.Run(RunProperties(extra, size, linkId), new A.Text(OfficeWriteHelper.CleanText(image.AltText))));
                        break;
                }
            }
        }

        private static A.RunProperties RunProperties(InlineStyleEnum style, int size, string? linkId)
        {
            A.RunProperties props = new A.RunProperties { Language = "en-US", FontSize = size, Dirty = false };
            if ((style & InlineStyleEnum.Bold) != 0) props.Bold = true;
            if ((style & InlineStyleEnum.Italic) != 0) props.Italic = true;
            if ((style & InlineStyleEnum.Underline) != 0) props.Underline = A.TextUnderlineValues.Single;
            if ((style & InlineStyleEnum.Strikethrough) != 0) props.Strike = A.TextStrikeValues.SingleStrike;
            if ((style & InlineStyleEnum.Superscript) != 0) props.Baseline = 30000;
            else if ((style & InlineStyleEnum.Subscript) != 0) props.Baseline = -25000;
            if ((style & InlineStyleEnum.Code) != 0) props.Append(new A.LatinFont { Typeface = _MonospaceFont });
            if (linkId != null) props.Append(new A.HyperlinkOnClick { Id = linkId });
            return props;
        }

        private string? LinkRelationship(string url)
        {
            if (!OfficeWriteHelper.IsSafeUrl(url))
            {
                _Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link with a disallowed URL scheme was removed; its text was kept.");
                return null;
            }

            Uri? uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.RelativeOrAbsolute, out uri) || uri == null) return null;
            string id = "rIdLink" + (_NextLink++).ToString(CultureInfo.InvariantCulture);
            _Slide.AddHyperlinkRelationship(uri, uri.IsAbsoluteUri, id);
            return id;
        }

        private P.GraphicFrame TableFrame(TableBlock table, long y)
        {
            int columns = Math.Max(1, table.ColumnCount);
            int rows = table.Rows.Count;
            bool[,] covered = new bool[rows, columns];
            A.Table grid = new A.Table();
            int header = Math.Min(table.HeaderRowCount, rows);
            grid.Append(new A.TableProperties { FirstRow = header > 0, BandRow = true });
            long columnWidth = PresentationScaffold.ContentWidth / columns;
            A.TableGrid tableGrid = new A.TableGrid();
            for (int c = 0; c < columns; c++) tableGrid.Append(new A.GridColumn { Width = columnWidth });
            grid.Append(tableGrid);

            for (int r = 0; r < rows; r++)
            {
                A.TableRow row = new A.TableRow { Height = PptxPaginator.TableRowHeight };
                int col = 0;
                Queue<TableCell> cells = new Queue<TableCell>(table.Rows[r].Cells);
                while (col < columns)
                {
                    if (covered[r, col])
                    {
                        A.TableCell placeholder = EmptyCell();
                        bool horizontal = col > 0 && covered[r, col - 1];
                        placeholder.VerticalMerge = true;
                        if (horizontal) placeholder.HorizontalMerge = true;
                        row.Append(placeholder);
                        col++;
                        continue;
                    }

                    if (cells.Count == 0)
                    {
                        row.Append(EmptyCell());
                        col++;
                        continue;
                    }

                    TableCell cell = cells.Dequeue();
                    int span = Math.Min(cell.ColumnSpan, columns - col);
                    int rowSpan = Math.Min(cell.RowSpan, rows - r);
                    bool bold = r < header || cell.IsHeader;
                    A.TableCell tc = Cell(cell, bold);
                    if (span > 1) tc.GridSpan = span;
                    if (rowSpan > 1) tc.RowSpan = rowSpan;
                    row.Append(tc);
                    for (int rr = r + 1; rr < r + rowSpan; rr++)
                        for (int cc = col; cc < col + span; cc++) covered[rr, cc] = true;
                    for (int cc = col + 1; cc < col + span; cc++)
                    {
                        A.TableCell merged = EmptyCell();
                        merged.HorizontalMerge = true;
                        row.Append(merged);
                    }

                    col += span;
                }

                grid.Append(row);
            }

            uint id = _NextShapeId++;
            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = "Table " + id },
                    new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = PresentationScaffold.ContentLeft, Y = y }, new A.Extents { Cx = columnWidth * columns, Cy = PptxPaginator.TableRowHeight * rows }),
                new A.Graphic(new A.GraphicData(grid) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
        }

        private A.TableCell Cell(TableCell cell, bool bold)
        {
            A.TextBody body = new A.TextBody(new A.BodyProperties(), new A.ListStyle());
            bool any = false;
            foreach (Block block in cell.Blocks)
            {
                if (block is ParagraphBlock paragraph)
                {
                    body.Append(Paragraph(paragraph.Inlines, _TableSize, bold ? InlineStyleEnum.Bold : InlineStyleEnum.None, null, 0, false, 1));
                }
                else
                {
                    string text = ModelText.Block(block);
                    body.Append(Paragraph(new List<Inline> { new TextInline(text) }, _TableSize, bold ? InlineStyleEnum.Bold : InlineStyleEnum.None, null, 0, false, 1));
                }

                any = true;
            }

            if (!any) body.Append(new A.Paragraph(new A.EndParagraphRunProperties { Language = "en-US", FontSize = _TableSize, Dirty = false }));
            return new A.TableCell(body, new A.TableCellProperties());
        }

        private static A.TableCell EmptyCell()
        {
            return new A.TableCell(
                new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.EndParagraphRunProperties { Language = "en-US", Dirty = false })),
                new A.TableCellProperties());
        }

        private OpenXmlElement? Picture(string resourceId, string? altText, long y, out long height)
        {
            height = 0;
            if (!_Document.Resources.TryGetValue(resourceId, out BinaryResource? resource)) return null;
            ImageInfo? info = ImageHeaderReader.Read(resource.Data);
            if (info == null) return null;

            if (!_ImageRelationships.TryGetValue(resourceId, out string? relId))
            {
                relId = "rIdImage" + (_NextImage++).ToString(CultureInfo.InvariantCulture);
                ImagePart part = _Slide.AddImagePart(new PartTypeInfo(info.MediaType, "." + ImageHeaderReader.ExtensionFor(info.Format)), relId);
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream(resource.Data))
                {
                    part.FeedData(ms);
                }

                _ImageRelationships[resourceId] = relId;
            }

            long available = PresentationScaffold.ContentBottom - y;
            if (available < PptxPaginator.TextLineHeight) available = PresentationScaffold.ContentBottom - PresentationScaffold.ContentTop;
            height = PptxPaginator.ImageHeight(resource, PresentationScaffold.ContentWidth, available, out long width);
            if (width < 1) width = 1;
            if (height < 1) height = 1;
            uint id = _NextShapeId++;
            P.NonVisualDrawingProperties nv = new P.NonVisualDrawingProperties { Id = id, Name = "Picture " + id };
            if (!string.IsNullOrEmpty(altText)) nv.Description = OfficeWriteHelper.CleanText(altText);
            return new P.Picture(
                new P.NonVisualPictureProperties(
                    nv,
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = PresentationScaffold.ContentLeft, Y = y }, new A.Extents { Cx = width, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        }

        #endregion
    }
}
