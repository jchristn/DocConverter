namespace DocConverter.Writers.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using MigraDoc.DocumentObjectModel;
    using MigraDoc.DocumentObjectModel.Tables;
    using MigraDoc.Rendering;
    using PdfSharp.Fonts;
    using MigraImage = MigraDoc.DocumentObjectModel.Shapes.Image;

    /// <summary>
    /// Maps one DocumentModel onto a MigraDoc document and renders it to PDF. One instance per conversion.
    /// </summary>
    internal sealed class PdfModelRenderer
    {
        #region Private-Members

        private const double ListIndentStep = 18.0;
        private const double ListHang = 18.0;
        private const double QuoteIndent = 18.0;

        private static readonly DateTime _DeterministicDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string _DeterministicId = "0D0C0C0E000040008000000000000001";

        private readonly DocumentModel _Model;
        private readonly ConversionOptions _Options;
        private readonly ConversionContext _Context;
        private readonly CancellationToken _Token;
        private readonly bool _CountGlyphs;
        private double _PageWidth = 612;
        private double _PageHeight = 792;
        private double _ContentWidth = 468;
        private long _MissingGlyphs = 0;
        private bool _StrikeWarned = false;
        private bool _AnyContent = false;
        private int _CellDepth = 0;

        #endregion

        #region Constructors-and-Factories

        internal PdfModelRenderer(DocumentModel model, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            _Model = model;
            _Options = options;
            _Context = context;
            _Token = token;
            _CountGlyphs = GlobalFontSettings.FontResolver is DocConverterFontResolver;
        }

        #endregion

        #region Public-Methods

        internal byte[] Render()
        {
            Document document = new Document();
            ApplyInfo(document);
            ConfigureStyles(document);

            Section section = document.AddSection();
            ConfigurePage(section);

            RenderBlocks(section.Elements, _Model.Blocks, 0, _ContentWidth, false, true);
            if (!_AnyContent) section.AddParagraph();

            if (_MissingGlyphs > 0)
            {
                _Context.AddWarning(
                    WarningCodeEnum.GlyphsUnavailable,
                    _MissingGlyphs.ToString(CultureInfo.InvariantCulture) + " character(s) cannot be drawn with the embedded font (Liberation) and render blank.");
            }

            _Token.ThrowIfCancellationRequested();
            PdfDocumentRenderer renderer = new PdfDocumentRenderer();
            renderer.Document = document;
            renderer.RenderDocument();

            PdfSharp.Pdf.PdfDocument pdf = renderer.PdfDocument;
            pdf.Info.Creator = "DocConverter";
            if (!string.IsNullOrEmpty(_Model.Metadata.Title)) pdf.Info.Title = _Model.Metadata.Title!;
            if (!string.IsNullOrEmpty(_Model.Metadata.Author)) pdf.Info.Author = _Model.Metadata.Author!;
            if (!string.IsNullOrEmpty(_Model.Metadata.Subject)) pdf.Info.Subject = _Model.Metadata.Subject!;
            if (!string.IsNullOrEmpty(_Model.Metadata.Keywords)) pdf.Info.Keywords = _Model.Metadata.Keywords!;

            if (_Options.Deterministic)
            {
                pdf.Info.CreationDate = _DeterministicDate;
                pdf.Info.ModificationDate = _DeterministicDate;
                pdf.Internals.FirstDocumentID = _DeterministicId;
                pdf.Internals.SecondDocumentID = _DeterministicId;
                foreach (PdfSharp.Pdf.PdfPage page in pdf.Pages)
                {
                    foreach (PdfSharp.Pdf.Annotations.PdfAnnotation annotation in page.Annotations)
                    {
                        annotation.Elements.SetDateTime("/M", _DeterministicDate);
                        annotation.Elements.Remove("/NM");
                    }
                }
            }
            else if (_Model.Metadata.CreatedUtc.HasValue)
            {
                pdf.Info.CreationDate = _Model.Metadata.CreatedUtc.Value;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                pdf.Save(ms, false);
                byte[] bytes = ms.ToArray();
                if (_Options.Deterministic)
                {
                    PdfSubsetTagNormalizer.Normalize(bytes);
                    PdfSubsetTagNormalizer.NormalizeGuids(bytes);
                }
                return bytes;
            }
        }

        #endregion

        #region Private-Methods

        private void ApplyInfo(Document document)
        {
            DocumentMetadata m = _Model.Metadata;
            if (!string.IsNullOrEmpty(m.Title)) document.Info.Title = m.Title!;
            if (!string.IsNullOrEmpty(m.Author)) document.Info.Author = m.Author!;
            if (!string.IsNullOrEmpty(m.Subject)) document.Info.Subject = m.Subject!;
            if (!string.IsNullOrEmpty(m.Keywords)) document.Info.Keywords = m.Keywords!;
        }

        private void ConfigureStyles(Document document)
        {
            double baseSize = _Options.Pdf.BaseFontSize;
            Style normal = document.Styles["Normal"]!;
            normal.Font.Name = EmbeddedFonts.SansFamily;
            normal.Font.Size = Unit.FromPoint(baseSize);
            normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(baseSize * 0.55);

            double[] scale = new double[] { 2.0, 1.6, 1.3, 1.15, 1.05, 1.0 };
            for (int level = 1; level <= 6; level++)
            {
                Style heading = document.Styles["Heading" + level]!;
                heading.Font.Name = EmbeddedFonts.SansFamily;
                heading.Font.Size = Unit.FromPoint(baseSize * scale[level - 1]);
                heading.Font.Bold = true;
                heading.Font.Italic = false;
                heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(baseSize * (level <= 2 ? 1.2 : 0.9));
                heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(baseSize * 0.5);
                heading.ParagraphFormat.KeepWithNext = true;
            }
        }

        private void ConfigurePage(Section section)
        {
            switch (_Options.Pdf.PageSize)
            {
                case PdfPageSizeEnum.A4:
                    _PageWidth = 595.28;
                    _PageHeight = 841.89;
                    break;
                case PdfPageSizeEnum.Legal:
                    _PageWidth = 612;
                    _PageHeight = 1008;
                    break;
                default:
                    _PageWidth = 612;
                    _PageHeight = 792;
                    break;
            }

            double margin = _Options.Pdf.MarginPoints;
            if (margin * 2 > _PageWidth - 72) margin = (_PageWidth - 72) / 2;
            _ContentWidth = _PageWidth - 2 * margin;

            section.PageSetup.PageWidth = Unit.FromPoint(_PageWidth);
            section.PageSetup.PageHeight = Unit.FromPoint(_PageHeight);
            section.PageSetup.TopMargin = Unit.FromPoint(margin);
            section.PageSetup.BottomMargin = Unit.FromPoint(margin);
            section.PageSetup.LeftMargin = Unit.FromPoint(margin);
            section.PageSetup.RightMargin = Unit.FromPoint(margin);
        }

        private void RenderBlocks(DocumentElements target, List<Block> blocks, double indent, double width, bool italic, bool topLevel)
        {
            foreach (Block block in blocks)
            {
                _Token.ThrowIfCancellationRequested();
                RenderBlock(target, block, indent, width, italic, topLevel);
            }
        }

        private void RenderBlock(DocumentElements target, Block block, double indent, double width, bool italic, bool topLevel)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    Paragraph hp = target.AddParagraph();
                    hp.Style = "Heading" + heading.Level;
                    ApplyIndent(hp, indent, italic);
                    AddInlines(hp, heading.Inlines, false);
                    _AnyContent = true;
                    break;
                case ParagraphBlock paragraph:
                    Paragraph p = target.AddParagraph();
                    ApplyIndent(p, indent, italic);
                    ApplyAlignment(p, paragraph.Alignment);
                    AddInlines(p, paragraph.Inlines, false);
                    _AnyContent = true;
                    break;
                case ListBlock list:
                    RenderList(target, list, 0, indent, width, italic);
                    break;
                case ListItemBlock looseItem:
                    ListBlock wrapper = new ListBlock(ListKindEnum.Unordered);
                    wrapper.Items.Add(looseItem);
                    RenderList(target, wrapper, 0, indent, width, italic);
                    break;
                case TableBlock table:
                    RenderTable(target, table, indent, width, topLevel);
                    break;
                case CodeBlock code:
                    RenderCode(target, code, indent);
                    break;
                case QuoteBlock quote:
                    int before = target.Count;
                    RenderBlocks(target, quote.Blocks, indent + QuoteIndent, width - QuoteIndent, true, false);
                    for (int i = before; i < target.Count; i++)
                    {
                        if (target[i] is Paragraph qp)
                        {
                            qp.Format.Borders.Left.Width = Unit.FromPoint(1.5);
                            qp.Format.Borders.Left.Color = Color.FromRgb(0xA0, 0xA0, 0xA0);
                            qp.Format.Borders.DistanceFromLeft = Unit.FromPoint(6);
                        }
                    }

                    break;
                case ImageBlock image:
                    RenderImageBlock(target, image, indent, width);
                    break;
                case ThematicBreakBlock _:
                    Paragraph rule = target.AddParagraph();
                    rule.Format.Borders.Bottom.Width = Unit.FromPoint(0.75);
                    rule.Format.Borders.Bottom.Color = Color.FromRgb(0x90, 0x90, 0x90);
                    rule.Format.SpaceAfter = Unit.FromPoint(_Options.Pdf.BaseFontSize);
                    _AnyContent = true;
                    break;
                case PageBreakBlock _:
                    if (topLevel) target.AddPageBreak();
                    break;
                case SectionBlock section:
                    RenderSection(target, section, indent, width, italic, topLevel);
                    break;
                default:
                    _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "A block of type " + block.GetType().Name + " is not supported by the PDF writer and was skipped.");
                    break;
            }
        }

        private void RenderSection(DocumentElements target, SectionBlock section, double indent, double width, bool italic, bool topLevel)
        {
            bool paged = section.Kind == SectionKindEnum.Page || section.Kind == SectionKindEnum.Slide;
            if (paged && topLevel && _AnyContent) target.AddPageBreak();
            if (SectionTitles.ShouldRender(section))
            {
                Paragraph title = target.AddParagraph();
                title.Style = "Heading2";
                ApplyIndent(title, indent, italic);
                AddText(title, section.Title!, InlineStyleEnum.None);
                _AnyContent = true;
            }

            RenderBlocks(target, section.Blocks, indent, width, italic, topLevel);
        }

        private void RenderList(DocumentElements target, ListBlock list, int level, double indent, double width, bool italic)
        {
            int number = list.Start;
            double itemIndent = indent + ListIndentStep * level + ListHang;
            foreach (ListItemBlock item in list.Items)
            {
                _Token.ThrowIfCancellationRequested();
                string marker;
                if (list.Kind == ListKindEnum.Ordered) marker = number.ToString(CultureInfo.InvariantCulture) + ".";
                else if (list.Kind == ListKindEnum.Task) marker = item.Checked == true ? "[x]" : "[ ]";
                else marker = "\u2022";
                number++;

                bool markerWritten = false;
                foreach (Block child in item.Blocks)
                {
                    if (child is ListBlock nested)
                    {
                        if (!markerWritten)
                        {
                            MarkerParagraph(target, marker, itemIndent, italic);
                            markerWritten = true;
                        }

                        RenderList(target, nested, level + 1, indent, width, italic);
                        continue;
                    }

                    if (!markerWritten && child is ParagraphBlock first)
                    {
                        Paragraph p = MarkerParagraph(target, marker, itemIndent, italic);
                        AddInlines(p, first.Inlines, false);
                        markerWritten = true;
                        continue;
                    }

                    if (!markerWritten)
                    {
                        MarkerParagraph(target, marker, itemIndent, italic);
                        markerWritten = true;
                    }

                    RenderBlock(target, child, itemIndent, width - itemIndent + indent, italic, false);
                }

                if (!markerWritten) MarkerParagraph(target, marker, itemIndent, italic);
            }
        }

        private Paragraph MarkerParagraph(DocumentElements target, string marker, double itemIndent, bool italic)
        {
            Paragraph p = target.AddParagraph();
            p.Format.LeftIndent = Unit.FromPoint(itemIndent);
            p.Format.FirstLineIndent = Unit.FromPoint(-ListHang);
            p.Format.TabStops.AddTabStop(Unit.FromPoint(itemIndent));
            p.Format.SpaceAfter = Unit.FromPoint(_Options.Pdf.BaseFontSize * 0.25);
            if (italic) p.Format.Font.Italic = true;
            CountGlyphs(marker, false);
            p.AddText(marker);
            p.AddTab();
            _AnyContent = true;
            return p;
        }

        private void RenderCode(DocumentElements target, CodeBlock code, double indent)
        {
            Paragraph p = target.AddParagraph();
            p.Format.Font.Name = EmbeddedFonts.MonoFamily;
            p.Format.Font.Size = Unit.FromPoint(_Options.Pdf.BaseFontSize * 0.9);
            p.Format.Shading.Color = Color.FromRgb(0xF2, 0xF2, 0xF2);
            p.Format.LeftIndent = Unit.FromPoint(indent + 4);
            p.Format.RightIndent = Unit.FromPoint(4);
            p.Format.SpaceBefore = Unit.FromPoint(2);
            p.Format.SpaceAfter = Unit.FromPoint(_Options.Pdf.BaseFontSize * 0.7);

            string[] lines = code.Text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) p.AddLineBreak();
                string line = lines[i].Replace("\t", "    ");
                int lead = 0;
                while (lead < line.Length && line[lead] == ' ') lead++;
                string text = new string('\u00A0', lead) + line.Substring(lead);
                CountGlyphs(text, true);
                if (text.Length > 0) p.AddText(text);
            }

            _AnyContent = true;
        }

        private void RenderTable(DocumentElements target, TableBlock model, double indent, double width, bool topLevel)
        {
            int columns = model.ColumnCount;
            if (columns == 0 || model.Rows.Count == 0) return;

            if (_CellDepth > 0)
            {
                FlattenTable(target, model, indent);
                return;
            }

            double[] weights = new double[columns];
            for (int c = 0; c < columns; c++) weights[c] = 3;
            foreach (TableRow row in model.Rows)
            {
                int col = 0;
                foreach (TableCell cell in row.Cells)
                {
                    if (col >= columns) break;
                    if (cell.ColumnSpan == 1)
                    {
                        int length = ModelText.Blocks(cell.Blocks, " ").Length;
                        double w = length > 60 ? 60 : length;
                        if (w > weights[col]) weights[col] = w;
                    }

                    col += cell.ColumnSpan;
                }
            }

            double total = 0;
            foreach (double w in weights) total += w;
            double available = width - indent;
            if (available < 72) available = 72;

            Table table = target.AddTable();
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Borders.Color = Color.FromRgb(0x80, 0x80, 0x80);
            table.Format.SpaceAfter = Unit.FromPoint(0);
            table.TopPadding = Unit.FromPoint(2);
            table.BottomPadding = Unit.FromPoint(2);
            table.LeftPadding = Unit.FromPoint(3);
            table.RightPadding = Unit.FromPoint(3);
            if (indent > 0) table.Rows.LeftIndent = Unit.FromPoint(indent);

            double[] widths = new double[columns];
            for (int c = 0; c < columns; c++)
            {
                widths[c] = available * weights[c] / total;
                table.AddColumn(Unit.FromPoint(widths[c]));
            }

            int[] spanRemaining = new int[columns];
            bool spansFlattened = false;
            for (int r = 0; r < model.Rows.Count; r++)
            {
                _Token.ThrowIfCancellationRequested();
                TableRow modelRow = model.Rows[r];
                Row row = table.AddRow();
                bool header = r < model.HeaderRowCount || (modelRow.Cells.Count > 0 && AllHeader(modelRow));
                if (header)
                {
                    row.HeadingFormat = r < model.HeaderRowCount;
                    row.Format.Font.Bold = true;
                    row.Shading.Color = Color.FromRgb(0xE8, 0xE8, 0xE8);
                }

                int col = 0;
                foreach (TableCell modelCell in modelRow.Cells)
                {
                    while (col < columns && spanRemaining[col] > 0) col++;
                    if (col >= columns)
                    {
                        spansFlattened = true;
                        break;
                    }

                    int colSpan = modelCell.ColumnSpan;
                    if (col + colSpan > columns) colSpan = columns - col;
                    int rowSpan = modelCell.RowSpan;
                    if (r + rowSpan > model.Rows.Count) rowSpan = model.Rows.Count - r;

                    Cell cell = row.Cells[col];
                    if (colSpan > 1) cell.MergeRight = colSpan - 1;
                    if (rowSpan > 1)
                    {
                        cell.MergeDown = rowSpan - 1;
                        for (int k = col; k < col + colSpan; k++) spanRemaining[k] = rowSpan;
                    }

                    double cellWidth = 0;
                    for (int k = col; k < col + colSpan; k++) cellWidth += widths[k];
                    _CellDepth++;
                    try
                    {
                        RenderBlocks(cell.Elements, modelCell.Blocks, 0, cellWidth - 6, false, false);
                    }
                    finally
                    {
                        _CellDepth--;
                    }
                    col += colSpan;
                }

                for (int k = 0; k < columns; k++) if (spanRemaining[k] > 0) spanRemaining[k]--;
            }

            if (spansFlattened) _Context.AddWarning(WarningCodeEnum.TableSpansFlattened, "Some merged table cells overlapped and were dropped in the PDF output.");
            Paragraph spacer = target.AddParagraph();
            spacer.Format.SpaceAfter = Unit.FromPoint(_Options.Pdf.BaseFontSize * 0.3);
            _AnyContent = true;
        }

        private void FlattenTable(DocumentElements target, TableBlock model, double indent)
        {
            _Context.AddWarning(WarningCodeEnum.TablesFlattened, "A table nested inside a table cell was written as text rows, because PDF output cannot nest tables.");
            foreach (TableRow row in model.Rows)
            {
                List<string> cells = new List<string>();
                foreach (TableCell cell in row.Cells) cells.Add(ModelText.Blocks(cell.Blocks, " "));
                Paragraph p = target.AddParagraph();
                ApplyIndent(p, indent, false);
                AddText(p, string.Join(" | ", cells), InlineStyleEnum.None);
            }
        }

        private static bool AllHeader(TableRow row)
        {
            foreach (TableCell cell in row.Cells) if (!cell.IsHeader) return false;
            return true;
        }

        private void RenderImageBlock(DocumentElements target, ImageBlock block, double indent, double width)
        {
            Paragraph p = target.AddParagraph();
            ApplyIndent(p, indent, false);
            AddImage(p, block.ResourceId, block.AltText, block.Width, block.Height, width - indent);
            if (!string.IsNullOrEmpty(block.Caption))
            {
                Paragraph caption = target.AddParagraph();
                ApplyIndent(caption, indent, true);
                caption.Format.Font.Size = Unit.FromPoint(_Options.Pdf.BaseFontSize * 0.9);
                AddText(caption, block.Caption!, InlineStyleEnum.None);
            }

            _AnyContent = true;
        }

        private void AddImage(Paragraph p, string resourceId, string? altText, double? widthPoints, double? heightPoints, double maxWidth)
        {
            if (!_Model.Resources.TryGetValue(resourceId, out BinaryResource? resource))
            {
                _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An image references the missing resource '" + resourceId + "' and was skipped.");
                AddPlaceholder(p, altText ?? resourceId, "missing", null, null);
                return;
            }

            ImageInfo? info = ImageHeaderReader.Read(resource.Data);
            string name = !string.IsNullOrEmpty(altText) ? altText! : (!string.IsNullOrEmpty(resource.FileName) ? resource.FileName! : resource.Id);
            if (info == null || !IsEmbeddable(info))
            {
                string formatName = info == null ? "unknown format" : info.FormatName;
                int? w = info?.Width ?? resource.PixelWidth;
                int? h = info?.Height ?? resource.PixelHeight;
                _Context.AddWarning(WarningCodeEnum.ImageFormatUnsupported, "An image in " + formatName + " format cannot be embedded in PDF and was replaced by a placeholder.");
                AddPlaceholder(p, name, formatName, w, h);
                return;
            }

            MigraImage image = p.AddImage("base64:" + Convert.ToBase64String(resource.Data));
            image.LockAspectRatio = true;
            int pixelWidth = info.Width ?? resource.PixelWidth ?? 96;
            int pixelHeight = info.Height ?? resource.PixelHeight ?? 96;
            double w2 = widthPoints ?? pixelWidth * 72.0 / 96.0;
            double h2 = heightPoints ?? pixelHeight * 72.0 / 96.0;
            if (w2 <= 0) w2 = 1;
            if (h2 <= 0) h2 = 1;
            double limit = maxWidth > 24 ? maxWidth : 24;
            if (w2 > limit)
            {
                h2 = h2 * limit / w2;
                w2 = limit;
            }

            double maxHeight = _PageHeight - 2 * _Options.Pdf.MarginPoints - 24;
            if (maxHeight > 24 && h2 > maxHeight)
            {
                w2 = w2 * maxHeight / h2;
                h2 = maxHeight;
            }

            image.Width = Unit.FromPoint(w2);
            image.Height = Unit.FromPoint(h2);
        }

        private void AddPlaceholder(Paragraph p, string name, string formatName, int? width, int? height)
        {
            string size = width.HasValue && height.HasValue
                ? " " + width.Value.ToString(CultureInfo.InvariantCulture) + "x" + height.Value.ToString(CultureInfo.InvariantCulture)
                : "";
            string text = "[Image: " + name + ", " + formatName + size + ", not embeddable in PDF]";
            p.Format.Borders.Width = Unit.FromPoint(0.75);
            p.Format.Borders.Color = Color.FromRgb(0x90, 0x90, 0x90);
            p.Format.Borders.Distance = Unit.FromPoint(3);
            AddText(p, text, InlineStyleEnum.None);
        }

        private static bool IsEmbeddable(ImageInfo info)
        {
            if (info.Format == DocumentFormatEnum.Png || info.Format == DocumentFormatEnum.Bmp) return true;
            if (info.Format == DocumentFormatEnum.Jpeg) return info.JpegComponents == 1 || info.JpegComponents == 3;
            return false;
        }

        private void ApplyIndent(Paragraph p, double indent, bool italic)
        {
            if (indent > 0) p.Format.LeftIndent = Unit.FromPoint(indent);
            if (italic) p.Format.Font.Italic = true;
        }

        private static void ApplyAlignment(Paragraph p, TextAlignmentEnum alignment)
        {
            switch (alignment)
            {
                case TextAlignmentEnum.Center:
                    p.Format.Alignment = ParagraphAlignment.Center;
                    break;
                case TextAlignmentEnum.Right:
                    p.Format.Alignment = ParagraphAlignment.Right;
                    break;
                case TextAlignmentEnum.Justify:
                    p.Format.Alignment = ParagraphAlignment.Justify;
                    break;
            }
        }

        private void AddInlines(object container, List<Inline> inlines, bool inLink)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        AddText(container, text.Text, text.Style);
                        break;
                    case LinkInline link:
                        if (!inLink && container is Paragraph paragraph && IsSafeExternalUrl(link.Url))
                        {
                            Hyperlink hyperlink = paragraph.AddHyperlink(link.Url, HyperlinkType.Web);
                            AddInlines(hyperlink, link.Inlines, true);
                        }
                        else
                        {
                            if (IsUnsafeScheme(link.Url))
                                _Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link with a disallowed URL scheme was written as plain text.");
                            AddInlines(container, link.Inlines, inLink);
                        }

                        break;
                    case ImageInline image:
                        if (container is Paragraph imageParagraph)
                            AddImage(imageParagraph, image.ResourceId, image.AltText, null, null, _ContentWidth);
                        break;
                    case LineBreakInline _:
                        if (container is Paragraph lp) lp.AddLineBreak();
                        else if (container is Hyperlink lh) lh.AddText(" ");
                        break;
                }
            }
        }

        private void AddText(object container, string text, InlineStyleEnum style)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool mono = (style & InlineStyleEnum.Code) == InlineStyleEnum.Code;
            if ((style & InlineStyleEnum.Strikethrough) == InlineStyleEnum.Strikethrough && !_StrikeWarned)
            {
                _StrikeWarned = true;
                _Context.AddWarning(WarningCodeEnum.FormattingLost, "Strikethrough text is written without the strike line, because the PDF writer cannot draw it.");
            }

            string[] parts = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    if (container is Paragraph bp) bp.AddLineBreak();
                    else if (container is Hyperlink bh) bh.AddText(" ");
                }

                string part = parts[i].Replace("\t", "    ");
                if (part.Length == 0) continue;
                CountGlyphs(part, mono);

                FormattedText formatted;
                if (container is Paragraph paragraph) formatted = paragraph.AddFormattedText(part);
                else if (container is Hyperlink hyperlink) formatted = hyperlink.AddFormattedText(part);
                else return;

                if ((style & InlineStyleEnum.Bold) == InlineStyleEnum.Bold) formatted.Bold = true;
                if ((style & InlineStyleEnum.Italic) == InlineStyleEnum.Italic) formatted.Italic = true;
                if ((style & InlineStyleEnum.Underline) == InlineStyleEnum.Underline) formatted.Underline = Underline.Single;
                if ((style & InlineStyleEnum.Superscript) == InlineStyleEnum.Superscript) formatted.Superscript = true;
                if ((style & InlineStyleEnum.Subscript) == InlineStyleEnum.Subscript) formatted.Subscript = true;
                if (mono) formatted.Font.Name = EmbeddedFonts.MonoFamily;
                if (container is Hyperlink) formatted.Color = Color.FromRgb(0x1F, 0x4E, 0x8C);
            }
        }

        private void CountGlyphs(string text, bool mono)
        {
            if (!_CountGlyphs || string.IsNullOrEmpty(text)) return;
            TrueTypeCmap cmap = mono ? TrueTypeCmap.Mono : TrueTypeCmap.Sans;
            for (int i = 0; i < text.Length; i++)
            {
                int codePoint;
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else
                {
                    if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                    UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (category == UnicodeCategory.Format || (category == UnicodeCategory.NonSpacingMark && c >= '\uFE00' && c <= '\uFE0F')) continue;
                    codePoint = c;
                }

                if (!cmap.Contains(codePoint)) _MissingGlyphs++;
            }
        }

        private static bool IsSafeExternalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
            string scheme = uri.Scheme.ToLowerInvariant();
            return scheme == "http" || scheme == "https" || scheme == "mailto" || scheme == "ftp" || scheme == "ftps";
        }

        private static bool IsUnsafeScheme(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
            string scheme = uri.Scheme.ToLowerInvariant();
            return !(scheme == "http" || scheme == "https" || scheme == "mailto" || scheme == "ftp" || scheme == "ftps");
        }

        #endregion
    }
}
